using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// ダウンロードと適用処理の実行を提供する。
/// </summary>
public sealed class DownloadWorkflowService(
    IApiService apiService,
    ILoggingService logger,
    IApplyWorkflowService applyWorkflowService
) : IDownloadWorkflowService {
    private static readonly ConcurrentDictionary<string, IPAddress> LastSuccessfulAddressCache = new();

    /// <inheritdoc/>
    public async Task<DownloadWorkflowResult> ExecuteDownloadAsync(
        DownloadExecutionRequest request,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        if(string.IsNullOrWhiteSpace( request.SaveRootPath )) {
            logger.Warn( "保存先ディレクトリが設定されていないため保存を中断する。" );
            return FailureDownloadResult( "保存先フォルダーが設定されていません" );
        }

        if(request.SelectedTab is null) {
            logger.Warn( "タブが選択されていないためダウンロードを中断する。" );
            return FailureDownloadResult( "タブが選択されていません" );
        }

        var checkedEntries = request.SelectedTab.GetCheckedEntries();
        var targetEntries = checkedEntries.Where( entry => !entry.IsDirectory ).ToList();
        if(targetEntries.Count == 0) {
            logger.Warn( "ダウンロード対象のファイルが存在しない。" );
            return FailureDownloadResult( "ダウンロード対象が有りません" );
        }
        logger.Info( $"ダウンロード対象を特定した。件数={targetEntries.Count}" );

        IReadOnlyList<string> paths = targetEntries.ConvertAll( entry => entry.Path );
        var pathResult = await apiService.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( paths, null ),
            cancellationToken
        );
        if(pathResult.IsFailed) {
            var reason = pathResult.Errors.Count > 0 ? pathResult.Errors[0].Message : null;
            var message = ResultNotificationPolicy.GetDownloadPathFailureMessage( pathResult.GetFirstErrorKind() );
            logger.Error( $"ダウンロードURLの取得に失敗した。Reason={reason}" );
            return FailureDownloadResult( message );
        }

        var downloadItems = pathResult.Value.Items.ToArray();
        logger.Info( $"ダウンロードURLを取得した。{downloadItems.Length}件" );

        if(downloadItems.Length == 0) {
            logger.Info( "ダウンロード対象が最新のため保存をスキップする。" );
            return SuccessDownloadResult( "対象ファイルは最新です" );
        }

        await this.DownloadFilesAsync(
            downloadItems,
            request.SaveRootPath,
            progressCallback,
            cancellationToken
        );

        return SuccessDownloadResult();
    }

    /// <inheritdoc/>
    public async Task<ApplyWorkflowResult> ExecuteApplyAsync(
        ApplyExecutionRequest request,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        if(request.SelectedTab is null) {
            logger.Warn( "タブが選択されていないため適用処理を中断する。" );
            return FailureApplyResult( "タブが選択されていません" );
        }

        var targetEntries = request.SelectedTab
            .GetCheckedViewModels()
            .Where( entry => !entry.IsDirectory )
            .Where( entry => entry.ChangeType is FileChangeType.Modified or FileChangeType.LocalOnly or FileChangeType.RepoOnly )
            .ToList();

        if(targetEntries.Count == 0) {
            logger.Warn( "適用対象のファイルが存在しない。" );
            return FailureApplyResult( "対象が有りません" );
        }
        logger.Info( $"適用対象を特定した。件数={targetEntries.Count}" );

        if(string.IsNullOrWhiteSpace( request.TranslateRootPath )) {
            logger.Warn( "翻訳ディレクトリが未設定のため処理を中断する。" );
            return FailureApplyResult( "翻訳ディレクトリを設定してください" );
        }

        var translateFullPath = Path.GetFullPath( request.TranslateRootPath );
        Directory.CreateDirectory( translateFullPath );
        var translateRootWithSeparator = translateFullPath.EndsWith( Path.DirectorySeparatorChar )
            ? translateFullPath
            : translateFullPath + Path.DirectorySeparatorChar;

        var resolvedTargets = new List<(IFileEntryViewModel Entry, string RootPath)>( targetEntries.Count );
        foreach(var entry in targetEntries) {
            var resolvedRoot = ResolveApplyRootPath( request, request.SelectedTab.TabType, entry.Path );
            if(resolvedRoot.IsFailed) {
                return FailureApplyResult( resolvedRoot.Message );
            }

            resolvedTargets.Add( (entry, resolvedRoot.RootPath) );
        }

        var groupedTargets = resolvedTargets
            .GroupBy( target => target.RootPath, StringComparer.OrdinalIgnoreCase )
            .ToArray();

        var processedCount = 0;
        foreach(var groupedTarget in groupedTargets) {
            cancellationToken.ThrowIfCancellationRequested();

            var rootFullPath = Path.GetFullPath( groupedTarget.Key );
            if(!EnsureApplyRootAvailable( request, request.SelectedTab.TabType, rootFullPath )) {
                logger.Warn( $"適用先ディレクトリが利用できない。Directory={rootFullPath}" );
                return FailureApplyResult( "適用先ディレクトリが存在しません" );
            }

            EnsureExternalArchivesAvailable( request, request.SelectedTab.TabType, rootFullPath, groupedTarget.Select( target => target.Entry ) );

            var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? rootFullPath
                : rootFullPath + Path.DirectorySeparatorChar;

            var groupEntries = groupedTarget.Select( target => target.Entry ).ToArray();
            var applyCompleted = await this.ApplyAsync(
                groupEntries,
                rootFullPath,
                rootWithSeparator,
                translateFullPath,
                translateRootWithSeparator,
                showSnackbarAsync,
                async value => {
                    var groupProgress = value / 100d;
                    var overallProgress = ( processedCount + (groupEntries.Length * groupProgress) ) / targetEntries.Count * 100d;
                    await progressCallback( overallProgress );
                },
                cancellationToken
            );

            if(!applyCompleted) {
                return new ApplyWorkflowResult( false, [] );
            }

            processedCount += groupEntries.Length;
        }

        await progressCallback( 100 );
        return SuccessApplyResult();
    }

    /// <inheritdoc/>
    public async Task DownloadFilesAsync(
        IReadOnlyList<ApiDownloadFilePathsItem> items,
        string saveRootPath,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        if(items.Count == 0) {
            return;
        }

        var successCount = 0;
        var failureCount = 0;
        var progressed = 0;
        var maxConcurrency = Math.Clamp( Environment.ProcessorCount, 2, 6 );
        using var semaphore = new SemaphoreSlim( maxConcurrency );
        using var httpClient = CreateHttpClient();
        List<Task> tasks = [];

        foreach(var item in items) {
            await semaphore.WaitAsync( cancellationToken );

            tasks.Add( Task.Run( async () => {
                try {
                    var filePath = Path.Combine( saveRootPath, item.Path );
                    await this.DownloadFileAsync( httpClient, item.Url, filePath, cancellationToken );
                    Interlocked.Increment( ref successCount );
                }
                catch {
                    Interlocked.Increment( ref failureCount );
                    throw;
                }
                finally {
                    var current = Interlocked.Increment( ref progressed );
                    await progressCallback( Math.Min( 100, (double)current / items.Count * 100 ) );
                    semaphore.Release();
                }
            }, cancellationToken ) );
        }

        try {
            await Task.WhenAll( tasks );
        }
        finally {
            logger.Info( $"ファイルのダウンロードが完了した。成功={successCount}/{items.Count} 件, 失敗={failureCount}/{items.Count} 件" );
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        return await applyWorkflowService.ApplyAsync(
            targetEntries,
            rootFullPath,
            rootWithSeparator,
            translateFullPath,
            translateRootWithSeparator,
            items => this.DownloadFilesAsync( items, translateFullPath, _ => Task.CompletedTask, cancellationToken ),
            showSnackbarAsync,
            progressCallback,
            cancellationToken
        );
    }

    /// <summary>
    /// 単一ファイルを保存する。
    /// </summary>
    /// <param name="httpClient">HTTP クライアント。</param>
    /// <param name="url">ダウンロードURL。</param>
    /// <param name="filePath">保存先パス。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    private async Task DownloadFileAsync( HttpClient httpClient, string url, string filePath, CancellationToken cancellationToken ) {
        logger.Info( $"ファイルをダウンロードする。Url={url}, FilePath={filePath}" );
        try {
            var bytes = await httpClient.GetByteArrayAsync( url, cancellationToken );
            var directoryName = Path.GetDirectoryName( filePath );
            if(!string.IsNullOrEmpty( directoryName ) && !Directory.Exists( directoryName )) {
                Directory.CreateDirectory( directoryName );
            }

            await File.WriteAllBytesAsync( filePath, bytes, cancellationToken );
        }
        catch(Exception ex) {
            logger.Error( $"ファイルのダウンロードに失敗した。Url={url}, FilePath={filePath}", ex );
            throw;
        }
        logger.Info( $"ファイルのダウンロードが完了した。Url={url}, FilePath={filePath}" );
    }

    /// <summary>
    /// 環境差異を吸収する HttpClient を生成する。
    /// </summary>
    /// <returns>初期化済みの HttpClient。</returns>
    private static HttpClient CreateHttpClient() {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds( 3 ),
            ConnectCallback = async ( context, token ) => {
                var host = context.DnsEndPoint!.Host;
                var port = context.DnsEndPoint.Port;
                var hostKey = host.ToLowerInvariant();

                IPAddress[] v4 = [];
                IPAddress[] v6 = [];
                try {
                    v4 = await Dns.GetHostAddressesAsync( host, AddressFamily.InterNetwork, token );
                }
                catch {
                    // IPv4 解決不可は許容する。
                }
                try {
                    v6 = await Dns.GetHostAddressesAsync( host, AddressFamily.InterNetworkV6, token );
                }
                catch {
                    // IPv6 解決不可は許容する。
                }

                var candidates = new List<IPAddress>( v4.Length + v6.Length + 1 );
                if(LastSuccessfulAddressCache.TryGetValue( hostKey, out var cached )) {
                    candidates.Add( cached );
                }
                candidates.AddRange( v4 );
                candidates.AddRange( v6 );

                foreach(var addr in candidates.Distinct()) {
                    var socket = new Socket( addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp ) { NoDelay = true };
                    try {
                        await socket.ConnectAsync( addr, port, token );
                        LastSuccessfulAddressCache[hostKey] = addr;
                        return new NetworkStream( socket, ownsSocket: true );
                    }
                    catch {
                        socket.Dispose();
                    }
                }

                LastSuccessfulAddressCache.TryRemove( hostKey, out _ );
                throw new SocketException( (int)SocketError.HostUnreachable );
            }
        };

        return new HttpClient( handler, disposeHandler: true );
    }

    /// <summary>
    /// ダウンロード失敗結果を生成する。
    /// </summary>
    /// <param name="message">通知メッセージ。</param>
    /// <returns>失敗結果。</returns>
    private static DownloadWorkflowResult FailureDownloadResult( string message ) =>
        new(
            false,
            [new WorkflowEvent( WorkflowEventKind.Notification, Message: message )]
        );

    /// <summary>
    /// ダウンロード成功結果を生成する。
    /// </summary>
    /// <param name="message">通知メッセージ。</param>
    /// <returns>成功結果。</returns>
    private static DownloadWorkflowResult SuccessDownloadResult( string? message = null ) =>
        string.IsNullOrWhiteSpace( message )
            ? new DownloadWorkflowResult( true, [] )
            : new DownloadWorkflowResult(
                true,
                [new WorkflowEvent( WorkflowEventKind.Notification, Message: message )]
            );

    /// <summary>
    /// 適用失敗結果を生成する。
    /// </summary>
    /// <param name="message">通知メッセージ。</param>
    /// <returns>失敗結果。</returns>
    private static ApplyWorkflowResult FailureApplyResult( string message ) =>
        new(
            false,
            [new WorkflowEvent( WorkflowEventKind.Notification, Message: message )]
        );

    /// <summary>
    /// 適用成功結果を生成する。
    /// </summary>
    /// <returns>成功結果。</returns>
    private static ApplyWorkflowResult SuccessApplyResult() =>
        new( true, [] );

    /// <summary>
    /// エントリーパスと設定から適用先ルートを解決する。
    /// </summary>
    /// <param name="request">適用要求。</param>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <param name="entryPath">翻訳エントリーパス。</param>
    /// <returns>解決結果。</returns>
    private ResolveApplyRootResult ResolveApplyRootPath( ApplyExecutionRequest request, CategoryType categoryType, string entryPath ) {
        switch(categoryType) {
            case CategoryType.Aircraft:
                return ResolveDcsWorldRootPath(
                    request.DcsWorldInstallDir,
                    request.UseExternalAircraftInjectionDir,
                    request.ExternalAircraftInjectionDir,
                    entryPath,
                    expectedCategorySegment: "aircraft" );
            case CategoryType.DlcCampaigns:
                return ResolveDcsWorldRootPath(
                    request.DcsWorldInstallDir,
                    request.UseExternalCampaignInjectionDir,
                    request.ExternalCampaignInjectionDir,
                    entryPath,
                    expectedCategorySegment: "campaigns" );
            case CategoryType.UserMissions:
                return string.IsNullOrWhiteSpace( request.SourceUserMissionDir )
                    ? ResolveApplyRootResult.Fail( "適用先ディレクトリを設定してください" )
                    : ResolveApplyRootResult.Success( request.SourceUserMissionDir );
            default:
                throw new InvalidOperationException( $"未対応のタブ種別: {categoryType}" );
        }
    }

    /// <summary>
    /// DCS World 配下カテゴリの適用先ルートを解決する。
    /// </summary>
    /// <param name="dcsWorldInstallDir">DCS World インストールフォルダー。</param>
    /// <param name="useExternalInjectionDir">外部保存を有効にするかどうか。</param>
    /// <param name="externalInjectionDir">外部保存フォルダー。</param>
    /// <param name="entryPath">翻訳エントリーパス。</param>
    /// <param name="expectedCategorySegment">カテゴリセグメント。</param>
    /// <returns>解決結果。</returns>
    private ResolveApplyRootResult ResolveDcsWorldRootPath(
        string? dcsWorldInstallDir,
        bool useExternalInjectionDir,
        string? externalInjectionDir,
        string entryPath,
        string expectedCategorySegment
    ) {
        var pathSegments = entryPath.Split( '/', StringSplitOptions.RemoveEmptyEntries );
        if(pathSegments.Length < 4 ||
            !string.Equals( pathSegments[0], "DCSWorld", StringComparison.OrdinalIgnoreCase ) ||
            !string.Equals( pathSegments[1], "Mods", StringComparison.OrdinalIgnoreCase ) ||
            !string.Equals( pathSegments[2], expectedCategorySegment, StringComparison.OrdinalIgnoreCase )) {
            logger.Warn( $"DCS World 配下エントリーのパス構造が不正である。Path={entryPath}, Category={expectedCategorySegment}" );
            return ResolveApplyRootResult.Fail( "適用先ディレクトリを設定してください" );
        }

        if(useExternalInjectionDir) {
            if(string.IsNullOrWhiteSpace( externalInjectionDir )) {
                return ResolveApplyRootResult.Fail( "適用先ディレクトリを設定してください" );
            }

            var packageName = pathSegments[3];
            var rootPath = Path.Combine(
                externalInjectionDir,
                $"{packageName}翻訳",
                "Mods",
                expectedCategorySegment );
            return ResolveApplyRootResult.Success( rootPath );
        }

        if(string.IsNullOrWhiteSpace( dcsWorldInstallDir )) {
            return ResolveApplyRootResult.Fail( "適用先ディレクトリを設定してください" );
        }

        return ResolveApplyRootResult.Success( Path.Combine( dcsWorldInstallDir, "Mods", expectedCategorySegment ) );
    }

    /// <summary>
    /// 適用先ルートを利用可能状態にする。
    /// </summary>
    /// <param name="request">適用要求。</param>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <param name="rootFullPath">適用先ルート。</param>
    /// <returns>利用可能なら <see langword="true"/>。</returns>
    private bool EnsureApplyRootAvailable( ApplyExecutionRequest request, CategoryType categoryType, string rootFullPath ) {
        var shouldCreateDirectory = categoryType switch
        {
            CategoryType.Aircraft => request.UseExternalAircraftInjectionDir,
            CategoryType.DlcCampaigns => request.UseExternalCampaignInjectionDir,
            CategoryType.UserMissions => false,
            _ => false,
        };

        if(Directory.Exists( rootFullPath )) {
            return true;
        }

        if(!shouldCreateDirectory) {
            return false;
        }

        Directory.CreateDirectory( rootFullPath );
        logger.Info( $"外部保存用の適用先ディレクトリを作成した。Directory={rootFullPath}" );
        return true;
    }

    /// <summary>
    /// 外部保存先で不足するアーカイブを DCS World から補完する。
    /// </summary>
    /// <param name="request">適用要求。</param>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <param name="rootFullPath">適用先ルート。</param>
    /// <param name="entries">対象エントリー。</param>
    private void EnsureExternalArchivesAvailable(
        ApplyExecutionRequest request,
        CategoryType categoryType,
        string rootFullPath,
        IEnumerable<IFileEntryViewModel> entries
    ) {
        var dcsWorldCategoryRoot = GetExternalArchiveSourceRootPath( request, categoryType );
        if(dcsWorldCategoryRoot is null) {
            return;
        }

        var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        foreach(var archiveRelativePath in EnumerateMissingArchiveRelativePaths( rootFullPath, rootWithSeparator, entries )) {
            var sourceArchivePath = Path.Combine( dcsWorldCategoryRoot, archiveRelativePath );
            if(!File.Exists( sourceArchivePath )) {
                logger.Warn( $"補完元のアーカイブが存在しないためコピーをスキップする。Source={sourceArchivePath}" );
                continue;
            }

            var destinationArchivePath = Path.Combine( rootFullPath, archiveRelativePath );
            var directoryPath = Path.GetDirectoryName( destinationArchivePath );
            if(!string.IsNullOrWhiteSpace( directoryPath )) {
                Directory.CreateDirectory( directoryPath );
            }

            File.Copy( sourceArchivePath, destinationArchivePath, overwrite: false );
            logger.Info( $"DCS World から外部保存先へアーカイブを補完した。Source={sourceArchivePath}, Destination={destinationArchivePath}" );
        }
    }

    /// <summary>
    /// 外部保存先のアーカイブ補完元ルートを取得する。
    /// </summary>
    /// <param name="request">適用要求。</param>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <returns>補完元ルート。対象外の場合は <see langword="null"/>。</returns>
    private static string? GetExternalArchiveSourceRootPath( ApplyExecutionRequest request, CategoryType categoryType ) {
        if(string.IsNullOrWhiteSpace( request.DcsWorldInstallDir )) {
            return null;
        }

        var dcsWorldInstallDir = Path.GetFullPath( request.DcsWorldInstallDir );
        return categoryType switch
        {
            CategoryType.Aircraft when request.UseExternalAircraftInjectionDir => Path.Combine( dcsWorldInstallDir, "Mods", "aircraft" ),
            CategoryType.DlcCampaigns when request.UseExternalCampaignInjectionDir => Path.Combine( dcsWorldInstallDir, "Mods", "campaigns" ),
            _ => null,
        };
    }

    /// <summary>
    /// 外部保存先に存在しないアーカイブの相対パスを列挙する。
    /// </summary>
    /// <param name="rootFullPath">適用先ルート。</param>
    /// <param name="rootWithSeparator">区切り文字付き適用先ルート。</param>
    /// <param name="entries">対象エントリー。</param>
    /// <returns>存在しないアーカイブの相対パス列。</returns>
    private IEnumerable<string> EnumerateMissingArchiveRelativePaths(
        string rootFullPath,
        string rootWithSeparator,
        IEnumerable<IFileEntryViewModel> entries
    ) {
        return entries
            .Select( entry => TryGetArchiveRelativePath( entry.Path ) )
            .Where( static relativePath => !string.IsNullOrWhiteSpace( relativePath ) )
            .Distinct( StringComparer.OrdinalIgnoreCase )
            .Where( relativePath =>
                TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, relativePath!, out var archivePath ) &&
                !File.Exists( archivePath ) )
            .Cast<string>();
    }

    /// <summary>
    /// 翻訳エントリーからアーカイブ実体の相対パスを取得する。
    /// </summary>
    /// <param name="entryPath">翻訳エントリーパス。</param>
    /// <returns>アーカイブ実体の相対パス。該当しない場合は <see langword="null"/>。</returns>
    private string? TryGetArchiveRelativePath( string entryPath ) {
        var parts = entryPath.Split( '/', StringSplitOptions.RemoveEmptyEntries );
        var archiveIndex = Array.FindIndex(
            parts,
            static segment =>
                segment.EndsWith( ".miz", StringComparison.OrdinalIgnoreCase ) ||
                segment.EndsWith( ".trk", StringComparison.OrdinalIgnoreCase ) );
        if(archiveIndex == -1) {
            return null;
        }

        var rootSkipCount = GetRootSegmentSkipCount( parts );
        var archiveSegments = parts.Take( archiveIndex + 1 ).Skip( rootSkipCount ).ToArray();
        if(archiveSegments.Length == 0) {
            logger.Warn( $"アーカイブ補完対象エントリーのパス構造が不正である。Path={entryPath}" );
            return null;
        }

        return Path.Combine( archiveSegments );
    }

    /// <summary>
    /// 相対パスがルート配下に収まる場合のみフルパスへ解決する。
    /// </summary>
    /// <param name="rootFullPath">ルートのフルパス。</param>
    /// <param name="rootWithSeparator">区切り文字付きルート。</param>
    /// <param name="relativePath">相対パス。</param>
    /// <param name="resolvedPath">解決後のフルパス。</param>
    /// <returns>ルート配下に収まる場合は <see langword="true"/>。</returns>
    private static bool TryResolvePathWithinRoot(
        string rootFullPath,
        string rootWithSeparator,
        string relativePath,
        out string resolvedPath
    ) {
        resolvedPath = string.Empty;
        if(string.IsNullOrWhiteSpace( relativePath )) {
            return false;
        }

        var candidate = Path.GetFullPath( Path.Combine( rootFullPath, relativePath ) );
        if(!candidate.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    /// <summary>
    /// エントリーパスから適用先ルートをスキップするセグメント数を取得する。
    /// </summary>
    /// <param name="segments">分解済みパスセグメント。</param>
    /// <returns>スキップするセグメント数。</returns>
    private static int GetRootSegmentSkipCount( string[] segments ) {
        if(segments.Length == 0) {
            return 0;
        }

        if(string.Equals( segments[0], "DCSWorld", StringComparison.OrdinalIgnoreCase ) &&
            segments.Length >= 3 &&
            string.Equals( segments[1], "Mods", StringComparison.OrdinalIgnoreCase )) {
            return 3;
        }

        if(string.Equals( segments[0], "UserMissions", StringComparison.OrdinalIgnoreCase )) {
            return 1;
        }

        return 0;
    }

    /// <summary>
    /// 適用先ルート解決結果を表す。
    /// </summary>
    /// <param name="IsFailed">解決失敗かどうか。</param>
    /// <param name="RootPath">解決したルート。</param>
    /// <param name="Message">失敗メッセージ。</param>
    private sealed record ResolveApplyRootResult( bool IsFailed, string RootPath, string Message ) {
        /// <summary>
        /// 成功結果を生成する。
        /// </summary>
        /// <param name="rootPath">解決したルート。</param>
        /// <returns>成功結果。</returns>
        public static ResolveApplyRootResult Success( string rootPath ) =>
            new( false, rootPath, string.Empty );

        /// <summary>
        /// 失敗結果を生成する。
        /// </summary>
        /// <param name="message">失敗メッセージ。</param>
        /// <returns>失敗結果。</returns>
        public static ResolveApplyRootResult Fail( string message ) =>
            new( true, string.Empty, message );
    }
}