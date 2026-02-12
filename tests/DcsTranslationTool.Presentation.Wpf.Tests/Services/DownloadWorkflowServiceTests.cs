using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class DownloadWorkflowServiceTests {
    [Fact]
    public async Task DownloadFilesAsyncは対象が空の場合に成功を返す() {
        var loggerMock = new Mock<ILoggingService>();
        var sut = new DownloadWorkflowService( loggerMock.Object );

        var result = await sut.DownloadFilesAsync(
            [],
            "translate",
            TestContext.Current.CancellationToken
        );

        Assert.True( result.IsSuccess );
        Assert.Empty( result.Events );
    }

    [Fact]
    public async Task DownloadFilesAsyncはキャンセル時にOperationCanceledExceptionを送出する() {
        var loggerMock = new Mock<ILoggingService>();
        var sut = new DownloadWorkflowService( loggerMock.Object );

        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => await sut.DownloadFilesAsync(
            [new ApiDownloadFilePathsItem( "https://example.invalid/file.lua", "DCSWorld/Mods/aircraft/A10C/L10N/file.lua" )],
            "translate",
            cancellationTokenSource.Token
        ) );
    }
}