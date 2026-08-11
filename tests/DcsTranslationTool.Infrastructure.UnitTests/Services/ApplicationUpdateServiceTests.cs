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
    public async Task ApplyPreparedUpdateAsyncは同一内容の管理ファイルを置換せず適用する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncは同一内容の管理ファイルを置換せず適用する ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        const string sharedRuntimePath = "lib/PenImc_cor3.dll";
        WriteApplication( applicationDirectory, "1.3.1", "old", (sharedRuntimePath, "shared runtime") );
        WriteApplication( stagingDirectory, "1.4.0", "new", (sharedRuntimePath, "shared runtime") );
        var installedRuntimePath = Path.Combine( applicationDirectory, sharedRuntimePath.Replace( '/', Path.DirectorySeparatorChar ) );
        using var lockedRuntime = new FileStream( installedRuntimePath, FileMode.Open, FileAccess.Read, FileShare.Read );
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
        Assert.Equal( "shared runtime", File.ReadAllText( installedRuntimePath ) );
        processLauncher.Verify( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ), Times.Once );
    }

    [Fact]
    public async Task ApplyPreparedUpdateAsyncはロック済みの同一内容ファイルを残してロールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncはロック済みの同一内容ファイルを残してロールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        const string sharedRuntimePath = "lib/PenImc_cor3.dll";
        WriteApplication( applicationDirectory, "1.3.1", "old", (sharedRuntimePath, "shared runtime") );
        WriteApplication( stagingDirectory, "1.4.0", "new", (sharedRuntimePath, "shared runtime") );
        var installedRuntimePath = Path.Combine( applicationDirectory, sharedRuntimePath.Replace( '/', Path.DirectorySeparatorChar ) );
        using var lockedRuntime = new FileStream( installedRuntimePath, FileMode.Open, FileAccess.Read, FileShare.Read );
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
        Assert.Equal( "shared runtime", File.ReadAllText( installedRuntimePath ) );
        var manifest = JsonSerializer.Deserialize<ApplicationUpdateManifest>(
            File.ReadAllText( Path.Combine( applicationDirectory, "update-manifest.json" ) ),
            JsonOptions );
        Assert.Equal( "1.3.1", manifest!.Version );
    }

    [Fact]
    public async Task ApplyPreparedUpdateAsyncは基準バージョンと異なる場合に適用しない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ApplyPreparedUpdateAsyncは基準バージョンと異なる場合に適用しない ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var workspaceDirectory = temporaryDirectory.GetPath( "updates/1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspaceDirectory, "staging" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        WriteApplication( stagingDirectory, "1.4.0", "new" );
        var requestPath = WriteApplyRequest( workspaceDirectory, applicationDirectory, stagingDirectory, "1.3.0" );
        var processLauncher = new Mock<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>();
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            processLauncher.Object,
            applicationDirectory: applicationDirectory,
            updatesDirectory: temporaryDirectory.GetPath( "updates" ) );

        var result = await service.ApplyPreparedUpdateAsync( requestPath, TestContext.Current.CancellationToken );

        Assert.True( result.IsFailed );
        Assert.Contains( result.Errors, error => error.Metadata.ContainsValue( "UPDATE_APPLY_BASE_VERSION_MISMATCH" ) );
        Assert.Equal( "old", File.ReadAllText( Path.Combine( applicationDirectory, "DCS-Translation-Tool.exe" ) ) );
        processLauncher.Verify( launcher => launcher.Start( It.IsAny<ProcessStartInfo>() ), Times.Never );
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
    public void DiscardStagedUpdateはステージ済み作業領域を削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DiscardStagedUpdateはステージ済み作業領域を削除する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var workspace = Path.Combine( updatesDirectory, "1.4.0-win-x64-11111111111111111111111111111111" );
        var staging = Path.Combine( workspace, "staging" );
        Directory.CreateDirectory( staging );
        var stageResult = new ApplicationUpdateStageResult(
            workspace,
            staging,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0" );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.DiscardStagedUpdate( stageResult );

        Assert.True( result.IsSuccess );
        Assert.False( Directory.Exists( workspace ) );
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
            new Version( 1, 3, 1 ),
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
    public void CleanupCompletedUpdatesはキャッシュを保持して作業領域を削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedUpdatesはキャッシュを保持して作業領域を削除する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var stagedWorkspace = Path.Combine( updatesDirectory, "1.4.0-win-x64-11111111111111111111111111111111" );
        var stagedStaging = Path.Combine( stagedWorkspace, "staging" );
        var completedWorkspace = Path.Combine( updatesDirectory, "1.3.1-win-x64-22222222222222222222222222222222" );
        var cachePath = Path.Combine( updatesDirectory, "cache", "cached.zip" );
        Directory.CreateDirectory( stagedStaging );
        Directory.CreateDirectory( completedWorkspace );
        Directory.CreateDirectory( Path.GetDirectoryName( cachePath )! );
        File.WriteAllText( cachePath, "cache" );
        File.WriteAllText( Path.Combine( completedWorkspace, "runner.log" ), "old" );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.CleanupCompletedUpdates();

        Assert.True( result.IsSuccess );
        Assert.True( File.Exists( cachePath ) );
        Assert.False( Directory.Exists( stagedWorkspace ) );
        Assert.False( Directory.Exists( completedWorkspace ) );
    }

    /// <summary>
    /// 別プロセスが使用中の更新作業領域をクリーンアップしないことを確認する。
    /// </summary>
    [Fact]
    public void CleanupCompletedUpdatesは使用中の作業領域を削除しない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedUpdatesは使用中の作業領域を削除しない ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var workspace = Path.Combine( updatesDirectory, "1.4.0-win-x64-11111111111111111111111111111111" );
        var stagingDirectory = Path.Combine( workspace, "staging" );
        Directory.CreateDirectory( stagingDirectory );
        var leasePath = Path.Combine( workspace, ".in-use" );
        using var lease = new FileStream( leasePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.CleanupCompletedUpdates();

        Assert.True( result.IsSuccess );
        Assert.True( Directory.Exists( workspace ) );
    }

    /// <summary>
    /// 差分キャッシュの期限切れアセットと未完了一時ファイルを削除することを確認する。
    /// </summary>
    [Fact]
    public void CleanupCompletedUpdatesは期限切れの差分キャッシュを削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedUpdatesは期限切れの差分キャッシュを削除する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var cacheDirectory = Path.Combine( updatesDirectory, "cache" );
        var expiredAssetPath = Path.Combine( cacheDirectory, $"{new string( 'a', 64 )}.zip" );
        var retainedAssetPath = Path.Combine( cacheDirectory, $"{new string( 'b', 64 )}.zip" );
        var expiredPartialPath = Path.Combine( cacheDirectory, $"{new string( 'c', 64 )}.zip.incomplete.partial" );
        Directory.CreateDirectory( cacheDirectory );
        File.WriteAllText( expiredAssetPath, "expired" );
        File.WriteAllText( retainedAssetPath, "retained" );
        File.WriteAllText( expiredPartialPath, "partial" );
        File.SetLastWriteTimeUtc( expiredAssetPath, DateTime.UtcNow.AddDays( -31 ) );
        File.SetLastWriteTimeUtc( retainedAssetPath, DateTime.UtcNow.AddDays( -29 ) );
        File.SetLastWriteTimeUtc( expiredPartialPath, DateTime.UtcNow.AddDays( -31 ) );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            applicationDirectory: temporaryDirectory.GetPath( "application" ),
            updatesDirectory: updatesDirectory );

        var result = service.CleanupCompletedUpdates();

        Assert.True( result.IsSuccess );
        Assert.False( File.Exists( expiredAssetPath ) );
        Assert.True( File.Exists( retainedAssetPath ) );
        Assert.False( File.Exists( expiredPartialPath ) );
    }

    [Fact]
    public async Task DownloadAndStageAsyncは完全版を二重検証して保留状態を保存する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは完全版を二重検証して保留状態を保存する ) );
        WriteApplication( temporaryDirectory.GetPath( "application" ), "1.3.1", "old" );
        var packageBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
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
            [] );
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
        Assert.Equal( new Version( 1, 3, 1 ), result.Value.BaseVersion );
        var pendingResult = service.GetPendingUpdate();
        Assert.True( pendingResult.IsSuccess, string.Join( "; ", pendingResult.Errors.Select( error => error.Message ) ) );
        Assert.Equal( result.Value, pendingResult.Value );
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    /// <summary>
    /// 検証済み差分キャッシュを再利用するときも sidecar を再検証することを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncは一致する差分キャッシュを再利用してsidecarを再検証する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは一致する差分キャッシュを再利用してsidecarを再検証する ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var deltaBytes = CreateDeltaPackageBytes( temporaryDirectory, "1.3.1", "1.4.0", "new" );
        const string deltaName = "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var fullBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
        const string fullName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullBytes )}  {fullName}\r\n" );
        var deltaAsset = CreateAsset( deltaName, deltaBytes, deltaSidecar, ApplicationUpdateAssetKind.Delta );
        var fullAsset = CreateAsset( fullName, fullBytes, fullSidecar, ApplicationUpdateAssetKind.Full );
        var cachePath = Path.Combine( updatesDirectory, "cache", $"{CalculateSha256( deltaBytes )}.zip" );
        Directory.CreateDirectory( Path.GetDirectoryName( cachePath )! );
        File.WriteAllBytes( cachePath, deltaBytes );
        File.SetLastWriteTimeUtc( cachePath, DateTime.UtcNow.AddDays( -1 ) );
        var cachedLastWriteTime = File.GetLastWriteTimeUtc( cachePath );
        var requests = new List<string>();
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            requests.Add( name );
            var responseBytes = name switch
            {
                var value when value == $"{deltaName}.sha256" => deltaSidecar,
                var value when value == fullName => fullBytes,
                var value when value == $"{fullName}.sha256" => fullSidecar,
                _ => throw new InvalidOperationException( $"Unexpected request: {name}" )
            };
            return Task.FromResult( CreateResponse( responseBytes, name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var update = new UpdateCheckResult(
            true,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "v1.4.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
            "win-x64",
            true,
            fullAsset,
            [deltaAsset] );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            updatesDirectory );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Delta, result.Value.AssetKind );
        Assert.Equal( new Version( 1, 3, 1 ), result.Value.BaseVersion );
        Assert.Equal( [$"{deltaName}.sha256"], requests );
        Assert.Equal( "new", File.ReadAllText( Path.Combine( result.Value.StagingDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.True( File.GetLastWriteTimeUtc( cachePath ) > cachedLastWriteTime );
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    /// <summary>
    /// 再解析ポイントのキャッシュルートを利用せず、作業ルート外へキャッシュを書き込まないことを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncは再解析ポイントの差分キャッシュへ書き込まない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは再解析ポイントの差分キャッシュへ書き込まない ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var outsideDirectory = temporaryDirectory.GetPath( "outside" );
        var cacheDirectory = Path.Combine( updatesDirectory, "cache" );
        Directory.CreateDirectory( updatesDirectory );
        Directory.CreateDirectory( outsideDirectory );
        Directory.CreateSymbolicLink( cacheDirectory, outsideDirectory );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var deltaBytes = CreateDeltaPackageBytes( temporaryDirectory, "1.3.1", "1.4.0", "new" );
        const string deltaName = "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var fullBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
        const string fullName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullBytes )}  {fullName}\r\n" );
        var deltaAsset = CreateAsset( deltaName, deltaBytes, deltaSidecar, ApplicationUpdateAssetKind.Delta );
        var fullAsset = CreateAsset( fullName, fullBytes, fullSidecar, ApplicationUpdateAssetKind.Full );
        var requests = new List<string>();
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            requests.Add( name );
            var responseBytes = name switch
            {
                var value when value == deltaName => deltaBytes,
                var value when value == $"{deltaName}.sha256" => deltaSidecar,
                var value when value == fullName => fullBytes,
                var value when value == $"{fullName}.sha256" => fullSidecar,
                _ => throw new InvalidOperationException( $"Unexpected request: {name}" )
            };
            return Task.FromResult( CreateResponse( responseBytes, name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var update = new UpdateCheckResult(
            true,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "v1.4.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
            "win-x64",
            true,
            fullAsset,
            [deltaAsset] );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            updatesDirectory );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Delta, result.Value.AssetKind );
        Assert.Equal( [deltaName, $"{deltaName}.sha256"], requests );
        Assert.False( File.Exists( Path.Combine( outsideDirectory, $"{CalculateSha256( deltaBytes )}.zip" ) ) );
        Assert.Empty( Directory.GetFiles( outsideDirectory, "*.partial", SearchOption.TopDirectoryOnly ) );
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    /// <summary>
    /// 現在の更新アセットと一致しない差分キャッシュを使用せず再取得することを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncはハッシュが一致しない差分キャッシュを再取得する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncはハッシュが一致しない差分キャッシュを再取得する ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var deltaBytes = CreateDeltaPackageBytes( temporaryDirectory, "1.3.1", "1.4.0", "new" );
        const string deltaName = "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var fullBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
        const string fullName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullBytes )}  {fullName}\r\n" );
        var deltaAsset = CreateAsset( deltaName, deltaBytes, deltaSidecar, ApplicationUpdateAssetKind.Delta );
        var fullAsset = CreateAsset( fullName, fullBytes, fullSidecar, ApplicationUpdateAssetKind.Full );
        var cachePath = Path.Combine( updatesDirectory, "cache", $"{CalculateSha256( deltaBytes )}.zip" );
        Directory.CreateDirectory( Path.GetDirectoryName( cachePath )! );
        File.WriteAllBytes( cachePath, Encoding.UTF8.GetBytes( "tampered cache" ) );
        var requests = new List<string>();
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            requests.Add( name );
            var responseBytes = name switch
            {
                var value when value == deltaName => deltaBytes,
                var value when value == $"{deltaName}.sha256" => deltaSidecar,
                var value when value == fullName => fullBytes,
                var value when value == $"{fullName}.sha256" => fullSidecar,
                _ => throw new InvalidOperationException( $"Unexpected request: {name}" )
            };
            return Task.FromResult( CreateResponse( responseBytes, name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var update = new UpdateCheckResult(
            true,
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "v1.4.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
            "win-x64",
            true,
            fullAsset,
            [deltaAsset] );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            updatesDirectory );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Delta, result.Value.AssetKind );
        Assert.Equal( [deltaName, $"{deltaName}.sha256"], requests );
        Assert.Equal( deltaBytes, File.ReadAllBytes( cachePath ) );
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    [Fact]
    public async Task DownloadAndStageAsyncは差分検証失敗時に完全版へフォールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは差分検証失敗時に完全版へフォールバックする ) );
        WriteApplication( temporaryDirectory.GetPath( "application" ), "1.3.1", "old" );
        var packageBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
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
            [deltaAsset] );
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
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    /// <summary>
    /// 失敗した差分ワークスペースを削除できない場合も別領域で完全版へフォールバックすることを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncは差分領域を削除できない場合も完全版へフォールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは差分領域を削除できない場合も完全版へフォールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var fullBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
        const string fullName = "DCS-Translation-Tool-1.4.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullBytes )}  {fullName}\r\n" );
        var deltaBytes = Encoding.UTF8.GetBytes( "not a zip" );
        const string deltaName = "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var requests = new List<string>();
        FileStream? lockedFile = null;
        string? failedWorkspace = null;
        try {
            var httpClient = TestHttpClientFactory.Create( (request, _) => {
                var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
                requests.Add( name );
                if(name == $"{deltaName}.sha256" && lockedFile is null) {
                    failedWorkspace = Assert.Single( Directory.GetDirectories( updatesDirectory ) );
                    lockedFile = new FileStream(
                        Path.Combine( failedWorkspace, "locked.tmp" ),
                        FileMode.CreateNew,
                        FileAccess.ReadWrite,
                        FileShare.None );
                }

                var bytes = name switch
                {
                    var value when value == deltaName => deltaBytes,
                    var value when value == $"{deltaName}.sha256" => deltaSidecar,
                    var value when value == $"{fullName}.sha256" => fullSidecar,
                    _ => fullBytes
                };
                return Task.FromResult( CreateResponse(
                    bytes,
                    name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
            }, "https://github.com/" );
            var fullAsset = CreateAsset( fullName, fullBytes, fullSidecar, ApplicationUpdateAssetKind.Full );
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
                [deltaAsset] );
            var service = new ApplicationUpdateService(
                new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
                Mock.Of<ILoggingService>(),
                Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
                httpClient,
                applicationDirectory,
                updatesDirectory );

            var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

            Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
            Assert.Equal( ApplicationUpdateAssetKind.Full, result.Value.AssetKind );
            Assert.NotEqual( failedWorkspace, result.Value.WorkspaceDirectory );
            Assert.Contains( fullName, requests );
            Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
        }
        finally {
            lockedFile?.Dispose();
        }
    }

    [Fact]
    public async Task DownloadAndStageAsyncは必須項目がnullの差分から完全版へフォールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは必須項目がnullの差分から完全版へフォールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        WriteApplication( applicationDirectory, "1.3.1", "old" );
        var packageBytes = CreateFullPackageBytes( temporaryDirectory, "1.4.0", "win-x64", 64 * 1024 );
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
            [deltaAsset] );
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
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    /// <summary>
    /// 複数差分を順番に再構築し、処理済みアーカイブと中間ステージを削除することを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncは複数差分を逐次再構築して中間ファイルを削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは複数差分を逐次再構築して中間ファイルを削除する ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        WriteApplication( applicationDirectory, "2.0.0", "version 2.0" );
        var firstDeltaBytes = CreateDeltaPackageBytes(
            temporaryDirectory,
            "2.0.0",
            "2.1.0",
            "version 2.1" );
        var secondDeltaBytes = CreateDeltaPackageBytes(
            temporaryDirectory,
            "2.1.0",
            "2.2.0",
            "version 2.2" );
        const string firstName = "DCS-Translation-Tool-2.0.0-to-2.1.0-win-x64.delta.zip";
        const string secondName = "DCS-Translation-Tool-2.1.0-to-2.2.0-win-x64.delta.zip";
        var firstSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( firstDeltaBytes )}  {firstName}\r\n" );
        var secondSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( secondDeltaBytes )}  {secondName}\r\n" );
        var firstAsset = CreateAsset(
            firstName,
            firstDeltaBytes,
            firstSidecar,
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 0, 0 ),
            new Version( 2, 1, 0 ) );
        var secondAsset = CreateAsset(
            secondName,
            secondDeltaBytes,
            secondSidecar,
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 1, 0 ),
            new Version( 2, 2, 0 ) );
        var fullPackageBytes = CreateFullPackageBytes( temporaryDirectory, "2.2.0", "win-x64" );
        const string fullName = "DCS-Translation-Tool-2.2.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullPackageBytes )}  {fullName}\r\n" );
        var fullAsset = CreateAsset(
            fullName,
            fullPackageBytes,
            fullSidecar,
            ApplicationUpdateAssetKind.Full,
            null,
            new Version( 2, 2, 0 ),
            100_000 );
        var responses = new Dictionary<string, byte[]>( StringComparer.Ordinal )
        {
            [firstName] = firstDeltaBytes,
            [$"{firstName}.sha256"] = firstSidecar,
            [secondName] = secondDeltaBytes,
            [$"{secondName}.sha256"] = secondSidecar
        };
        var requests = new List<string>();
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            requests.Add( name );
            return Task.FromResult( CreateResponse(
                responses[name],
                name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var update = new UpdateCheckResult(
            true,
            new Version( 2, 0, 0 ),
            new Version( 2, 2, 0 ),
            "v2.2.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v2.2.0",
            "win-x64",
            true,
            fullAsset,
            [firstAsset, secondAsset] );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            updatesDirectory );
        var progressValues = new List<ApplicationUpdateProgress>();

        var result = await service.DownloadAndStageAsync(
            update,
            new InlineProgress<ApplicationUpdateProgress>( progressValues.Add ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Delta, result.Value.AssetKind );
        Assert.Equal( "version 2.2", File.ReadAllText( Path.Combine( result.Value.StagingDirectory, "DCS-Translation-Tool.exe" ) ) );
        Assert.Equal( [firstName, $"{firstName}.sha256", secondName, $"{secondName}.sha256"], requests );
        Assert.Empty( Directory.GetFiles( result.Value.WorkspaceDirectory, "*.zip*", SearchOption.AllDirectories ) );
        Assert.Single( Directory.GetDirectories( result.Value.WorkspaceDirectory, "staging*", SearchOption.TopDirectoryOnly ) );
        var expectedDownloadSize = firstAsset.Size + firstAsset.Checksum.Size + secondAsset.Size + secondAsset.Checksum.Size;
        Assert.NotEmpty( progressValues );
        Assert.All( progressValues, value => Assert.Equal( expectedDownloadSize, value.TotalBytes ) );
        Assert.Equal( expectedDownloadSize, progressValues.Max( value => value.DownloadedBytes ) );
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
        Assert.Equal(
            progressValues.Select( value => value.DownloadedBytes ).Order().ToArray(),
            progressValues.Select( value => value.DownloadedBytes ).ToArray() );
    }

    /// <summary>
    /// 差分取得のキャンセル時に完全版へフォールバックせず作業領域を削除することを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncは差分取得のキャンセル時に完全版を取得しない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは差分取得のキャンセル時に完全版を取得しない ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        WriteApplication( applicationDirectory, "2.0.0", "version 2.0" );
        var deltaBytes = CreateDeltaPackageBytes( temporaryDirectory, "2.0.0", "2.1.0", "version 2.1" );
        const string deltaName = "DCS-Translation-Tool-2.0.0-to-2.1.0-win-x64.delta.zip";
        var deltaSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( deltaBytes )}  {deltaName}\r\n" );
        var fullBytes = CreateFullPackageBytes( temporaryDirectory, "2.1.0", "win-x64" );
        const string fullName = "DCS-Translation-Tool-2.1.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullBytes )}  {fullName}\r\n" );
        var deltaAsset = CreateAsset(
            deltaName,
            deltaBytes,
            deltaSidecar,
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 0, 0 ),
            new Version( 2, 1, 0 ) );
        var fullAsset = CreateAsset(
            fullName,
            fullBytes,
            fullSidecar,
            ApplicationUpdateAssetKind.Full,
            null,
            new Version( 2, 1, 0 ),
            100_000 );
        using var cancellationTokenSource = new CancellationTokenSource();
        var requests = new List<string>();
        var httpClient = TestHttpClientFactory.Create( (request, cancellationToken) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            requests.Add( name );
            cancellationTokenSource.Cancel();
            return Task.FromCanceled<HttpResponseMessage>( cancellationToken );
        }, "https://github.com/" );
        var update = new UpdateCheckResult(
            true,
            new Version( 2, 0, 0 ),
            new Version( 2, 1, 0 ),
            "v2.1.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v2.1.0",
            "win-x64",
            true,
            fullAsset,
            [deltaAsset] );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            updatesDirectory );

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.DownloadAndStageAsync( update, null, cancellationTokenSource.Token ) );

        Assert.Equal( [deltaName], requests );
        Assert.DoesNotContain( fullName, requests );
        Assert.Empty( Directory.GetDirectories( updatesDirectory ) );
    }

    /// <summary>
    /// 後段差分のsidecar検証失敗時に中間領域を削除して完全版へフォールバックすることを確認する。
    /// </summary>
    [Fact]
    public async Task DownloadAndStageAsyncは後段差分のsidecar検証失敗時に完全版へフォールバックする() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( DownloadAndStageAsyncは後段差分のsidecar検証失敗時に完全版へフォールバックする ) );
        var applicationDirectory = temporaryDirectory.GetPath( "application" );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        WriteApplication( applicationDirectory, "2.0.0", "version 2.0" );
        var firstDeltaBytes = CreateDeltaPackageBytes( temporaryDirectory, "2.0.0", "2.1.0", "version 2.1" );
        var secondDeltaBytes = CreateDeltaPackageBytes( temporaryDirectory, "2.1.0", "2.2.0", "version 2.2" );
        const string firstName = "DCS-Translation-Tool-2.0.0-to-2.1.0-win-x64.delta.zip";
        const string secondName = "DCS-Translation-Tool-2.1.0-to-2.2.0-win-x64.delta.zip";
        var firstSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( firstDeltaBytes )}  {firstName}\r\n" );
        var invalidSecondSidecar = Encoding.UTF8.GetBytes( $"{new string( '0', 64 )}  {secondName}\r\n" );
        var firstAsset = CreateAsset(
            firstName,
            firstDeltaBytes,
            firstSidecar,
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 0, 0 ),
            new Version( 2, 1, 0 ) );
        var secondAsset = CreateAsset(
            secondName,
            secondDeltaBytes,
            invalidSecondSidecar,
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 1, 0 ),
            new Version( 2, 2, 0 ) );
        var fullBytes = CreateFullPackageBytes( temporaryDirectory, "2.2.0", "win-x64", 64 * 1024 );
        const string fullName = "DCS-Translation-Tool-2.2.0-win-x64.zip";
        var fullSidecar = Encoding.UTF8.GetBytes( $"{CalculateSha256( fullBytes )}  {fullName}\r\n" );
        var fullAsset = CreateAsset(
            fullName,
            fullBytes,
            fullSidecar,
            ApplicationUpdateAssetKind.Full,
            null,
            new Version( 2, 2, 0 ) );
        var responses = new Dictionary<string, byte[]>( StringComparer.Ordinal )
        {
            [firstName] = firstDeltaBytes,
            [$"{firstName}.sha256"] = firstSidecar,
            [secondName] = secondDeltaBytes,
            [$"{secondName}.sha256"] = invalidSecondSidecar,
            [fullName] = fullBytes,
            [$"{fullName}.sha256"] = fullSidecar
        };
        var requests = new List<string>();
        var httpClient = TestHttpClientFactory.Create( (request, _) => {
            var name = Path.GetFileName( request.RequestUri!.AbsolutePath );
            requests.Add( name );
            return Task.FromResult( CreateResponse(
                responses[name],
                name.EndsWith( ".sha256", StringComparison.Ordinal ) ? "text/plain" : "application/zip" ) );
        }, "https://github.com/" );
        var update = new UpdateCheckResult(
            true,
            new Version( 2, 0, 0 ),
            new Version( 2, 2, 0 ),
            "v2.2.0",
            "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v2.2.0",
            "win-x64",
            true,
            fullAsset,
            [firstAsset, secondAsset] );
        var service = new ApplicationUpdateService(
            new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() ),
            Mock.Of<ILoggingService>(),
            Mock.Of<DcsTranslationTool.Infrastructure.Interfaces.IProcessLauncher>(),
            httpClient,
            applicationDirectory,
            updatesDirectory );

        var result = await service.DownloadAndStageAsync( update, null, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess, string.Join( "; ", result.Errors.Select( error => error.Message ) ) );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.Value.AssetKind );
        Assert.Equal(
            [firstName, $"{firstName}.sha256", secondName, $"{secondName}.sha256", fullName, $"{fullName}.sha256"],
            requests );
        Assert.Equal(
            result.Value.WorkspaceDirectory,
            Assert.Single( Directory.GetDirectories( updatesDirectory ), directory => Path.GetFileName( directory ) != "cache" ) );
        Assert.True( service.DiscardStagedUpdate( result.Value ).IsSuccess );
    }

    private static byte[] CreateFullPackageBytes(
        TemporaryDirectory temporaryDirectory,
        string version,
        string rid,
        int paddingSize = 0
    ) {
        var packageRoot = temporaryDirectory.GetPath( $"package-{Guid.NewGuid():N}" );
        Directory.CreateDirectory( packageRoot );
        var executablePath = Path.Combine( packageRoot, "DCS-Translation-Tool.exe" );
        File.WriteAllText( executablePath, "executable" );
        if(paddingSize > 0) {
            File.WriteAllBytes( Path.Combine( packageRoot, "payload.bin" ), RandomNumberGenerator.GetBytes( paddingSize ) );
        }

        var manifestFiles = Directory.GetFiles( packageRoot, "*", SearchOption.AllDirectories )
            .Select( path => {
                var bytes = File.ReadAllBytes( path );
                return new ApplicationUpdateManifestFile(
                    Path.GetRelativePath( packageRoot, path ).Replace( '\\', '/' ),
                    bytes.LongLength,
                    CalculateSha256( bytes ) );
            } )
            .ToArray();
        var manifest = new ApplicationUpdateManifest(
            1,
            "DcsTranslationTool",
            version,
            rid,
            "DCS-Translation-Tool.exe",
            manifestFiles );
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

    private static byte[] CreateDeltaPackageBytes(
        TemporaryDirectory temporaryDirectory,
        string baseVersion,
        string targetVersion,
        string executableContent
    ) {
        var executableBytes = Encoding.UTF8.GetBytes( executableContent );
        var targetManifest = new ApplicationUpdateManifest(
            1,
            "DcsTranslationTool",
            targetVersion,
            "win-x64",
            "DCS-Translation-Tool.exe",
            [new ApplicationUpdateManifestFile(
                "DCS-Translation-Tool.exe",
                executableBytes.LongLength,
                CalculateSha256( executableBytes ) )] );
        var deltaManifest = new ApplicationUpdateDeltaManifest(
            1,
            "DcsTranslationTool",
            baseVersion,
            targetVersion,
            "win-x64",
            ["DCS-Translation-Tool.exe"],
            [],
            targetManifest );
        var packageRoot = temporaryDirectory.GetPath( $"delta-{baseVersion}-{targetVersion}-{Guid.NewGuid():N}" );
        var payloadDirectory = Path.Combine( packageRoot, "payload" );
        Directory.CreateDirectory( payloadDirectory );
        File.WriteAllBytes( Path.Combine( payloadDirectory, "DCS-Translation-Tool.exe" ), executableBytes );
        File.WriteAllText(
            Path.Combine( packageRoot, ApplicationUpdatePackageService.DeltaManifestFileName ),
            JsonSerializer.Serialize( deltaManifest, JsonOptions ) );
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

    private static string WriteApplyRequest( string workspaceDirectory, string applicationDirectory, string stagingDirectory, string baseVersion = "1.3.1" ) {
        Directory.CreateDirectory( workspaceDirectory );
        File.WriteAllText( Path.Combine( workspaceDirectory, ".in-use" ), string.Empty );
        var request = new ApplicationUpdateApplyRequest(
            applicationDirectory,
            stagingDirectory,
            Path.Combine( workspaceDirectory, "backup" ),
            0,
            "win-x64",
            baseVersion,
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
    ) => CreateAsset(
        name,
        packageBytes,
        sidecarBytes,
        kind,
        kind == ApplicationUpdateAssetKind.Delta ? new Version( 1, 3, 1 ) : null,
        new Version( 1, 4, 0 ) );

    private static ApplicationUpdateAsset CreateAsset(
        string name,
        byte[] packageBytes,
        byte[] sidecarBytes,
        ApplicationUpdateAssetKind kind,
        Version? baseVersion,
        Version targetVersion,
        long? advertisedSize = null
    ) => new(
        name,
        $"https://github.com/5kdn/DCS-Translation-Tool/releases/download/v{targetVersion.ToString( 3 )}/{name}",
        advertisedSize ?? packageBytes.LongLength,
        $"sha256:{CalculateSha256( packageBytes )}",
        kind,
        baseVersion,
        targetVersion,
        new ApplicationUpdateChecksumAsset(
            $"{name}.sha256",
            $"https://github.com/5kdn/DCS-Translation-Tool/releases/download/v{targetVersion.ToString( 3 )}/{name}.sha256",
            sidecarBytes.LongLength,
            $"sha256:{CalculateSha256( sidecarBytes )}" ) );

    /// <summary>
    /// 通知を呼び出しスレッド上で同期的に転送する進捗実装を表す。
    /// </summary>
    private sealed class InlineProgress<T>( Action<T> report ) : IProgress<T> {
        /// <inheritdoc />
        public void Report( T value ) => report( value );
    }

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