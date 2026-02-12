using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class ApplyWorkflowServiceTests {
    [Fact]
    public async Task ApplyAsyncはRepoOnly同期が失敗した場合に適用処理を実行しない() {
        var repoOnlySyncService = new Mock<IRepoOnlySyncService>( MockBehavior.Strict );
        var entryApplyService = new Mock<IEntryApplyService>( MockBehavior.Strict );
        var sut = new ApplyWorkflowService( repoOnlySyncService.Object, entryApplyService.Object );

        var targetEntries = Array.Empty<IFileEntryViewModel>();
        repoOnlySyncService
            .Setup( service => service.EnsureRepoOnlyFilesAsync(
                targetEntries,
                "translate",
                "translate\\",
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( false );

        var result = await sut.ApplyAsync(
            targetEntries,
            "root",
            "root\\",
            "translate",
            "translate\\",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        Assert.False( result );
        repoOnlySyncService.VerifyAll();
        entryApplyService.Verify( service => service.ApplyEntriesAsync(
            It.IsAny<IReadOnlyList<IFileEntryViewModel>>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<Func<double, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
    }

    [Fact]
    public async Task ApplyAsyncはRepoOnly同期成功時に適用処理を実行する() {
        var repoOnlySyncService = new Mock<IRepoOnlySyncService>( MockBehavior.Strict );
        var entryApplyService = new Mock<IEntryApplyService>( MockBehavior.Strict );
        var sut = new ApplyWorkflowService( repoOnlySyncService.Object, entryApplyService.Object );

        var targetEntries = Array.Empty<IFileEntryViewModel>();
        repoOnlySyncService
            .Setup( service => service.EnsureRepoOnlyFilesAsync(
                targetEntries,
                "translate",
                "translate\\",
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( true );
        entryApplyService
            .Setup( service => service.ApplyEntriesAsync(
                targetEntries,
                "root",
                "root\\",
                "translate",
                "translate\\",
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( true );

        var result = await sut.ApplyAsync(
            targetEntries,
            "root",
            "root\\",
            "translate",
            "translate\\",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        Assert.True( result );
        repoOnlySyncService.VerifyAll();
        entryApplyService.VerifyAll();
    }
}