using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;

using Caliburn.Micro;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
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
                        TranslationArchiveType.Miz ),
                    new TranslationArchiveEntry(
                        "Campaign1.trk",
                        @"C:\DCSWorld\Mods\campaigns\RedFlag\Campaign1.trk",
                        "RedFlag/Campaign1.trk",
                        TranslationArchiveCategory.DlcCampaigns,
                        TranslationArchiveType.Trk )
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
                        TranslationArchiveType.Miz )
                ] )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Second.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Second.miz",
                        "A10C/Second.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz )
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
    public async Task Refresh後は旧ツリーの選択状態を引き継がない() {
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
                        TranslationArchiveType.Miz )
                ] )
            .ReturnsAsync(
                [
                    new TranslationArchiveEntry(
                        "Second.miz",
                        @"C:\DCSWorld\Mods\aircraft\A10C\Second.miz",
                        "A10C/Second.miz",
                        TranslationArchiveCategory.Aircraft,
                        TranslationArchiveType.Miz )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var initialTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var initialModuleNode = Assert.Single( initialTab.Root.Children );
        var initialFileNode = Assert.Single( initialModuleNode.Children );
        initialFileNode.IsSelected = true;

        Assert.True( viewModel.HasSelectedEntry );

        await viewModel.Refresh();

        var refreshedTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var refreshedModuleNode = Assert.Single( refreshedTab.Root.Children );
        var refreshedFileNode = Assert.Single( refreshedModuleNode.Children );

        Assert.False( viewModel.HasSelectedEntry );
        Assert.False( refreshedFileNode.IsSelected );
        Assert.Equal( "Second.miz", refreshedFileNode.Name );
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
                        TranslationArchiveType.Miz )
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
                        TranslationArchiveType.Miz )
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
                        TranslationArchiveType.Miz )
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
    public async Task ディレクトリノード選択時に子ファイルは自動選択されない() {
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
                        TranslationArchiveType.Miz )
                ] );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );

        moduleNode.IsSelected = true;

        Assert.True( moduleNode.IsSelected );
        Assert.False( fileNode.IsSelected );
        Assert.True( viewModel.CanOpenDirectory );
        Assert.False( viewModel.CanCreateTranslation );
    }

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
                        TranslationArchiveType.Miz )
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
                        TranslationArchiveType.Miz )
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
                        TranslationArchiveType.Miz )
                ] );
        context.WindowManagerMock
            .Setup( manager => manager.ShowWindowAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .Returns( Task.CompletedTask );
        context.TranslationCreationViewModelFactoryMock
            .Setup( factory => factory.Create( It.IsAny<string>() ) )
            .Returns<string>( path => new TranslationCreationViewModel(
                path,
                context.AppSettingsServiceMock.Object,
                context.ApplicationInfoServiceMock.Object,
                context.DialogServiceMock.Object,
                context.DialogProviderMock.Object,
                context.SystemServiceMock.Object,
                context.LoggerMock.Object,
                context.TranslationDictionaryServiceMock.Object ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );
        fileNode.IsSelected = true;

        await viewModel.CreateTranslation();

        context.TranslationCreationViewModelFactoryMock.Verify(
            factory => factory.Create( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ),
            Times.Once );
        context.WindowManagerMock.Verify( manager => manager.ShowWindowAsync(
            It.Is<object>( model => IsTranslationCreationViewModelWithArchiveFullPath( model, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ) ),
            It.IsAny<object?>(),
            It.IsAny<IDictionary<string, object>?>() ), Times.Once );
    }

    public async Task CreateTranslationはファクトリ失敗時にsnackbarと詳細ログを出す() {
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
                        TranslationArchiveType.Miz )
                ] );
        context.TranslationCreationViewModelFactoryMock
            .Setup( factory => factory.Create( It.IsAny<string>() ) )
            .Throws( new InvalidOperationException( "factory failed" ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );
        fileNode.IsSelected = true;

        await viewModel.CreateTranslation();

        context.SnackbarServiceMock.Verify( service => service.Show(
            Strings_Translation.CreateTranslationWindowOpenFailedMessage,
            It.IsAny<string?>(),
            It.IsAny<System.Action?>(),
            It.IsAny<object?>(),
            It.IsAny<TimeSpan?>() ), Times.Once );
        context.LoggerMock.Verify(
            service => service.Error(
                It.Is<string>( message => message.Contains( "ViewModel の生成に失敗", StringComparison.Ordinal ) && message.Contains( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz", StringComparison.Ordinal ) ),
                It.IsAny<Exception>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
    }

    [StaFact]
    public async Task CreateTranslationはウィンドウ表示失敗時にsnackbarと詳細ログを出す() {
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
                        TranslationArchiveType.Miz )
                ] );
        context.TranslationCreationViewModelFactoryMock
            .Setup( factory => factory.Create( It.IsAny<string>() ) )
            .Returns<string>( path => new TranslationCreationViewModel(
                path,
                context.AppSettingsServiceMock.Object,
                context.ApplicationInfoServiceMock.Object,
                context.DialogServiceMock.Object,
                context.DialogProviderMock.Object,
                context.SystemServiceMock.Object,
                context.LoggerMock.Object,
                context.TranslationDictionaryServiceMock.Object ) );
        context.WindowManagerMock
            .Setup( manager => manager.ShowWindowAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .ThrowsAsync( new InvalidOperationException( "window failed" ) );

        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var aircraftTab = viewModel.Tabs.Single( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = Assert.Single( aircraftTab.Root.Children );
        var fileNode = Assert.Single( moduleNode.Children );
        fileNode.IsSelected = true;

        await viewModel.CreateTranslation();

        context.SnackbarServiceMock.Verify( service => service.Show(
            Strings_Translation.CreateTranslationWindowOpenFailedMessage,
            It.IsAny<string?>(),
            It.IsAny<System.Action?>(),
            It.IsAny<object?>(),
            It.IsAny<TimeSpan?>() ), Times.Once );
        context.LoggerMock.Verify(
            service => service.Error(
                It.Is<string>( message => message.Contains( "ウィンドウの表示に失敗", StringComparison.Ordinal ) && message.Contains( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz", StringComparison.Ordinal ) ),
                It.IsAny<Exception>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.Once );
    }

    [StaFact]
    public async Task Trkファイル選択時にCanCreateTranslationがtrueになる() {
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
                        TranslationArchiveType.Trk )
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

    [StaFact]
    public async Task ディレクトリ解除時に選択済み子ファイルへ昇格してCanCreateTranslationがtrueになる() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var scenario = CreateManualSelectionScenario( viewModel );
        SimulateDirectoryToFileSelectionTransition( viewModel, scenario );

        Assert.True( viewModel.HasSelectedEntry );
        Assert.True( viewModel.CanCreateTranslation );
    }

    [StaFact]
    public async Task ディレクトリ解除時に選択済み子ファイルへ昇格してOpenDirectoryはファイルパスで開く() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var scenario = CreateManualSelectionScenario( viewModel );
        SimulateDirectoryToFileSelectionTransition( viewModel, scenario );

        viewModel.OpenDirectory();

        context.SystemServiceMock.Verify( service => service.OpenDirectory( scenario.FilePath ), Times.Once );
    }

    [StaFact]
    public async Task ディレクトリ解除時に選択済み子ファイルへ昇格してCreateTranslationはファイルパスを使う() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.WindowManagerMock
            .Setup( manager => manager.ShowWindowAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .Returns( Task.CompletedTask );
        context.TranslationCreationViewModelFactoryMock
            .Setup( factory => factory.Create( It.IsAny<string>() ) )
            .Returns<string>( path => new TranslationCreationViewModel(
                path,
                context.AppSettingsServiceMock.Object,
                context.ApplicationInfoServiceMock.Object,
                context.DialogServiceMock.Object,
                context.DialogProviderMock.Object,
                context.SystemServiceMock.Object,
                context.LoggerMock.Object,
                context.TranslationDictionaryServiceMock.Object ) );
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        var scenario = CreateManualSelectionScenario( viewModel );
        SimulateDirectoryToFileSelectionTransition( viewModel, scenario );

        await viewModel.CreateTranslation();

        context.TranslationCreationViewModelFactoryMock.Verify( factory => factory.Create( scenario.FilePath ), Times.Once );
        context.WindowManagerMock.Verify( manager => manager.ShowWindowAsync(
            It.Is<object>( model => IsTranslationCreationViewModelWithArchiveFullPath( model, scenario.FilePath ) ),
            It.IsAny<object?>(),
            It.IsAny<IDictionary<string, object>?>() ), Times.Once );
    }

    private static bool IsTranslationCreationViewModelWithArchiveFullPath( object model, string expectedArchiveFullPath ) =>
        model is TranslationCreationViewModel translationCreationViewModel
        && translationCreationViewModel.ArchiveFullPath == expectedArchiveFullPath;

    private static ManualSelectionScenario CreateManualSelectionScenario( TranslationFileSelectionViewModel viewModel ) {
        const string directoryPath = @"C:\DCSWorld\Mods\aircraft\A10C";
        const string filePath = @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz";
        var directoryNode = new ManualFileEntryViewModel( "A10C", "A10C", true, directoryPath );
        var fileNode = new ManualFileEntryViewModel( "Mission1.miz", "A10C/Mission1.miz", false, filePath );
        directoryNode.Children.Add( fileNode );

        RegisterSelectionSubscription( viewModel, CategoryType.Aircraft, directoryNode );
        RegisterSelectionSubscription( viewModel, CategoryType.Aircraft, fileNode );
        return new ManualSelectionScenario( directoryNode, fileNode, filePath );
    }

    private static void SimulateDirectoryToFileSelectionTransition( TranslationFileSelectionViewModel viewModel, ManualSelectionScenario scenario ) {
        scenario.DirectoryNode.SetSelected( true );
        InvokeNodePropertyChanged( viewModel, scenario.DirectoryNode );

        scenario.FileNode.SetSelected( true );
        InvokeNodePropertyChanged( viewModel, scenario.FileNode );

        scenario.DirectoryNode.SetSelected( false );
        InvokeNodePropertyChanged( viewModel, scenario.DirectoryNode );
    }

    private static void RegisterSelectionSubscription( TranslationFileSelectionViewModel viewModel, CategoryType categoryType, IFileEntryViewModel node ) {
        var method = typeof( TranslationFileSelectionViewModel ).GetMethod( "RegisterSelectionSubscription", BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new MissingMethodException( typeof( TranslationFileSelectionViewModel ).FullName, "RegisterSelectionSubscription" );
        method.Invoke( viewModel, [categoryType, node] );
    }

    private static void InvokeNodePropertyChanged( TranslationFileSelectionViewModel viewModel, IFileEntryViewModel node ) {
        var method = typeof( TranslationFileSelectionViewModel ).GetMethod( "OnNodePropertyChanged", BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new MissingMethodException( typeof( TranslationFileSelectionViewModel ).FullName, "OnNodePropertyChanged" );
        method.Invoke( viewModel, [node, new PropertyChangedEventArgs( nameof( IFileEntryViewModel.IsSelected ) )] );
    }

    private sealed record ManualSelectionScenario(
        ManualFileEntryViewModel DirectoryNode,
        ManualFileEntryViewModel FileNode,
        string FilePath );

    private sealed class ManualFileEntryViewModel( string name, string path, bool isDirectory, string localSha ) : PropertyChangedBase, IFileEntryViewModel {
        public event EventHandler<bool?>? CheckStateChanged {
            add {
            }
            remove {
            }
        }

        public string Name { get; } = name;

        public string Path { get; } = path;

        public bool IsDirectory { get; } = isDirectory;

        public FileEntry Model { get; } = new LocalFileEntry( name, path, isDirectory, localSha );

        public FileChangeType? ChangeType => FileChangeType.Modified;

        public bool CanCheck => true;

        public bool? CheckState { get; set; }

        public bool IsSelected { get; set; }

        public bool IsExpanded { get; set; }

        public bool IsVisible { get; set; } = true;

        public ObservableCollection<IFileEntryViewModel> Children { get; set; } = [];

        public void SetSelectRecursive( bool value ) => IsSelected = value;

        public List<FileEntry> GetCheckedModelRecursive( bool fileOnly = false ) => [];

        public List<IFileEntryViewModel> GetCheckedViewModelRecursive() => [];

        public void Dispose() {
        }

        internal void SetSelected( bool value ) {
            IsSelected = value;
            NotifyOfPropertyChange( nameof( IsSelected ) );
        }
    }

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
        internal Mock<IApplicationInfoService> ApplicationInfoServiceMock { get; } = new();
        internal Mock<IDialogService> DialogServiceMock { get; } = new();
        internal Mock<IDialogProvider> DialogProviderMock { get; } = new();
        internal Mock<IDispatcherService> DispatcherServiceMock { get; } = new();
        internal Mock<ILoggingService> LoggerMock { get; } = new();
        internal Mock<ISnackbarService> SnackbarServiceMock { get; } = new();
        internal Mock<ISystemService> SystemServiceMock { get; } = new();
        internal Mock<ITranslationCreationViewModelFactory> TranslationCreationViewModelFactoryMock { get; } = new();
        internal Mock<ITranslationDictionaryService> TranslationDictionaryServiceMock { get; } = new();
        internal Mock<IWindowManager> WindowManagerMock { get; } = new();
        internal Mock<ITranslationArchiveDiscoveryService> DiscoveryServiceMock { get; } = new();

        internal TranslationFileSelectionViewModel CreateViewModel() =>
            new(
                AppSettingsServiceMock.Object,
                DispatcherServiceMock.Object,
                LoggerMock.Object,
                SnackbarServiceMock.Object,
                SystemServiceMock.Object,
                TranslationCreationViewModelFactoryMock.Object,
                WindowManagerMock.Object,
                DiscoveryServiceMock.Object );
    }
}