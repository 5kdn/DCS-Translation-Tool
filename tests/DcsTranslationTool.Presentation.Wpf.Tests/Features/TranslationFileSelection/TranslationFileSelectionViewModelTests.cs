using Caliburn.Micro;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationFileSelection;

/// <summary>
/// TranslationFileSelectionViewModel の動作を検証する。
/// </summary>
public sealed class TranslationFileSelectionViewModelTests {
    [StaFact]
    public async Task ActivateAsyncは初期読込でカテゴリ別タブを構築する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true ),
                    new TranslationArchiveEntry(
                        "Campaign1.trk",
                        @"C:\DCSWorld\Mods\campaigns\RedFlag\Campaign1.trk",
                        "RedFlag/Campaign1.trk",
                        TranslationArchiveCategory.DlcCampaigns,
                        TranslationArchiveType.Trk,
                        true )
                ] );

        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Equal( 3, viewModel.Tabs.Count );
        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var aircraftNode = Assert.Single( aircraftTab.Root.Children );
        Assert.Equal( "A10C", aircraftNode.Name );
        Assert.Single( aircraftNode.Children );
    }

    [StaFact]
    public async Task Refreshは一覧を再読込する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .SetupSequence( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "First.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\First.miz",
                        "A10C/First.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Second.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Second.miz",
                        "A10C/Second.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        await viewModel.Refresh();

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );
        Assert.Equal( "Second.miz", fileNode.Name );
        context.DiscoveryServiceMock.Verify(
            service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ),
            Times.Exactly( 2 ) );
    }

    [StaFact]
    public async Task 選択状態変更時にHasSelectedEntryが更新される() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );

        Assert.False( viewModel.HasSelectedEntry );

        fileNode.IsSelected = true;

        Assert.True( viewModel.HasSelectedEntry );
        Assert.True( viewModel.CanOpenDirectory );
        Assert.True( viewModel.CanCreateTranslation );
    }

    [StaFact]
    public async Task ファイルノード選択時にOpenDirectoryはファイルパスで開く() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );
        fileNode.IsSelected = true;

        viewModel.OpenDirectory();

        context.SystemServiceMock.Verify( service => service.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ), Times.Once );
    }

    [StaFact]
    public async Task ディレクトリノード選択時にOpenDirectoryはディレクトリパスで開く() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        moduleNode.IsSelected = true;

        viewModel.OpenDirectory();

        context.SystemServiceMock.Verify( service => service.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C" ), Times.Once );
    }

    [StaFact]
    public async Task ディレクトリノード選択時はCanCreateTranslationがfalseになる() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        moduleNode.IsSelected = true;

        Assert.False( viewModel.CanCreateTranslation );
    }

    [StaFact]
    public async Task 未選択時はCanOpenDirectoryがfalseになる() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.False( viewModel.CanOpenDirectory );
        Assert.False( viewModel.CanCreateTranslation );
    }

    [StaFact]
    public async Task ノード選択時にCreateTranslationはウィンドウを表示する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Mission1.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                        "A10C/Mission1.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz,
                        true )
                ] );
        context.WindowManagerMock
            .Setup( manager => manager.ShowWindowAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .Returns( Task.CompletedTask );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );
        fileNode.IsSelected = true;

        await viewModel.CreateTranslation();

        context.WindowManagerMock.Verify( manager => manager.ShowWindowAsync(
            It.Is<object>( model => IsTranslationCreationViewModelWithArchiveFullPath( model, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ) ),
            It.IsAny<object?>(),
            It.IsAny<IDictionary<string, object>?>() ), Times.Once );
    }

    [StaFact]
    public async Task trkファイル選択時にCanCreateTranslationがtrueになる() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.DiscoveryServiceMock
            .Setup( service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Campaign1.trk",
                        @"C:\DCSWorld\Mods\campaigns\RedFlag\Campaign1.trk",
                        "RedFlag/Campaign1.trk",
                        TranslationArchiveCategory.DlcCampaigns,
                        TranslationArchiveType.Trk,
                        true )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var campaignTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.DlcCampaigns );
        viewModel.SelectedTabIndex = viewModel.Tabs.IndexOf( campaignTab );
        var campaignNode = Assert.Single( campaignTab.Root.Children );
        var fileNode = Assert.Single( campaignNode.Children );
        fileNode.IsSelected = true;

        Assert.True( viewModel.CanCreateTranslation );
    }

    [StaFact]
    public async Task 設定未指定時は設定案内メッセージを表示する() {
        var context = new TranslationFileSelectionViewModelTestContext( new AppSettings
        {
            DcsWorldInstallDir = string.Empty,
            SourceUserMissionDir = string.Empty,
        } );
        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Equal( Strings_Translation.SettingsNotConfiguredMessage, viewModel.CurrentStatusMessage );
        context.DiscoveryServiceMock.Verify(
            service => service.DiscoverAsync( It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>() ),
            Times.Never );
    }

    private static bool IsTranslationCreationViewModelWithArchiveFullPath( object model, string expectedArchiveFullPath ) =>
        model is TranslationCreationViewModel translationCreationViewModel
        && translationCreationViewModel.ArchiveFullPath == expectedArchiveFullPath;

    private sealed class TranslationFileSelectionViewModelTestContext {
        private readonly AppSettings _settings;

        internal TranslationFileSelectionViewModelTestContext( AppSettings? settings = null ) {
            _settings = settings ?? new AppSettings
            {
                DcsWorldInstallDir = @"C:\DCSWorld",
                SourceUserMissionDir = @"C:\UserMissions",
            };

            AppSettingsServiceMock
                .Setup( service => service.Settings )
                .Returns( _settings );

            DispatcherServiceMock
                .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
                .Returns<Func<Task>>( func => func() );
        }

        internal Mock<IAppSettingsService> AppSettingsServiceMock { get; } = new();
        internal Mock<IDispatcherService> DispatcherServiceMock { get; } = new();
        internal Mock<ILoggingService> LoggerMock { get; } = new();
        internal Mock<ISnackbarService> SnackbarServiceMock { get; } = new();
        internal Mock<ISystemService> SystemServiceMock { get; } = new();
        internal Mock<IWindowManager> WindowManagerMock { get; } = new();
        internal Mock<ITranslationArchiveDiscoveryService> DiscoveryServiceMock { get; } = new();

        internal TranslationFileSelectionViewModel CreateViewModel() =>
            new(
                AppSettingsServiceMock.Object,
                DispatcherServiceMock.Object,
                LoggerMock.Object,
                SnackbarServiceMock.Object,
                SystemServiceMock.Object,
                WindowManagerMock.Object,
                DiscoveryServiceMock.Object );
    }
}