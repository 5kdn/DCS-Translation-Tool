using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Services;

/// <summary>
/// <see cref="TranslationFileSelectionWorkflowService"/> の動作を検証する。
/// </summary>
public sealed class TranslationFileSelectionWorkflowServiceTests {
    [Fact]
    public async Task 設定未指定時は設定案内メッセージを返す() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var loggerMock = new Mock<ILoggingService>();
        var discoveryServiceMock = new Mock<ITranslationArchiveDiscoveryService>();
        var treeServiceMock = new Mock<ITranslationArchiveTreeService>();
        treeServiceMock
            .Setup( service => service.BuildTabs( It.IsAny<IReadOnlyList<TranslationArchiveEntry>>(), It.IsAny<string?>(), It.IsAny<string?>() ) )
            .Returns( [] );
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings() );
        var sut = new TranslationFileSelectionWorkflowService(
            appSettingsServiceMock.Object,
            loggerMock.Object,
            discoveryServiceMock.Object,
            treeServiceMock.Object );

        var result = await sut.LoadAsync( CancellationToken.None );

        Assert.Equal( Strings_Translation.SettingsNotConfiguredMessage, result.StatusMessage );
        discoveryServiceMock.Verify(
            service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ),
            Times.Never );
    }

    [Fact]
    public async Task 読み込み失敗時は失敗メッセージを返す() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        var loggerMock = new Mock<ILoggingService>();
        var discoveryServiceMock = new Mock<ITranslationArchiveDiscoveryService>();
        var treeServiceMock = new Mock<ITranslationArchiveTreeService>();
        treeServiceMock
            .Setup( service => service.BuildTabs( It.IsAny<IReadOnlyList<TranslationArchiveEntry>>(), It.IsAny<string?>(), It.IsAny<string?>() ) )
            .Returns( [] );
        appSettingsServiceMock
            .Setup( service => service.Settings )
            .Returns( new AppSettings
            {
                DcsWorldInstallDir = @"C:\DCSWorld",
                SourceUserMissionDir = @"C:\UserMissions"
            } );
        discoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ThrowsAsync( new InvalidOperationException( "failed" ) );
        var sut = new TranslationFileSelectionWorkflowService(
            appSettingsServiceMock.Object,
            loggerMock.Object,
            discoveryServiceMock.Object,
            treeServiceMock.Object );

        var result = await sut.LoadAsync( CancellationToken.None );

        Assert.Equal( Strings_Translation.LoadFailedMessage, result.StatusMessage );
        Assert.Equal( Strings_Translation.LoadFailedMessage, result.NotificationMessage );
    }
}