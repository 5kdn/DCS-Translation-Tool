using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Services;

/// <summary>
/// <see cref="TranslationFileSelectionActionService"/> の動作を検証する。
/// </summary>
public sealed class TranslationFileSelectionActionServiceTests {
    [Fact]
    public void OpenDirectoryは指定パスを開く() {
        var loggerMock = new Mock<ILoggingService>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        var systemServiceMock = new Mock<ISystemService>();
        var factoryMock = new Mock<ITranslationCreationViewModelFactory>();
        var windowManagerMock = new Mock<IWindowManager>();
        var sut = new TranslationFileSelectionActionService(
            loggerMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object,
            factoryMock.Object,
            windowManagerMock.Object );

        sut.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        systemServiceMock.Verify( service => service.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ), Times.Once );
    }

    [Fact]
    public async Task OpenTranslationCreationAsyncは生成失敗時に通知する() {
        var loggerMock = new Mock<ILoggingService>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        var systemServiceMock = new Mock<ISystemService>();
        var factoryMock = new Mock<ITranslationCreationViewModelFactory>();
        var windowManagerMock = new Mock<IWindowManager>();
        factoryMock
            .Setup( factory => factory.Create( It.IsAny<string>() ) )
            .Throws( new InvalidOperationException( "failed" ) );
        var sut = new TranslationFileSelectionActionService(
            loggerMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object,
            factoryMock.Object,
            windowManagerMock.Object );

        await sut.OpenTranslationCreationAsync( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        snackbarServiceMock.Verify(
            service => service.Show(
                Strings_Translation.CreateTranslationWindowOpenFailedMessage,
                It.IsAny<string?>(),
                It.IsAny<System.Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ),
            Times.Once );
    }
}