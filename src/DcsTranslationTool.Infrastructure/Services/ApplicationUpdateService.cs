using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;

using FluentResults;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// GitHub Release からアプリケーション更新を取得して安全にステージングする。
/// </summary>
/// <param name="packageService">更新パッケージサービスを受け取る。</param>
/// <param name="logger">ロギングサービスを受け取る。</param>
/// <param name="processLauncher">外部プロセス起動サービスを受け取る。</param>
/// <param name="httpClient">更新取得用 HTTP クライアントを受け取る。</param>
/// <param name="applicationDirectory">現在の配布ルートを受け取る。</param>
/// <param name="updatesDirectory">更新作業ルートを受け取る。</param>
public sealed partial class ApplicationUpdateService(
    ApplicationUpdatePackageService packageService,
    ILoggingService logger,
    IProcessLauncher processLauncher,
    HttpClient? httpClient = null,
    string? applicationDirectory = null,
    string? updatesDirectory = null
) : IApplicationUpdateService {
    private const string PendingUpdateFileName = "pending-update.json";
    private const int DownloadBufferSize = 81920;

    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web )
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly HttpClient _httpClient = InitializeHttpClient( httpClient );
    private readonly string _applicationDirectory = ResolveApplicationDirectory( applicationDirectory );
    private readonly string _updatesDirectory = ResolveUpdatesDirectory( updatesDirectory );
    private readonly IProcessLauncher _processLauncher = processLauncher;

    /// <inheritdoc />
    public async Task<Result<ApplicationUpdateStageResult>> DownloadAndStageAsync(
        UpdateCheckResult update,
        IProgress<ApplicationUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default
    ) {
        if(!update.IsUpdateAvailable || !update.IsImmutable || update.LatestVersion is null ||
            string.IsNullOrWhiteSpace( update.RuntimeIdentifier ) || update.FullAsset is null ||
            string.IsNullOrWhiteSpace( update.ReleaseUrl )) {
            return Result.Fail( ResultErrorFactory.Validation( "自動更新に必要な情報が不足しています。", "UPDATE_INFORMATION_INCOMPLETE" ) );
        }

        Directory.CreateDirectory( this._updatesDirectory );
        var workspaceDirectory = this.CreateWorkspaceDirectory( update.LatestVersion, update.RuntimeIdentifier );

        try {
            if(update.IsDeltaPreferred) {
                var deltaResult = await this.StageDeltaChainAsync(
                    update,
                    workspaceDirectory,
                    progress,
                    cancellationToken );
                if(deltaResult.IsSuccess) {
                    return PersistOrCleanup( deltaResult.Value );
                }

                logger.Warn( $"差分更新の取得または検証に失敗したため完全版へフォールバックする。Errors={string.Join( "; ", deltaResult.Errors.Select( error => error.Message ) )}" );
                TryDeleteDirectory( workspaceDirectory );
                workspaceDirectory = this.CreateWorkspaceDirectory( update.LatestVersion, update.RuntimeIdentifier );
            }

            var fullResult = await this.StageAssetAsync(
                update.FullAsset,
                update,
                workspaceDirectory,
                progress,
                cancellationToken );
            if(fullResult.IsSuccess) {
                return PersistOrCleanup( fullResult.Value );
            }

            TryDeleteDirectory( workspaceDirectory );
            return fullResult;
        }
        catch(OperationCanceledException) {
            TryDeleteDirectory( workspaceDirectory );
            throw;
        }
    }

    /// <summary>
    /// 更新先とRIDに対応する一意な更新作業ディレクトリを作成する。
    /// </summary>
    private string CreateWorkspaceDirectory( Version targetVersion, string runtimeIdentifier ) {
        var workspaceDirectory = Path.Combine(
            this._updatesDirectory,
            $"{targetVersion.ToString( 3 )}-{runtimeIdentifier}-{Guid.NewGuid():N}" );
        Directory.CreateDirectory( workspaceDirectory );
        return workspaceDirectory;
    }

    /// <summary>
    /// 差分アセットを基準版順に取得し、最新版のステージを逐次再構築する。
    /// </summary>
    private async Task<Result<ApplicationUpdateStageResult>> StageDeltaChainAsync(
        UpdateCheckResult update,
        string workspaceDirectory,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken
    ) {
        var currentManifestResult = packageService.ReadInstalledManifest( this._applicationDirectory );
        if(currentManifestResult.IsFailed) {
            return currentManifestResult.ToResult<ApplicationUpdateStageResult>();
        }

        var baseDirectory = this._applicationDirectory;
        var baseManifest = currentManifestResult.Value;
        long completedDownloadBytes = 0;
        try {
            for(var index = 0; index < update.DeltaAssets.Count; index++) {
                var asset = update.DeltaAssets[index];
                var assetPath = Path.Combine( workspaceDirectory, asset.Name );
                var checksumPath = Path.Combine( workspaceDirectory, asset.Checksum.Name );
                var isLast = index == update.DeltaAssets.Count - 1;
                var destinationDirectory = Path.Combine( workspaceDirectory, isLast ? "staging" : $"staging-{index + 1}" );
                var downloadResult = await this.DownloadFileAsync(
                    asset.DownloadUrl,
                    assetPath,
                    asset.Size,
                    asset.Sha256Digest,
                    new AggregateProgress( progress, completedDownloadBytes, update.DeltaDownloadSize ),
                    cancellationToken );
                if(downloadResult.IsFailed) {
                    return downloadResult.ToResult<ApplicationUpdateStageResult>();
                }

                completedDownloadBytes = AddDownloadSize( completedDownloadBytes, asset.Size );
                var checksumResult = await this.DownloadFileAsync(
                    asset.Checksum.DownloadUrl,
                    checksumPath,
                    asset.Checksum.Size,
                    asset.Checksum.Sha256Digest,
                    new AggregateProgress( progress, completedDownloadBytes, update.DeltaDownloadSize ),
                    cancellationToken );
                if(checksumResult.IsFailed) {
                    return checksumResult.ToResult<ApplicationUpdateStageResult>();
                }

                completedDownloadBytes = AddDownloadSize( completedDownloadBytes, asset.Checksum.Size );
                progress?.Report( new ApplicationUpdateProgress(
                    ApplicationUpdateProgressPhase.Verifying,
                    completedDownloadBytes,
                    update.DeltaDownloadSize ) );
                var sidecarResult = ValidateSidecar( checksumPath, asset.Name, assetPath );
                if(sidecarResult.IsFailed) {
                    return sidecarResult.ToResult<ApplicationUpdateStageResult>();
                }

                progress?.Report( new ApplicationUpdateProgress(
                    ApplicationUpdateProgressPhase.Staging,
                    completedDownloadBytes,
                    update.DeltaDownloadSize ) );
                var packageResult = packageService.ReconstructDeltaPackage(
                    assetPath,
                    baseDirectory,
                    destinationDirectory,
                    baseManifest,
                    asset.TargetVersion,
                    update.RuntimeIdentifier! );
                if(packageResult.IsFailed) {
                    return packageResult.ToResult<ApplicationUpdateStageResult>();
                }

                TryDeleteFile( assetPath );
                TryDeleteFile( checksumPath );
                if(!PathsEqual( baseDirectory, this._applicationDirectory )) {
                    TryDeleteDirectory( baseDirectory );
                }

                baseDirectory = destinationDirectory;
                baseManifest = packageResult.Value;
            }

            return Result.Ok( new ApplicationUpdateStageResult(
                workspaceDirectory,
                baseDirectory,
                update.LatestVersion!,
                update.RuntimeIdentifier!,
                ApplicationUpdateAssetKind.Delta,
                update.ReleaseUrl! ) );
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or HttpRequestException) {
            logger.Error( "差分チェーンのステージングに失敗した。", ex );
            return Result.Fail( ResultErrorFactory.External( "差分更新を準備できませんでした。", "UPDATE_DELTA_CHAIN_STAGE_FAILED", ex ) );
        }
    }

    /// <inheritdoc />
    public Result<ApplicationUpdateStageResult?> GetPendingUpdate() {
        var pendingPath = Path.Combine( this._updatesDirectory, PendingUpdateFileName );
        if(!File.Exists( pendingPath )) {
            return Result.Ok<ApplicationUpdateStageResult?>( null );
        }

        try {
            var pending = JsonSerializer.Deserialize<ApplicationUpdateStageResult>( File.ReadAllText( pendingPath ), JsonOptions );
            if(pending is null || !this.ValidateStageResultPaths( pending )) {
                return Result.Fail( ResultErrorFactory.Validation( "保留中の更新情報が不正です。", "UPDATE_PENDING_INVALID" ) );
            }

            return Result.Ok<ApplicationUpdateStageResult?>( pending );
        }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
            return Result.Fail( ResultErrorFactory.External( "保留中の更新情報を読み込めませんでした。", "UPDATE_PENDING_READ_FAILED", ex ) );
        }
    }

    /// <inheritdoc />
    public Result CleanupCompletedUpdates() {
        if(!Directory.Exists( this._updatesDirectory )) {
            return Result.Ok();
        }

        try {
            if(ContainsReparsePoint( this._updatesDirectory )) {
                return Result.Fail( ResultErrorFactory.Validation( "更新作業ルートに再解析ポイントを使用できません。", "UPDATE_CLEANUP_ROOT_INVALID" ) );
            }

            string? pendingWorkspace = null;
            var pendingResult = this.GetPendingUpdate();
            if(pendingResult.IsSuccess && pendingResult.Value is not null) {
                pendingWorkspace = Path.GetFullPath( pendingResult.Value.WorkspaceDirectory );
            }

            foreach(var workspace in Directory.GetDirectories( this._updatesDirectory, "*", SearchOption.TopDirectoryOnly )) {
                if(pendingWorkspace is not null && PathsEqual( workspace, pendingWorkspace )) {
                    continue;
                }

                if(!WorkspaceDirectoryNameRegex().IsMatch( Path.GetFileName( workspace ) )) {
                    continue;
                }

                if(ContainsReparsePointInTree( workspace )) {
                    logger.Warn( $"再解析ポイントを含む更新作業領域を削除しない。Workspace={workspace}" );
                    continue;
                }

                TryDeleteDirectory( workspace );
            }

            return Result.Ok();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {
            logger.Warn( "完了済み更新の作業領域をクリーンアップできなかった。", ex );
            return Result.Fail( ResultErrorFactory.External( "完了済み更新をクリーンアップできませんでした。", "UPDATE_CLEANUP_FAILED", ex ) );
        }
    }

    /// <inheritdoc />
    public Result LaunchApplyProcess( ApplicationUpdateStageResult stageResult, int parentProcessId ) {
        ArgumentNullException.ThrowIfNull( stageResult );
        try {
            if(parentProcessId < 0 || !this.ValidateStageResultPaths( stageResult )) {
                return Result.Fail( ResultErrorFactory.Validation( "ステージ済み更新のパスが不正です。", "UPDATE_STAGE_PATH_INVALID" ) );
            }

            var currentManifestResult = packageService.ReadInstalledManifest( this._applicationDirectory );
            if(currentManifestResult.IsFailed) {
                return currentManifestResult.ToResult();
            }

            var currentManifest = currentManifestResult.Value;
            var currentVersion = Version.Parse( currentManifest.Version );
            var currentValidation = packageService.ValidateManifest(
                this._applicationDirectory,
                currentManifest,
                currentVersion,
                stageResult.RuntimeIdentifier,
                requireExactFileSet: false );
            if(currentValidation.IsFailed) {
                return currentValidation;
            }

            var runnerDirectory = Path.Combine( stageResult.WorkspaceDirectory, "runner" );
            if(Directory.Exists( runnerDirectory )) {
                Directory.Delete( runnerDirectory, true );
            }

            Directory.CreateDirectory( runnerDirectory );
            foreach(var file in currentManifest.Files) {
                CopyRelativeFile( this._applicationDirectory, runnerDirectory, file.Path, overwrite: false );
            }
            File.Copy(
                Path.Combine( this._applicationDirectory, ApplicationUpdatePackageService.ManifestFileName ),
                Path.Combine( runnerDirectory, ApplicationUpdatePackageService.ManifestFileName ),
                false );

            var request = new ApplicationUpdateApplyRequest(
                this._applicationDirectory,
                stageResult.StagingDirectory,
                Path.Combine( stageResult.WorkspaceDirectory, "backup" ),
                parentProcessId,
                stageResult.RuntimeIdentifier,
                currentVersion.ToString( 3 ),
                stageResult.TargetVersion.ToString( 3 ) );
            var requestPath = Path.Combine( stageResult.WorkspaceDirectory, "apply-request.json" );
            File.WriteAllText( requestPath, JsonSerializer.Serialize( request, JsonOptions ) );

            var executablePath = Path.Combine( runnerDirectory, currentManifest.EntryPoint );
            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = runnerDirectory,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add( "--apply-update" );
            startInfo.ArgumentList.Add( requestPath );
            return this._processLauncher.Start( startInfo ) is null
                ? Result.Fail( ResultErrorFactory.External( "更新適用プロセスを開始できませんでした。", "UPDATE_APPLY_PROCESS_START_FAILED" ) )
                : Result.Ok();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException) {
            logger.Error( "更新適用プロセスの準備に失敗した。", ex );
            return Result.Fail( ResultErrorFactory.External( "更新適用プロセスを準備できませんでした。", "UPDATE_APPLY_PROCESS_PREPARE_FAILED", ex ) );
        }
    }

    /// <inheritdoc />
    public async Task<Result> ApplyPreparedUpdateAsync( string requestPath, CancellationToken cancellationToken = default ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( requestPath );
        ApplicationUpdateApplyRequest? request;
        try {
            request = JsonSerializer.Deserialize<ApplicationUpdateApplyRequest>( await File.ReadAllTextAsync( requestPath, cancellationToken ), JsonOptions );
        }
        catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException) {
            return Result.Fail( ResultErrorFactory.External( "更新適用要求を読み込めませんでした。", "UPDATE_APPLY_REQUEST_READ_FAILED", ex ) );
        }

        if(request is null || !ValidateApplyRequestPaths( requestPath, request )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新適用要求のパスが不正です。", "UPDATE_APPLY_REQUEST_INVALID" ) );
        }

        try {
            await WaitForParentExitAsync( request.ParentProcessId, cancellationToken );
            EnsureDirectoryWritable( request.ApplicationDirectory );

            var currentManifestResult = packageService.ReadInstalledManifest( request.ApplicationDirectory );
            var targetManifestResult = packageService.ReadInstalledManifest( request.StagingDirectory );
            if(currentManifestResult.IsFailed || targetManifestResult.IsFailed) {
                return Result.Fail( currentManifestResult.Errors.Concat( targetManifestResult.Errors ) );
            }

            var currentManifest = currentManifestResult.Value;
            var targetManifest = targetManifestResult.Value;
            if(!string.Equals( Version.Parse( currentManifest.Version ).ToString( 3 ), Version.Parse( request.BaseVersion ).ToString( 3 ), StringComparison.Ordinal )) {
                return Result.Fail( ResultErrorFactory.Conflict(
                    "更新の準備後にアプリケーションのバージョンが変更されています。更新を再確認してください。",
                    "UPDATE_APPLY_BASE_VERSION_MISMATCH" ) );
            }
            var currentValidation = packageService.ValidateManifest(
                request.ApplicationDirectory,
                currentManifest,
                Version.Parse( currentManifest.Version ),
                request.RuntimeIdentifier,
                requireExactFileSet: false );
            var targetValidation = packageService.ValidateManifest(
                request.StagingDirectory,
                targetManifest,
                Version.Parse( request.TargetVersion ),
                request.RuntimeIdentifier,
                requireExactFileSet: true );
            if(currentValidation.IsFailed || targetValidation.IsFailed) {
                return Result.Fail( currentValidation.Errors.Concat( targetValidation.Errors ) );
            }

            var currentFilesByPath = currentManifest.Files.ToDictionary(
                file => NormalizeRelativePath( file.Path ),
                StringComparer.OrdinalIgnoreCase );
            foreach(var targetFile in targetManifest.Files) {
                var normalizedPath = NormalizeRelativePath( targetFile.Path );
                var destinationPath = ResolveRelativePath( request.ApplicationDirectory, normalizedPath );
                if(!currentFilesByPath.ContainsKey( normalizedPath ) && (File.Exists( destinationPath ) || Directory.Exists( destinationPath ))) {
                    return Result.Fail( ResultErrorFactory.Conflict(
                        $"管理対象外ファイルと更新先が衝突しています: {targetFile.Path}",
                        "UPDATE_APPLY_UNKNOWN_FILE_COLLISION" ) );
                }
            }

            BackupCurrentFiles( request, currentManifest );
            try {
                foreach(var targetFile in targetManifest.Files) {
                    var normalizedPath = NormalizeRelativePath( targetFile.Path );
                    if(!currentFilesByPath.TryGetValue( normalizedPath, out var currentFile ) || !HaveSameContent( currentFile, targetFile )) {
                        CopyRelativeFile( request.StagingDirectory, request.ApplicationDirectory, targetFile.Path, overwrite: true );
                    }
                }

                var targetPaths = targetManifest.Files.Select( file => NormalizeRelativePath( file.Path ) )
                    .ToHashSet( StringComparer.OrdinalIgnoreCase );
                foreach(var oldFile in currentManifest.Files.Where( file => !targetPaths.Contains( NormalizeRelativePath( file.Path ) ) )) {
                    var oldPath = ResolveRelativePath( request.ApplicationDirectory, oldFile.Path );
                    if(File.Exists( oldPath )) {
                        File.Delete( oldPath );
                    }
                }

                File.Copy(
                    Path.Combine( request.StagingDirectory, ApplicationUpdatePackageService.ManifestFileName ),
                    Path.Combine( request.ApplicationDirectory, ApplicationUpdatePackageService.ManifestFileName ),
                    true );
                var finalValidation = packageService.ValidateManifest(
                    request.ApplicationDirectory,
                    targetManifest,
                    Version.Parse( request.TargetVersion ),
                    request.RuntimeIdentifier,
                    requireExactFileSet: false );
                if(finalValidation.IsFailed) {
                    throw new InvalidDataException( string.Join( "; ", finalValidation.Errors.Select( error => error.Message ) ) );
                }

                var executablePath = ResolveRelativePath( request.ApplicationDirectory, targetManifest.EntryPoint );
                var process = this._processLauncher.Start( new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = request.ApplicationDirectory,
                    UseShellExecute = false
                } );
                if(process is null) {
                    throw new InvalidOperationException( "更新後のアプリケーションを開始できませんでした。" );
                }

                TryDeleteFile( Path.Combine( this._updatesDirectory, PendingUpdateFileName ) );
                CleanupAppliedWorkspace( Path.GetDirectoryName( Path.GetFullPath( requestPath ) )! );
                return Result.Ok();
            }
            catch(Exception applyException) when(applyException is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException) {
                logger.Error( "更新ファイルの置換に失敗したためロールバックする。", applyException );
                try {
                    RestoreBackup( request, currentManifest, targetManifest );
                }
                catch(Exception rollbackException) when(rollbackException is IOException or UnauthorizedAccessException) {
                    logger.Error( $"更新のロールバックに失敗した。Backup={request.BackupDirectory}", rollbackException );
                    return Result.Fail( ResultErrorFactory.External(
                        $"更新と復元に失敗しました。バックアップを保持しています: {request.BackupDirectory}",
                        "UPDATE_ROLLBACK_FAILED",
                        rollbackException ) );
                }

                return Result.Fail( ResultErrorFactory.External( "更新を適用できなかったため以前のバージョンへ復元しました。", "UPDATE_APPLY_FAILED", applyException ) );
            }
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException or FormatException or ArgumentException) {
            logger.Error( "更新適用前の検証に失敗した。", ex );
            return Result.Fail( ResultErrorFactory.External( "更新を適用できませんでした。現行版を維持します。", "UPDATE_APPLY_VALIDATION_FAILED", ex ) );
        }
    }

    private async Task<Result<ApplicationUpdateStageResult>> StageAssetAsync(
        ApplicationUpdateAsset asset,
        UpdateCheckResult update,
        string workspaceDirectory,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken
    ) {
        var assetPath = Path.Combine( workspaceDirectory, asset.Name );
        var checksumPath = Path.Combine( workspaceDirectory, asset.Checksum.Name );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        var totalDownloadSize = AddDownloadSize( asset.Size, asset.Checksum.Size );
        try {
            var downloadResult = await this.DownloadFileAsync(
                asset.DownloadUrl,
                assetPath,
                asset.Size,
                asset.Sha256Digest,
                new AggregateProgress( progress, 0, totalDownloadSize ),
                cancellationToken );
            if(downloadResult.IsFailed) {
                return downloadResult.ToResult<ApplicationUpdateStageResult>();
            }

            var checksumResult = await this.DownloadFileAsync(
                asset.Checksum.DownloadUrl,
                checksumPath,
                asset.Checksum.Size,
                asset.Checksum.Sha256Digest,
                new AggregateProgress( progress, asset.Size, totalDownloadSize ),
                cancellationToken );
            if(checksumResult.IsFailed) {
                return checksumResult.ToResult<ApplicationUpdateStageResult>();
            }

            progress?.Report( new ApplicationUpdateProgress( ApplicationUpdateProgressPhase.Verifying, totalDownloadSize, totalDownloadSize ) );
            var sidecarResult = ValidateSidecar( checksumPath, asset.Name, assetPath );
            if(sidecarResult.IsFailed) {
                return sidecarResult.ToResult<ApplicationUpdateStageResult>();
            }

            progress?.Report( new ApplicationUpdateProgress( ApplicationUpdateProgressPhase.Staging, totalDownloadSize, totalDownloadSize ) );
            Result<ApplicationUpdateManifest> packageResult;
            if(asset.Kind == ApplicationUpdateAssetKind.Delta) {
                var currentManifestResult = packageService.ReadInstalledManifest( this._applicationDirectory );
                if(currentManifestResult.IsFailed) {
                    return currentManifestResult.ToResult<ApplicationUpdateStageResult>();
                }

                packageResult = packageService.ReconstructDeltaPackage(
                    assetPath,
                    this._applicationDirectory,
                    stagingDirectory,
                    currentManifestResult.Value,
                    update.LatestVersion!,
                    update.RuntimeIdentifier! );
            }
            else {
                packageResult = packageService.ExtractFullPackage(
                    assetPath,
                    stagingDirectory,
                    update.LatestVersion!,
                    update.RuntimeIdentifier! );
            }

            if(packageResult.IsFailed) {
                return packageResult.ToResult<ApplicationUpdateStageResult>();
            }

            return Result.Ok( new ApplicationUpdateStageResult(
                workspaceDirectory,
                stagingDirectory,
                update.LatestVersion!,
                update.RuntimeIdentifier!,
                asset.Kind,
                update.ReleaseUrl! ) );
        }
        catch(OperationCanceledException) {
            throw;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or HttpRequestException) {
            logger.Error( $"更新アセットのステージングに失敗した。Asset={asset.Name}", ex );
            return Result.Fail( ResultErrorFactory.External( "更新アセットを準備できませんでした。", "UPDATE_STAGE_FAILED", ex ) );
        }
    }

    private async Task<Result> DownloadFileAsync(
        string downloadUrl,
        string destinationPath,
        long expectedSize,
        string expectedDigest,
        IProgress<ApplicationUpdateProgress>? progress,
        CancellationToken cancellationToken
    ) {
        if(!TryParseDigest( expectedDigest, out var expectedHash )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新アセットの digest が不正です。", "UPDATE_DIGEST_INVALID" ) );
        }

        using var request = new HttpRequestMessage( HttpMethod.Get, downloadUrl );
        using var response = await this._httpClient.SendAsync( request, HttpCompletionOption.ResponseHeadersRead, cancellationToken ).ConfigureAwait( false );
        if(!response.IsSuccessStatusCode) {
            return Result.Fail( ResultErrorFactory.External( $"更新アセットの取得に失敗しました。Status={(int)response.StatusCode}", "UPDATE_DOWNLOAD_HTTP_FAILED" ) );
        }

        if(response.Content.Headers.ContentLength is not long contentLength || contentLength != expectedSize) {
            return Result.Fail( ResultErrorFactory.Validation( "更新アセットの Content-Length が一致しません。", "UPDATE_DOWNLOAD_LENGTH_MISMATCH" ) );
        }

        var parent = Path.GetDirectoryName( destinationPath );
        if(!string.IsNullOrWhiteSpace( parent )) {
            Directory.CreateDirectory( parent );
        }

        long downloadedBytes = 0;
        await using(var source = await response.Content.ReadAsStreamAsync( cancellationToken ).ConfigureAwait( false )) {
            await using var destination = new FileStream( destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, true );
            var buffer = new byte[DownloadBufferSize];
            while(true) {
                var read = await source.ReadAsync( buffer, cancellationToken ).ConfigureAwait( false );
                if(read == 0) {
                    break;
                }

                downloadedBytes += read;
                if(downloadedBytes > expectedSize) {
                    return Result.Fail( ResultErrorFactory.Validation( "更新アセットが想定サイズを超えました。", "UPDATE_DOWNLOAD_TOO_LARGE" ) );
                }

                await destination.WriteAsync( buffer.AsMemory( 0, read ), cancellationToken ).ConfigureAwait( false );
                progress?.Report( new ApplicationUpdateProgress( ApplicationUpdateProgressPhase.Downloading, downloadedBytes, expectedSize ) );
            }

            await destination.FlushAsync( cancellationToken ).ConfigureAwait( false );
        }

        if(downloadedBytes != expectedSize || !string.Equals( CalculateSha256( destinationPath ), expectedHash, StringComparison.OrdinalIgnoreCase )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新アセットの SHA-256 が一致しません。", "UPDATE_DOWNLOAD_HASH_MISMATCH" ) );
        }

        return Result.Ok();
    }

    private Result<ApplicationUpdateStageResult> PersistPendingUpdate( ApplicationUpdateStageResult stageResult ) {
        try {
            Directory.CreateDirectory( this._updatesDirectory );
            File.WriteAllText(
                Path.Combine( this._updatesDirectory, PendingUpdateFileName ),
                JsonSerializer.Serialize( stageResult, JsonOptions ) );
            return Result.Ok( stageResult );
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            logger.Error( "保留中の更新情報を保存できなかった。", ex );
            return Result.Fail( ResultErrorFactory.External( "更新の保留状態を保存できませんでした。", "UPDATE_PENDING_WRITE_FAILED", ex ) );
        }
    }

    private Result<ApplicationUpdateStageResult> PersistOrCleanup( ApplicationUpdateStageResult stageResult ) {
        var result = PersistPendingUpdate( stageResult );
        if(result.IsFailed) {
            TryDeleteDirectory( stageResult.WorkspaceDirectory );
        }
        return result;
    }

    private static Result ValidateSidecar( string sidecarPath, string expectedAssetName, string assetPath ) {
        var sidecar = File.ReadAllText( sidecarPath ).Trim();
        var match = SidecarRegex().Match( sidecar );
        if(!match.Success ||
            !string.Equals( match.Groups["name"].Value.Trim(), expectedAssetName, StringComparison.Ordinal ) ||
            !string.Equals( match.Groups["hash"].Value, CalculateSha256( assetPath ), StringComparison.OrdinalIgnoreCase )) {
            return Result.Fail( ResultErrorFactory.Validation( "SHA-256 sidecar の内容が一致しません。", "UPDATE_SIDECAR_MISMATCH" ) );
        }

        return Result.Ok();
    }

    private static bool TryParseDigest( string digest, out string hash ) {
        const string prefix = "sha256:";
        hash = digest.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) ? digest[prefix.Length..] : string.Empty;
        return Sha256Regex().IsMatch( hash );
    }

    private static string CalculateSha256( string path ) {
        using var stream = File.OpenRead( path );
        return Convert.ToHexString( SHA256.HashData( stream ) ).ToLowerInvariant();
    }

    private static HttpClient InitializeHttpClient( HttpClient? httpClient ) {
        var client = httpClient ?? new HttpClient();
        if(client.DefaultRequestHeaders.UserAgent.Count == 0) {
            client.DefaultRequestHeaders.UserAgent.Add( new ProductInfoHeaderValue( "DCS-Translation-Tool", "1.0" ) );
        }

        return client;
    }

    private static string ResolveApplicationDirectory( string? applicationDirectory ) {
        if(!string.IsNullOrWhiteSpace( applicationDirectory )) {
            return Path.GetFullPath( applicationDirectory );
        }

        var processPath = Environment.ProcessPath;
        return Path.GetFullPath( !string.IsNullOrWhiteSpace( processPath )
            ? Path.GetDirectoryName( processPath ) ?? AppContext.BaseDirectory
            : AppContext.BaseDirectory );
    }

    private static string ResolveUpdatesDirectory( string? updatesDirectory ) {
        if(!string.IsNullOrWhiteSpace( updatesDirectory )) {
            return Path.GetFullPath( updatesDirectory );
        }

#if E2E_TESTING
        return ApplicationUpdateE2eConfiguration.Load().UpdatesDirectory;
#else
        return Path.Combine(
            Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData ),
            "DcsTranslationTool",
            "updates" );
