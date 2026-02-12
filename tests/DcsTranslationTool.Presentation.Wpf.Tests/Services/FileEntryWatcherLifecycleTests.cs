using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

/// <summary>
/// FileEntryWatcherLifecycle の動作を検証するテストを提供する。
/// </summary>
public sealed class FileEntryWatcherLifecycleTests {
    /// <summary>
    /// StartWatching を重複実行しても監視開始が一度だけ呼ばれることを確認する。
    /// </summary>
    [Fact]
    public void StartWatchingを重複実行してもWatchは一度だけ呼ばれる() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( new AppSettings { TranslateFileDir = "C:\\Translation" } );

        var fileEntryServiceMock = new Mock<IFileEntryService>();
        fileEntryServiceMock
            .Setup( service => service.Watch( It.IsAny<string>() ) );

        var loggerMock = new Mock<ILoggingService>();
        var sut = new FileEntryWatcherLifecycle(
            appSettingsServiceMock.Object,
            fileEntryServiceMock.Object,
            loggerMock.Object
        );

        sut.StartWatching();
        sut.StartWatching();

        fileEntryServiceMock.Verify( service => service.Watch( "C:\\Translation" ), Times.Once );
    }

    /// <summary>
    /// StopWatching を重複実行しても例外にならず、停止時にのみ破棄が実行されることを確認する。
    /// </summary>
    [Fact]
    public void StopWatchingを重複実行しても安全に停止できる() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( new AppSettings { TranslateFileDir = "C:\\Translation" } );

        var fileEntryServiceMock = new Mock<IFileEntryService>();
        fileEntryServiceMock
            .Setup( service => service.Watch( It.IsAny<string>() ) );
        fileEntryServiceMock
            .Setup( service => service.Dispose() );

        var loggerMock = new Mock<ILoggingService>();
        var sut = new FileEntryWatcherLifecycle(
            appSettingsServiceMock.Object,
            fileEntryServiceMock.Object,
            loggerMock.Object
        );

        sut.StartWatching();
        sut.StartWatching();
        sut.StopWatching();
        fileEntryServiceMock.Verify( service => service.Dispose(), Times.Never );

        sut.StopWatching();
        sut.StopWatching();

        fileEntryServiceMock.Verify( service => service.Dispose(), Times.Once );
    }
}