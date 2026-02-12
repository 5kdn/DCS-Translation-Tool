using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class ApplyWorkflowServiceTests {
    [Fact]
    public async Task ApplyAsyncはURL取得失敗時に失敗結果を返す() {
        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
        var sut = new ApplyWorkflowService(
            apiServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            loggerMock.Object,
            zipServiceMock.Object
        );

        const string repoPath = "DCSWorld/Mods/aircraft/A10C/L10N/RepoOnly.lua";
        var entry = new FileEntryViewModel(
            new RepoFileEntry( "RepoOnly.lua", repoPath, false, "repo" ),
            ChangeTypeMode.Download,
            loggerMock.Object
        );

        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.Is<ApiDownloadFilePathsRequest>( request => request.Paths.Count == 1 && request.Paths[0] == repoPath ),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( Result.Fail<ApiDownloadFilePathsResult>( ResultErrorFactory.External( "failed", "TEST" ) ) );

        var result = await sut.ApplyAsync(
            [entry],
            "root",
            "root\\",
            "translate",
            "translate\\",
            TestContext.Current.CancellationToken
        );

        Assert.False( result.IsSuccess );
        Assert.Contains( result.Events, e => e.Kind == WorkflowEventKind.Notification && e.Message == "ダウンロードURLの取得に失敗しました" );
        apiServiceMock.VerifyAll();
    }
}