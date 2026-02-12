using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class RepoOnlySyncServiceTests {
    [Fact]
    public async Task EnsureRepoOnlyFilesAsyncはRepoOnlyが存在しない場合にtrueを返す() {
        var apiService = new Mock<IApiService>( MockBehavior.Strict );
        var logger = new Mock<ILoggingService>();
        var sut = new RepoOnlySyncService( apiService.Object, logger.Object, new PathSafetyGuard() );

        var entry = new FileEntryViewModel(
            new LocalFileEntry( "Example.lua", "UserMissions/My/Example.lua", false, "local" ),
            ChangeTypeMode.Download,
            logger.Object
        );

        var downloadCalled = false;
        var result = await sut.EnsureRepoOnlyFilesAsync(
            [entry],
            "translate",
            "translate\\",
            _ => {
                downloadCalled = true;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        Assert.True( result );
        Assert.False( downloadCalled );
    }

    [Fact]
    public async Task EnsureRepoOnlyFilesAsyncはURL取得失敗時にfalseを返す() {
        var apiService = new Mock<IApiService>( MockBehavior.Strict );
        var logger = new Mock<ILoggingService>();
        var sut = new RepoOnlySyncService( apiService.Object, logger.Object, new PathSafetyGuard() );

        const string repoPath = "DCSWorld/Mods/aircraft/A10C/L10N/RepoOnly.lua";
        var entry = new FileEntryViewModel(
            new RepoFileEntry( "RepoOnly.lua", repoPath, false, "repo" ),
            ChangeTypeMode.Download,
            logger.Object
        );

        apiService
            .Setup( service => service.DownloadFilePathsAsync(
                It.Is<ApiDownloadFilePathsRequest>( request => request.Paths.Count == 1 && request.Paths[0] == repoPath ),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( Result.Fail<ApiDownloadFilePathsResult>( "failed" ) );

        var messages = new List<string>();
        var result = await sut.EnsureRepoOnlyFilesAsync(
            [entry],
            "translate",
            "translate\\",
            _ => Task.CompletedTask,
            message => {
                messages.Add( message );
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken
        );

        Assert.False( result );
        Assert.Contains( "ダウンロードURLの取得に失敗しました", messages );
        apiService.VerifyAll();
    }
}