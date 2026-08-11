using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.TestCommon.IO;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

/// <summary>
/// <see cref="UpdateWorkspaceLeaseService"/> の作業領域リースとクリーンアップを検証する。
/// </summary>
public sealed class UpdateWorkspaceLeaseServiceTests {
    /// <summary>
    /// 使用中の作業領域をクリーンアップから除外することを確認する。
    /// </summary>
    [Fact]
    public void CleanupCompletedWorkspacesは使用中の作業領域を削除しない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedWorkspacesは使用中の作業領域を削除しない ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        using var service = new UpdateWorkspaceLeaseService( Mock.Of<ILoggingService>(), updatesDirectory );
        var workspace = service.CreateWorkspaceDirectory( new Version( 1, 4, 0 ), "win-x64" );

        service.CleanupCompletedWorkspaces();

        Assert.True( Directory.Exists( workspace ) );
    }

    /// <summary>
    /// 親プロセスのリース解放後に作業領域をクリーンアップできることを確認する。
    /// </summary>
    [Fact]
    public void CleanupCompletedWorkspacesはリース解放後の作業領域を削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedWorkspacesはリース解放後の作業領域を削除する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        using var service = new UpdateWorkspaceLeaseService( Mock.Of<ILoggingService>(), updatesDirectory );
        var workspace = service.CreateWorkspaceDirectory( new Version( 1, 4, 0 ), "win-x64" );

        service.ReleaseWorkspaceLease( workspace );
        service.CleanupCompletedWorkspaces();

        Assert.False( Directory.Exists( workspace ) );
    }

    /// <summary>
    /// 親プロセスのリース解放後も更新runnerのリースが作業領域を保護することを確認する。
    /// </summary>
    [Fact]
    public void CleanupCompletedWorkspacesはrunnerのリースを保持する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( CleanupCompletedWorkspacesはrunnerのリースを保持する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        using var service = new UpdateWorkspaceLeaseService( Mock.Of<ILoggingService>(), updatesDirectory );
        var workspace = service.CreateWorkspaceDirectory( new Version( 1, 4, 0 ), "win-x64" );

        using var runnerLease = service.AcquireWorkspaceLease( workspace );
        service.ReleaseWorkspaceLease( workspace );
        service.CleanupCompletedWorkspaces();

        Assert.True( Directory.Exists( workspace ) );
    }

    /// <summary>
    /// 更新ルート外または命名規則外の作業領域を所有しないことを確認する。
    /// </summary>
    [Fact]
    public void IsOwnedWorkspaceは更新ルート外の作業領域を拒否する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( IsOwnedWorkspaceは更新ルート外の作業領域を拒否する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        using var service = new UpdateWorkspaceLeaseService( Mock.Of<ILoggingService>(), updatesDirectory );
        var outsideWorkspace = temporaryDirectory.GetPath( "outside/1.4.0-win-x64-11111111111111111111111111111111" );

        var isOwned = service.IsOwnedWorkspace( outsideWorkspace, new Version( 1, 4, 0 ), "win-x64" );

        Assert.False( isOwned );
    }
}