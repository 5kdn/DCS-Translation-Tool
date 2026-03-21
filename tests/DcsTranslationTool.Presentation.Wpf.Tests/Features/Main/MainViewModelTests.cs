using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.Main;

/// <summary>MainViewModel のナビゲーション動作を検証するテストを提供する。</summary>
public sealed class MainViewModelTests {
    /// <summary>NavToTranslationFileSelection を呼び出した際に Translation File Selection ページの ViewModel へ遷移することを検証する。</summary>
    [StaFact]
    public void NavToTranslationFileSelectionを呼び出すとTranslationFileSelectionページのViewModelへ遷移する() {
        var loggerMock = new Mock<ILoggingService>();
        var navigationServiceMock = new Mock<INavigationService>();
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<TranslationFileSelectionViewModel>( It.IsAny<object?>() ) );

        var viewModel = new MainViewModel(
            loggerMock.Object,
            navigationServiceMock.Object );

        viewModel.NavToTranslationFileSelection();

        navigationServiceMock.Verify( service => service.NavigateToViewModel<TranslationFileSelectionViewModel>( It.IsAny<object?>() ), Times.Once );
    }
}