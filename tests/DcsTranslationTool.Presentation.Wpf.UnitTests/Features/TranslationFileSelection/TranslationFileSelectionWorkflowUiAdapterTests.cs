using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationFileSelection;

/// <summary>
/// <see cref="TranslationFileSelectionWorkflowUiAdapter"/> の動作を検証する。
/// </summary>
public sealed class TranslationFileSelectionWorkflowUiAdapterTests {
    [Fact]
    public async Task ApplyLoadResultAsyncはDispatcherService経由で状態反映する() {
        var dispatcherServiceMock = new Mock<IDispatcherService>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        var dispatcherScope = false;
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( action => InvokeThroughScopeAsync( action ) );
        var sut = new TranslationFileSelectionWorkflowUiAdapter(
            dispatcherServiceMock.Object,
            snackbarServiceMock.Object );
        var applied = false;

        await sut.ApplyLoadResultAsync(
            new TranslationFileSelectionLoadResult( [], string.Empty ),
            _ => applied = dispatcherScope );

        Assert.True( applied );

        Task InvokeThroughScopeAsync( Func<Task> action ) {
            dispatcherScope = true;
            return RunAsync();

            async Task RunAsync() {
                try {
                    await action();
                }
                finally {
                    dispatcherScope = false;
                }
            }
        }
    }

    [Fact]
    public async Task ApplyLoadResultAsyncは通知メッセージ存在時のみSnackbarを表示する() {
        var dispatcherServiceMock = new Mock<IDispatcherService>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( action => action() );
        var sut = new TranslationFileSelectionWorkflowUiAdapter(
            dispatcherServiceMock.Object,
            snackbarServiceMock.Object );

        await sut.ApplyLoadResultAsync(
            new TranslationFileSelectionLoadResult( [], string.Empty, Strings_Translation.LoadFailedMessage ),
            _ => { } );
        await sut.ApplyLoadResultAsync(
            new TranslationFileSelectionLoadResult( [], string.Empty ),
            _ => { } );

        snackbarServiceMock.Verify(
            service => service.Show(
                Strings_Translation.LoadFailedMessage,
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ),
            Times.Once );
    }
}