#endif
    }

    private bool ValidateApplyRequestPaths( string requestPath, ApplicationUpdateApplyRequest request ) {
        if(!Path.IsPathFullyQualified( request.ApplicationDirectory ) ||
            !Path.IsPathFullyQualified( request.StagingDirectory ) ||
            !Path.IsPathFullyQualified( request.BackupDirectory ) ||
            !Path.IsPathFullyQualified( requestPath ) ||
            request.ParentProcessId < 0 ||
            request.RuntimeIdentifier is not ("win-x64" or "win-x86") ||
            !Version.TryParse( request.BaseVersion, out _ ) ||
            !Version.TryParse( request.TargetVersion, out var targetVersion )) {
            return false;
        }

        var workspace = Path.GetDirectoryName( Path.GetFullPath( requestPath ) );
        if(string.IsNullOrWhiteSpace( workspace )) {
            return false;
        }

        var staging = Path.GetFullPath( request.StagingDirectory );
        var backup = Path.GetFullPath( request.BackupDirectory );
        return this.IsOwnedWorkspace( workspace, targetVersion, request.RuntimeIdentifier ) &&
            PathsEqual( requestPath, Path.Combine( workspace, "apply-request.json" ) ) &&
            PathsEqual( staging, Path.Combine( workspace, "staging" ) ) &&
            PathsEqual( backup, Path.Combine( workspace, "backup" ) ) &&
            !ContainsReparsePointInTree( workspace ) &&
            !IsPathWithinRoot( request.ApplicationDirectory, workspace ) &&
            !IsPathWithinRoot( workspace, request.ApplicationDirectory );
    }

    private bool ValidateStageResultPaths( ApplicationUpdateStageResult stageResult ) {
        if(!Path.IsPathFullyQualified( stageResult.WorkspaceDirectory ) ||
            !Path.IsPathFullyQualified( stageResult.StagingDirectory ) ||
            stageResult.RuntimeIdentifier is not ("win-x64" or "win-x86") ||
            stageResult.TargetVersion <= new Version( 0, 0 ) ||
            !Uri.TryCreate( stageResult.ReleaseUrl, UriKind.Absolute, out var releaseUri ) ||
            !IsTrustedReleaseUri( releaseUri )) {
            return false;
        }

        var workspace = Path.GetFullPath( stageResult.WorkspaceDirectory );
        var staging = Path.GetFullPath( stageResult.StagingDirectory );
        return this.IsOwnedWorkspace( workspace, stageResult.TargetVersion, stageResult.RuntimeIdentifier ) &&
            PathsEqual( staging, Path.Combine( workspace, "staging" ) ) &&
            Directory.Exists( staging ) &&
            !ContainsReparsePointInTree( workspace );
    }

    /// <summary>
    /// 更新元Release URIが現在のビルドで許可された境界内かを確認する。
    /// </summary>
    /// <param name="releaseUri">検証するRelease URIを受け取る。</param>
    /// <returns>信頼できる場合はtrueを返す。</returns>
    private static bool IsTrustedReleaseUri( Uri releaseUri ) {
#if E2E_TESTING
        if(releaseUri.Scheme == Uri.UriSchemeHttp && releaseUri.IsLoopback) {
            return true;
        }
#endif
        return releaseUri.Scheme == Uri.UriSchemeHttps;
    }

    private bool IsOwnedWorkspace( string workspaceDirectory, Version targetVersion, string runtimeIdentifier ) {
        var workspace = Path.GetFullPath( workspaceDirectory )
            .TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
        var parent = Path.GetDirectoryName( workspace );
        if(string.IsNullOrWhiteSpace( parent ) || !PathsEqual( parent, this._updatesDirectory )) {
            return false;
        }

        var match = WorkspaceDirectoryNameRegex().Match( Path.GetFileName( workspace ) );
        return match.Success &&
            Version.TryParse( match.Groups["version"].Value, out var workspaceVersion ) &&
            workspaceVersion.Major == targetVersion.Major &&
            workspaceVersion.Minor == targetVersion.Minor &&
            workspaceVersion.Build == targetVersion.Build &&
            string.Equals( match.Groups["rid"].Value, runtimeIdentifier, StringComparison.OrdinalIgnoreCase );
    }

    private static bool ContainsReparsePoint( string path ) {
        var fullPath = Path.GetFullPath( path );
        if(!Directory.Exists( fullPath ) && !File.Exists( fullPath )) {
            return false;
        }

        return File.GetAttributes( fullPath ).HasFlag( FileAttributes.ReparsePoint );
    }

    /// <summary>
    /// 更新ワークスペース名を検証する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex(
        "^(?<version>[0-9]+\\.[0-9]+\\.[0-9]+)-(?<rid>win-x64|win-x86)-[0-9a-f]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase )]
    private static partial Regex WorkspaceDirectoryNameRegex();

    /// <summary>
    /// SHA-256 sidecarの内容を解析する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex( "^(?<hash>[0-9a-fA-F]{64})\\s+\\*?(?<name>[^\\r\\n]+)$", RegexOptions.CultureInvariant )]
    private static partial Regex SidecarRegex();

    /// <summary>
    /// SHA-256文字列を検証する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex( "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant )]
    private static partial Regex Sha256Regex();

    private static bool ContainsReparsePointInTree( string root ) {
        if(ContainsReparsePoint( root )) {
            return true;
        }

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push( Path.GetFullPath( root ) );
        while(pendingDirectories.TryPop( out var directory )) {
            foreach(var path in Directory.GetFileSystemEntries( directory )) {
                if(ContainsReparsePoint( path )) {
                    return true;
                }
                if(Directory.Exists( path )) {
                    pendingDirectories.Push( path );
                }
            }
        }

        return false;
    }

    private void CleanupAppliedWorkspace( string workspaceDirectory ) {
        try {
            foreach(var path in Directory.GetFileSystemEntries( workspaceDirectory )) {
                if(string.Equals( Path.GetFileName( path ), "runner", StringComparison.OrdinalIgnoreCase )) {
                    continue;
                }

                if(Directory.Exists( path )) {
                    TryDeleteDirectory( path );
                }
                else {
                    TryDeleteFile( path );
                }
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            logger.Warn( $"適用済み更新の作業ファイルをクリーンアップできなかった。Workspace={workspaceDirectory}", ex );
        }
    }

    private static bool PathsEqual( string left, string right ) =>
        string.Equals(
            Path.GetFullPath( left ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ),
            Path.GetFullPath( right ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ),
            StringComparison.OrdinalIgnoreCase );

    private static async Task WaitForParentExitAsync( int parentProcessId, CancellationToken cancellationToken ) {
        if(parentProcessId == 0) {
            return;
        }

        try {
            using var parent = Process.GetProcessById( parentProcessId );
            await parent.WaitForExitAsync( cancellationToken );
        }
        catch(ArgumentException) {
            // 親プロセスが既に終了している場合はそのまま適用を続行する。
        }
    }

    private static void EnsureDirectoryWritable( string directory ) {
        if(!Directory.Exists( directory )) {
            throw new DirectoryNotFoundException( directory );
        }

        var probePath = Path.Combine( directory, $".update-write-test-{Guid.NewGuid():N}.tmp" );
        try {
            using var stream = new FileStream( probePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose );
            stream.WriteByte( 0 );
        }
        finally {
            TryDeleteFile( probePath );
        }
    }

    private static void BackupCurrentFiles( ApplicationUpdateApplyRequest request, ApplicationUpdateManifest currentManifest ) {
        if(Directory.Exists( request.BackupDirectory )) {
            Directory.Delete( request.BackupDirectory, true );
        }

        Directory.CreateDirectory( request.BackupDirectory );
        foreach(var file in currentManifest.Files) {
            CopyRelativeFile( request.ApplicationDirectory, request.BackupDirectory, file.Path, overwrite: false );
        }
        File.Copy(
            Path.Combine( request.ApplicationDirectory, ApplicationUpdatePackageService.ManifestFileName ),
            Path.Combine( request.BackupDirectory, ApplicationUpdatePackageService.ManifestFileName ),
            false );
    }

    private static void RestoreBackup(
        ApplicationUpdateApplyRequest request,
        ApplicationUpdateManifest currentManifest,
        ApplicationUpdateManifest targetManifest
    ) {
        var currentFilesByPath = currentManifest.Files.ToDictionary(
            file => NormalizeRelativePath( file.Path ),
            StringComparer.OrdinalIgnoreCase );
        foreach(var targetFile in targetManifest.Files) {
            var normalizedPath = NormalizeRelativePath( targetFile.Path );
            if(!currentFilesByPath.TryGetValue( normalizedPath, out var currentFile ) || !HaveSameContent( currentFile, targetFile )) {
                var targetPath = ResolveRelativePath( request.ApplicationDirectory, targetFile.Path );
                if(File.Exists( targetPath )) {
                    File.Delete( targetPath );
                }
            }
        }

        foreach(var oldFile in currentManifest.Files) {
            var normalizedPath = NormalizeRelativePath( oldFile.Path );
            var targetFile = targetManifest.Files.FirstOrDefault( file => string.Equals( NormalizeRelativePath( file.Path ), normalizedPath, StringComparison.OrdinalIgnoreCase ) );
            if(targetFile is null || !HaveSameContent( oldFile, targetFile )) {
                CopyRelativeFile( request.BackupDirectory, request.ApplicationDirectory, oldFile.Path, overwrite: true );
            }
        }
        File.Copy(
            Path.Combine( request.BackupDirectory, ApplicationUpdatePackageService.ManifestFileName ),
            Path.Combine( request.ApplicationDirectory, ApplicationUpdatePackageService.ManifestFileName ),
            true );
    }

    private static void CopyRelativeFile( string sourceRoot, string destinationRoot, string relativePath, bool overwrite ) {
        var sourcePath = ResolveRelativePath( sourceRoot, relativePath );
        var destinationPath = ResolveRelativePath( destinationRoot, relativePath );
        var parent = Path.GetDirectoryName( destinationPath );
        if(!string.IsNullOrWhiteSpace( parent )) {
            Directory.CreateDirectory( parent );
        }
        File.Copy( sourcePath, destinationPath, overwrite );
    }

    /// <summary>
    /// マニフェスト上で同一内容のファイルかどうかを確認する。
    /// </summary>
    /// <param name="first">比較元のファイルを受け取る。</param>
    /// <param name="second">比較先のファイルを受け取る。</param>
    /// <returns>サイズとハッシュが一致する場合はtrueを返す。</returns>
    private static bool HaveSameContent( ApplicationUpdateManifestFile first, ApplicationUpdateManifestFile second ) =>
        first.Size == second.Size && string.Equals( first.Sha256, second.Sha256, StringComparison.Ordinal );

    private static string ResolveRelativePath( string root, string relativePath ) {
        var normalized = NormalizeRelativePath( relativePath );
        if(string.IsNullOrWhiteSpace( normalized ) || Path.IsPathRooted( relativePath ) ||
            normalized.Split( '/', StringSplitOptions.RemoveEmptyEntries ).Any( segment => segment is "." or ".." || segment.Contains( ':', StringComparison.Ordinal ) )) {
            throw new InvalidDataException( $"安全でない更新パスです: {relativePath}" );
        }

        var rootPath = Path.GetFullPath( root );
        var fullPath = Path.GetFullPath( Path.Combine( rootPath, normalized.Replace( '/', Path.DirectorySeparatorChar ) ) );
        if(!IsPathWithinRoot( rootPath, fullPath )) {
            throw new InvalidDataException( $"更新ルート外を指すパスです: {relativePath}" );
        }

        if(Directory.Exists( rootPath ) && File.GetAttributes( rootPath ).HasFlag( FileAttributes.ReparsePoint )) {
            throw new InvalidDataException( "更新ルートに再解析ポイントを使用できません。" );
        }

        var currentPath = rootPath;
        foreach(var segment in normalized.Split( '/', StringSplitOptions.RemoveEmptyEntries )) {
            currentPath = Path.Combine( currentPath, segment );
            if((File.Exists( currentPath ) || Directory.Exists( currentPath )) &&
                File.GetAttributes( currentPath ).HasFlag( FileAttributes.ReparsePoint )) {
                throw new InvalidDataException( $"更新パスに再解析ポイントが含まれています: {relativePath}" );
            }
        }
        return fullPath;
    }

    private static bool IsPathWithinRoot( string root, string path ) {
        var rootPath = Path.GetFullPath( root ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath( path );
        return fullPath.StartsWith( rootPath, StringComparison.OrdinalIgnoreCase );
    }

    private static string NormalizeRelativePath( string path ) =>
        path.Replace( '\\', '/' ).TrimStart( '/' ).TrimEnd( '/' );

    /// <summary>
    /// オーバーフロー時に最大値へ飽和させて転送量を加算する。
    /// </summary>
    private static long AddDownloadSize( long left, long right ) =>
        left < 0 || right < 0 || left > long.MaxValue - right
            ? long.MaxValue
            : left + right;

    private static void TryDeleteFile( string path ) {
        try {
            if(File.Exists( path )) {
                File.Delete( path );
            }
        }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }

    private static void TryDeleteDirectory( string path ) {
        try {
            if(Directory.Exists( path )) {
                Directory.Delete( path, true );
            }
        }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 個別ファイルの進捗を更新方式全体の進捗へ変換する。
    /// </summary>
    private sealed class AggregateProgress(
        IProgress<ApplicationUpdateProgress>? progress,
        long completedBytes,
        long totalBytes
    ) : IProgress<ApplicationUpdateProgress> {
        /// <inheritdoc />
        public void Report( ApplicationUpdateProgress value ) => progress?.Report( new ApplicationUpdateProgress(
            value.Phase,
            Math.Min( AddDownloadSize( completedBytes, value.DownloadedBytes ), totalBytes ),
            totalBytes ) );
    }
}