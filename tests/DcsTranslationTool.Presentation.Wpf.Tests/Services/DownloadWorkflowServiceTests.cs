using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class DownloadWorkflowServiceTests {
    [Fact]
    public async Task ApplyAsyncはApplyWorkflowServiceへ委譲する() {
        var apiService = new Mock<IApiService>( MockBehavior.Strict );
        var logger = new Mock<ILoggingService>();
        var applyWorkflowService = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiService.Object, logger.Object, applyWorkflowService.Object );

        var targetEntries = Array.Empty<IFileEntryViewModel>();
        applyWorkflowService
            .Setup( service => service.ApplyAsync(
                targetEntries,
                "root",
                "root\\",
                "translate",
                "translate\\",
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
            TestContext.Current.CancellationToken
        );

        Assert.True( result );
        applyWorkflowService.VerifyAll();
    }
}