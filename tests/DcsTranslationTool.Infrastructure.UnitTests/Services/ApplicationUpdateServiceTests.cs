using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.TestCommon.Http;
using DcsTranslationTool.TestCommon.IO;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

/// <summary>
/// <see cref="ApplicationUpdateService"/> の取得とステージングを検証する。
/// </summary>
public sealed class ApplicationUpdateServiceTests {
    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web );

    [Fact]
    public async Task ApplyPreparedUpdateAsyncは管理ファイルだけを置換して利用者データを保持する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncは管理ファイルだけを置換して利用者データを保持する ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        WriteApplication( applicationDirectory, "1.3.1", "old", ("removed.dll", "remove") );
        WriteApplication( stagingDirectory, "1.4.0", "new", ("added.dll", "add") );
        Directory.CreateDirectory( Path.Combine( applicationDirectory, "Logs" ) );
        File.WriteAllText( Path.Combine( applicationDirectory, "Logs", "app.log" ), "log" );
        Directory.CreateDirectory( Path.Combine( applicationDirectory, "TranslateFile" ) );
        File.WriteAllText( Path.Combine( applicationDirectory, "TranslateFile", "user.lua" ), "user" );
        File.WriteAllText( Path.Combine( applicationDirectory, "unknown.txt" ), "unknown" );
        var requestPath = WriteApplyRequest( workspaceDirectory, applicationDirectory, stagingDirectory );
        var processLauncher = new Mock<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>();
        processLauncher.Setup( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ) ).Returns( new Process() );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            processLauncher.Object,
            applicationDirectory: applicationDirectory,
            updatesDirectory: temporaryDirectory.GetPath( "updates" ) );

        var result = await service.ApplyPreparedUpdateAsync( requestPath, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( "new", File.ReadAllText( Path.Combine( applicationDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.True( File.Exists( Path.Combine( applicationDirectory, "added.dll" ) ) );
        Assert.False( File.Exists( Path.Combine( applicationDirectory, "removed.dll" ) ) );
        Assert.Equal( "log", File.ReadAllText( Path.Combine( applicationDirectory, "Logs", "app.log" ) ) );
        Assert.Equal( "user", File.ReadAllText( Path.Combine( applicationDirectory, "TranslateFile", "user.lua" ) ) );
        Assert.Equal( "unknown", File.ReadAllText( Path.Combine( applicationDirectory, "unknown.txt" ) ) );
        Assert.False( Directory.Exists( stagingDirectory ) );
        Assert.False( Directory.Exists( Path.Combine( workspaceDirectory, "backup" ) ) );
        Assert.False( File.Exists( requestPath ) );
    }

    [Fact]
    public async Task ApplyPreparedUpdateAsyncは新管理パスと未知ファイルの衝突時に現行版を維持する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncは新管理パスと未知ファイルの衝突時に現行版を維持する ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        WriteApplication( stagingDirectory, "1.4.0", "new", ("added.dll", "managed") );
        File.WriteAllText( Path.Combine( applicationDirectory, "added.dll" ), "user-owned" );
        var requestPath = WriteApplyRequest( workspaceDirectory, applicationDirectory, stagingDirectory );
        var processLauncher = new Mock<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>();
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            processLauncher.Object,
            applicationDirectory: applicationDirectory,
            updatesDirectory: temporaryDirectory.GetPath( "updates" ) );

        var result = await service.ApplyPreparedUpdateAsync( requestPath, TestContext.Current.CancellationToken );

        Assert.True( result.IsFailed );
        Assert.Equal( "old", File.ReadAllText( Path.Combine( applicationDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.Equal( "user-owned", File.ReadAllText( Path.Combine( applicationDirectory, "added.dll" ) ) );
        processLauncher.Verify( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ), Times.Never );
    }

    [Fact]
    public async Task ApplyPreparedUpdateAsyncは再起動失敗時に旧管理ファイルへロールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncは再起動失敗時に旧管理ファイルへロールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        WriteApplication( applicationDirectory, "1.3.1", "old", ("removed.dll", "old library") );
        WriteApplication( stagingDirectory, "1.4.0", "new", ("added.dll", "new library") );
        var requestPath = WriteApplyRequest( workspaceDirectory, applicationDirectory, stagingDirectory );
        var processLauncher = new Mock<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>();
        processLauncher.Setup( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ) ).Returns( (Process?)null );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            processLauncher.Object,
            applicationDirectory: applicationDirectory,
            updatesDirectory: temporaryDirectory.GetPath( "updates" ) );

        var result = await service.ApplyPreparedUpdateAsync( requestPath, TestContext.Current.CancellationToken );

        Assert.True( result.IsFailed );
        Assert.Equal( "old", File.ReadAllText( Path.Combine( applicationDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.Equal( "old library", File.ReadAllText( Path.Combine( applicationDirectory, "removed.dll" ) ) );
        Assert.False( File.Exists( Path.Combine( applicationDirectory, "added.dll" ) ) );
        var manifest = JsonSerializer.Deserialize<ApplicationUpdateManifest>(
            File.ReadAllText( Path.Combine( applicationDirectory, "update-manifest.json" ) ),
            JsonOptions );
        Assert.Equal( "1.3.1", manifest!.Version );
    }

    [Fact]
    public async Task ApplyPreparedUpdateAsyncは置換後の検証失敗時に旧管理ファイルへロールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncは置換後の検証失敗時に旧管理ファイルへロールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        WriteApplication( applicationDirectory, "1.3.1", "old", ("removed.dll", "old library") );
        WriteApplication( stagingDirectory, "1.4.0", "new", ("payload.bin", new string( 'x', 32 * 1024 * 1024 )) );
        var requestPath = WriteApplyRequest( workspaceDirectory, applicationDirectory, stagingDirectory );
        var corruptionCompleted = new TaskCompletionSource( TaskCreationOptions.RunContinuationsAsynchronously );
        using var watcher = new FileSystemWatcher( applicationDirectory, "DCS-Translation-Tool.exe" )
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
        };
        watcher.Changed += ( _, _ ) => {
            watcher.EnableRaisingEvents = false;
            File.WriteAllText( Path.Combine( applicationDirectory, "DCS-Translation-Tool.exe" ), "corrupt" );
            corruptionCompleted.TrySetResult();
        };
        watcher.EnableRaisingEvents = true;
        var processLauncher = new Mock<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>();
        processLauncher.Setup( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ) ).Returns( new Process() );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            processLauncher.Object,
            applicationDirectory: applicationDirectory,
            updatesDirectory: temporaryDirectory.GetPath( "updates" ) );

        var result = await service.ApplyPreparedUpdateAsync( requestPath, TestContext.Current.CancellationToken );
        await corruptionCompleted.Task.WaitAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsFailed );
        Assert.Equal( "old", File.ReadAllText( Path.Combine( applicationDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.Equal( "old library", File.ReadAllText( Path.Combine( applicationDirectory, "removed.dll" ) ) );
        Assert.False( File.Exists( Path.Combine( applicationDirectory, "payload.bin" ) ) );
        processLauncher.Verify( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ), Times.Never );
    }

    [Fact]
    public void GetPendingUpdateは更新作業ルート外の保留情報を拒否する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( GetPendingUpdateは更新作業ルート外の保留情報を拒否する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var outsideWorkspace = temporaryDirectory.GetPath( "outside" );
        var outsideStaging = Path.Combine( outsideWorkspace, "staging" );
        Directory.CreateDirectory( updatesDirectory );
        Directory.CreateDirectory( outsideStaging );
        var pending = new ApplicationUpdateStageResult(
            outsideWorkspace,
            outsideStaging,
            new Version( 1, 4, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0" );
        File.WriteAllText(
            Path.Combine( updatesDirectory, "pending-update.json" ),
            JsonSerializer.Serialize( pending, JsonOptions ) );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.GetPendingUpdate();

        Assert.True( result.IsFailed );
    }

    /// <summary>
    /// ファイルバージョン由来のリビジョン付きバージョンでも保留中の更新を復元できることを確認する。
    /// </summary>
    [Fact]
    public void GetPendingUpdateはリビジョン付き対象バージョンを受け入れる() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( GetPendingUpdateはリビジョン付き対象バージョンを受け入れる ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var workspace = Path.Combine( updatesDirectory, "1.4.0-win-x64-11111111111111111111111111111111" );
        var staging = Path.Combine( workspace, "staging" );
        Directory.CreateDirectory( staging );
        var pending = new ApplicationUpdateStageResult(
            workspace,
            staging,
            new Version( 1, 4, 0, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0" );
        File.WriteAllText(
            Path.Combine( updatesDirectory, "pending-update.json" ),
            JsonSerializer.Serialize( pending, JsonOptions ) );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.GetPendingUpdate();

        Assert.True( result.IsSuccess );
        Assert.NotNull( result.Value );
    }

    [Fact]
    public void LaunchApplyProcessは命名規則外のrunnerを削除しない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( LaunchApplyProcessは命名規則外のrunnerを削除しない ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var outsideWorkspace = Path.Combine( updatesDirectory, "unmanaged" );
        var outsideStaging = Path.Combine( outsideWorkspace, "staging" );
        var outsideRunner = Path.Combine( outsideWorkspace, "runner" );
        Directory.CreateDirectory( outsideStaging );
        Directory.CreateDirectory( outsideRunner );
        var sentinelPath = Path.Combine( outsideRunner, "sentinel.txt" );
        File.WriteAllText( sentinelPath, "keep" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var stageResult = new ApplicationUpdateStageResult(
            outsideWorkspace,
            outsideStaging,
            new Version( 1, 4, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0" );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: applicationDirectory,
            updatesDirectory: updatesDirectory );

        var result = service.LaunchApplyProcess( stageResult, Environment.ProcessId );

        Assert.True( result.IsFailed );
        Assert.Equal( "keep", File.ReadAllText( sentinelPath ) );
    }

    [Fact]
    public void CleanupCompletedUpdatesは保留中の作業領域を保持して完了済み領域だけを削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedUpdatesは保留中の作業領域を保持して完了済み領域だけを削除する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var pendingWorkspace = Path.Combine( updatesDirectory, "1.4.0-win-x64-11111111111111111111111111111111" );
        var pendingStaging = Path.Combine( pendingWorkspace, "staging" );
        var completedWorkspace = Path.Combine( updatesDirectory, "1.3.1-win-x64-22222222222222222222222222222222" );
        Directory.CreateDirectory( pendingStaging );
        Directory.CreateDirectory( completedWorkspace );
        File.WriteAllText( Path.Combine( completedWorkspace, "runner.log" ), "old" );
        var pending = new ApplicationUpdateStageResult(
            pendingWorkspace,
            pendingStaging,
            new Version( 1, 4, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0" );
        File.WriteAllText(
            Path.Combine( updatesDirectory, "pending-update.json" ),
            JsonSerializer.Serialize( pending, JsonOptions ) );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.CleanupCompletedUpdates();

        Assert.True( result.IsSuccess );
        Assert.True( Directory.Exists( pendingWorkspace ) );
        Assert.False( Directory.Exists( completedWorkspace ) );
    }

    [Fact]
    public async Task DownloadAndStageAsyncは完全版を二重検証して保留状態へ保存する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは完全版を二重検証して保留状態へ保存する ) );
        var packageBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64" );
        const string packageName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var packageHash = CalculateSha256( packageBytes );
        var sidecarBytes = Encoding.UTF8.GetBytes( $"{packageHash}  {packageName}\r\n" );
        var httpClient = TestHttpClientFactory.Create( (request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath.EndsWith( ".sha256", StringComparison.Ordinal )
                ? CreateResponse( sidecarBytes, "text/plain" )
                : CreateResponse( packageBytes, "application/zip" ) ), "https://github.com/" );
        var asset = CreateAsset( packageName, packageBytes, sidecarBytes, ApplicationUpdateAssetKind.Full );
        var update = new UpdateCheckResult(
            true,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "v1.4.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
            "win-x64",
            true,
            asset,
            null );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            temporaryDirectory.GetPath( "application" ),
            temporaryDirectory.GetPath( "updates" ) );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.Value.AssetKind );
        Assert.True( File.Exists( Path.Combine( result.Value.StagingDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.True( File.Exists( temporaryDirectory.GetPath( "updates/pending-update.json" ) ) );
    }

    [Fact]
    public async Task DownloadAndStageAsyncは差分検証失敗時に完全版へフォールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは差分検証失敗時に完全版へフォールバックする ) );
        var packageBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64" );
        const string fullName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( packageBytes )}  {fullName}\r\n" );
        var deltaBytes = Encoding.UTF8.GetBytes( "not a zip" );
        const string deltaName = "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            var bytes = name switch
            {
                var value when value == $"{deltaName}.sha256" => deltaSidecar,
                var value when value == deltaName => deltaBytes,
                var value when value == $"{fullName}.sha256" => fullSidecar,
                _ => packageBytes
            };
            return Task.FromResult( CreateResponse( bytes, name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var fullAsset = CreateAsset( fullName, packageBytes, fullSidecar, ApplicationUpdateAssetKind.Full );
        var deltaAsset = CreateAsset( deltaName, deltaBytes, deltaSidecar, ApplicationUpdateAssetKind.Delta );
        var update = new UpdateCheckResult(
            true,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "v1.4.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
            "win-x64",
            true,
            fullAsset,
            deltaAsset );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            temporaryDirectory.GetPath( "application" ),
            temporaryDirectory.GetPath( "updates" ) );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.Value.AssetKind );
    }

    [Fact]
    public async Task DownloadAndStageAsyncは必須項目がnullの差分から完全版へフォールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは必須項目がnullの差分から完全版へフォールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var packageBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64" );
        const string fullName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( packageBytes )}  {fullName}\r\n" );
        var deltaBytes = CreateMalformedDeltaPackageBytes( temporaryDirectory );
        const string deltaName = "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            var bytes = name switch
            {
                var value when value == $"{deltaName}.sha256" => deltaSidecar,
                var value when value == deltaName => deltaBytes,
                var value when value == $"{fullName}.sha256" => fullSidecar,
                _ => packageBytes
            };
            return Task.FromResult( CreateResponse( bytes, name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var fullAsset = CreateAsset( fullName, packageBytes, fullSidecar, ApplicationUpdateAssetKind.Full );
        var deltaAsset = CreateAsset( deltaName, deltaBytes, deltaSidecar, ApplicationUpdateAssetKind.Delta );
        var update = new UpdateCheckResult(
            true,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "v1.4.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
            "win-x64",
            true,
            fullAsset,
            deltaAsset );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            temporaryDirectory.GetPath( "updates" ) );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.Value.AssetKind );
    }

    private static byte[] CreateFullPackageBytes( TemporaryDirectory temporaryDirectory, string version, string rid ) {
        var packageRoot = temporaryDirectory.GetPath( $"package-{Guid.NewGuid():N}" );
        Directory.CreateDirectory( packageRoot );
        var executablePath = Path.Combine( packageRoot, "DCS-Translation-Tool.exe" );
        File.WriteAllText( executablePath, "executable" );
        var executableBytes = File.ReadAllBytes( executablePath );
        var manifest = new ApplicationUpdateManifest(
            1,
            "DcsTranslationTool",
            version,
            rid,
            "DCS-Translation-Tool.exe",
            [new ApplicationUpdateManifestFile( "DCS-Translation-Tool.exe", executableBytes.LongLength, CalculateSha256( executableBytes ) )] );
        File.WriteAllText(
            Path.Combine( packageRoot, "update-manifest.json" ),
            JsonSerializer.Serialize( manifest, JsonOptions ) );
        var zipPath = temporaryDirectory.GetPath( $"{Guid.NewGuid():N}.zip" );
        ZipFile.CreateFromDirectory( packageRoot, zipPath );
        return File.ReadAllBytes( zipPath );
    }

    private static byte[] CreateMalformedDeltaPackageBytes( TemporaryDirectory temporaryDirectory ) {
        var packageRoot = temporaryDirectory.GetPath( $"malformed-delta-{Guid.NewGuid():N}" );
        Directory.CreateDirectory( packageRoot );
        File.WriteAllText(
            Path.Combine( packageRoot, ApplicationUpdatePackageService.DeltaManifestFileName ),
            """
            {
              "schemaVersion": 1,
              "applicationId": "DcsTranslationTool",
              "baseVersion": "1.3.1",
              "targetVersion": "1.4.0",
              "runtimeIdentifier": "win-x64",
              "addedOrChangedFiles": null,
              "removedFiles": [],
              "targetManifest": null
            }
            """ );
        var zipPath = temporaryDirectory.GetPath( $"{Guid.NewGuid():N}.delta.zip" );
        ZipFile.CreateFromDirectory( packageRoot, zipPath );
        return File.ReadAllBytes( zipPath );
    }

    private static void WriteApplication( string directory, string version, string executableContent, params (string Path, string Content)[] files ) {
        Directory.CreateDirectory( directory );
        File.WriteAllText( Path.Combine( directory, "DCS-Translation-Tool.exe" ), executableContent );
        foreach(var file in files) {
            var path = Path.Combine( directory, file.Path );
            Directory.CreateDirectory( Path.GetDirectoryName( path )! );
            File.WriteAllText( path, file.Content );
        }

        var manifestFiles = Directory.GetFiles( directory, "*", SearchOption.AllDirectories )
            .Select( path => new ApplicationUpdateManifestFile(
                Path.GetRelativePath( directory, path ).Replace( '\\', '/' ),
                new FileInfo( path ).Length,
                CalculateSha256( File.ReadAllBytes( path ) ) ) )
            .ToArray();
        var manifest = new ApplicationUpdateManifest( 1, "DcsTranslationTool", version, "win-x64", "DCS-Translation-Tool.exe", manifestFiles );
        File.WriteAllText( Path.Combine( directory, "update-manifest.json" ), JsonSerializer.Serialize( manifest, JsonOptions ) );
    }

    private static string WriteApplyRequest( string workspaceDirectory, string applicationDirectory, string stagingDirectory ) {
        Directory.CreateDirectory( workspaceDirectory );
        var request = new ApplicationUpdateApplyRequest(
            applicationDirectory,
            stagingDirectory,
            Path.Combine( workspaceDirectory, "backup" ),
            0,
            "win-x64",
            "1.4.0" );
        var requestPath = Path.Combine( workspaceDirectory, "apply-request.json" );
        File.WriteAllText( requestPath, JsonSerializer.Serialize( request, JsonOptions ) );
        return requestPath;
    }

    private static ApplicationUpdateAsset CreateAsset(
        string name,
        byte[] packageBytes,
        byte[] sidecarBytes,
        ApplicationUpdateAssetKind kind
    ) => new(
        name,
        $"https://github.com/5kdn/DCS-Translation-Tool/releases/download/v1.4.0/{name}",
        packageBytes.LongLength,
        $"sha256:{CalculateSha256( packageBytes )}",
        kind,
        kind == ApplicationUpdateAssetKind.Delta ? new Version( 1, 3, 1 ) : null,
        new Version( 1, 4, 0 ),
        new ApplicationUpdateChecksumAsset(
            $"{name}.sha256",
            $"https://github.com/5kdn/DCS-Translation-Tool/releases/download/v1.4.0/{name}.sha256",
            sidecarBytes.LongLength,
            $"sha256:{CalculateSha256( sidecarBytes )}" ) );

    private static HttpResponseMessage CreateResponse( byte[] bytes, string mediaType ) => new( HttpStatusCode.OK )
    {
        Content = new ByteArrayContent( bytes )
        {
            Headers =
            {
                ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue( mediaType ),
                ContentLength = bytes.LongLength
            }
        }
    };

    private static string CalculateSha256( byte[] bytes ) =>
        Convert.ToHexString( SHA256.HashData( bytes ) ).ToLowerInvariant();
}