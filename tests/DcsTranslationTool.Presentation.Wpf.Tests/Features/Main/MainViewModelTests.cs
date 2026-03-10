using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Translation;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.Main;

/// <summary>MainViewModel のナビゲーション動作を検証するテストを提供する。</summary>
public sealed class MainViewModelTests {
    /// <summary>NavToTranslation を呼び出した際に TranslationViewModel へ遷移することを検証する。</summary>
    [StaFact]
    public void NavToTranslationを呼び出すとTranslationViewModelへ遷移する() {
        var loggerMock = new Mock<ILoggingService>();
        var navigationServiceMock = new Mock<INavigationService>();
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<TranslationViewModel>( It.IsAny<object?>() ) );

        var viewModel = new MainViewModel(
            loggerMock.Object,
            navigationServiceMock.Object );

        viewModel.NavToTranslation();

        navigationServiceMock.Verify( service => service.NavigateToViewModel<TranslationViewModel>( It.IsAny<object?>() ), Times.Once );
    }
}