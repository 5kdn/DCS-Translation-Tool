using System.Windows;
using System.Windows.Controls;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;
using DcsTranslationTool.TestCommon.Reflection;
using DcsTranslationTool.TestCommon.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationFileSelection;

/// <summary>
/// <see cref="TranslationFileSelectionViewModel"/> の動作を検証する。
/// </summary>
public sealed class TranslationFileSelectionViewModelTests {
    [StaFact]
    public async Task ActivateAsyncは読み込み結果を画面状態へ反映する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Equal( 3, viewModel.Tabs.Count );
        Assert.Equal( 0, viewModel.SelectedTabIndex );
        context.WorkflowUiAdapterMock.Verify(
            adapter => adapter.ApplyLoadResultAsync(
                It.Is<TranslationFileSelectionLoadResult>( result => result.Tabs.Count == 3 ),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ),
            Times.Once );
    }

    [StaFact]
    public async Task 設定案内メッセージを反映する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( CreateTabs( context.LoggerMock.Object ), Strings_Translation.SettingsNotConfiguredMessage ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Equal( Strings_Translation.SettingsNotConfiguredMessage, viewModel.CurrentStatusMessage );
    }

    [StaFact]
    public async Task Refreshは通知メッセージ表示をUIアダプタへ委譲する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult(
                CreateTabs( context.LoggerMock.Object ),
                Strings_Translation.LoadFailedMessage,
                Strings_Translation.LoadFailedMessage ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();

        await viewModel.Refresh();

        context.WorkflowUiAdapterMock.Verify(
            adapter => adapter.ApplyLoadResultAsync(
                It.Is<TranslationFileSelectionLoadResult>( result => result.NotificationMessage == Strings_Translation.LoadFailedMessage ),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ),
            Times.Once );
    }

    [StaFact]
    public async Task 選択状態変化時にガードプロパティが更新される() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        var fileNode = (FileEntryViewModel)tabs[0].Root.Children[0].Children[0];
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );
        fileNode.IsSelected = true;

        Assert.True( viewModel.HasSelectedEntry );
        Assert.True( viewModel.CanOpenDirectory );
        Assert.True( viewModel.CanCreateTranslation );
    }

    [StaFact]
    public async Task ディレクトリ解除時に選択済み子ファイルへフォールバックする() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        var directoryNode = (FileEntryViewModel)tabs[0].Root.Children[0];
        var fileNode = (FileEntryViewModel)directoryNode.Children[0];
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );
        directoryNode.IsSelected = true;
        fileNode.IsSelected = true;
        directoryNode.IsSelected = false;

        Assert.True( viewModel.CanCreateTranslation );
        viewModel.OpenDirectory();

        context.ActionServiceMock.Verify( service => service.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ), Times.Once );
    }

    [StaFact]
    public async Task TreeView選択バインディング経由でもディレクトリ解除時に子ファイルへフォールバックする() {
        WpfTestHelper.EnsureApplicationResources(
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml",
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Button.xaml",
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.PopupBox.xaml",
            "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.SplitButton.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/CustomBrushes.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/_Thickness.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/WindowStyle.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/ButtonStyle.xaml",
            "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/TreeViewStyle.xaml" );

        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );

        var viewModel = context.CreateViewModel();
        var view = new TranslationFileSelectionView
        {
            DataContext = viewModel
        };
        var hostWindow = new Window
        {
            Content = view,
            Width = 1024,
            Height = 768,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false
        };

        try {
            hostWindow.Show();
            await viewModel.ActivateAsync( CancellationToken.None );
            WpfTestHelper.PumpDispatcher();

            var treeView = WpfTestHelper.GetRequiredDescendantAfterLayout<TreeView>( view );
            var directoryItem = WpfTestHelper.GetTreeViewItemAt<TreeViewItem>( treeView, 0 );
            directoryItem.IsSelected = true;
            WpfTestHelper.PumpDispatcher();

            Assert.True( viewModel.HasSelectedEntry );
            Assert.True( viewModel.CanOpenDirectory );
            Assert.False( viewModel.CanCreateTranslation );

            directoryItem.IsExpanded = true;
            WpfTestHelper.PumpDispatcher();

            var fileItem = WpfTestHelper.GetTreeViewItemAt<TreeViewItem>( directoryItem, 0 );
            fileItem.IsSelected = true;
            WpfTestHelper.PumpDispatcher();

            Assert.True( viewModel.CanCreateTranslation );

            directoryItem.IsSelected = false;
            WpfTestHelper.PumpDispatcher();

            Assert.True( viewModel.CanCreateTranslation );

            viewModel.OpenDirectory();

            context.ActionServiceMock.Verify( service => service.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ), Times.Once );
        }
        finally {
            hostWindow.Close();
        }
    }

    [StaFact]
    public async Task OpenDirectoryはaction_serviceへ委譲する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        var fileNode = (FileEntryViewModel)tabs[0].Root.Children[0].Children[0];
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );
        fileNode.IsSelected = true;

        viewModel.OpenDirectory();

        context.ActionServiceMock.Verify( service => service.OpenDirectory( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ), Times.Once );
    }

    [StaFact]
    public async Task CreateTranslationはaction_serviceへ委譲する() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        var fileNode = (FileEntryViewModel)tabs[0].Root.Children[0].Children[0];
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );
        fileNode.IsSelected = true;

        await viewModel.CreateTranslation();

        context.ActionServiceMock.Verify( service => service.OpenTranslationCreationAsync( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ), Times.Once );
    }

    [StaFact]
    public async Task OnDeactivateAsyncは通知クリアと選択購読解除を行う() {
        var context = new TranslationFileSelectionViewModelTestContext();
        var tabs = CreateTabs( context.LoggerMock.Object );
        var fileNode = (FileEntryViewModel)tabs[0].Root.Children[0].Children[0];
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationFileSelectionLoadResult( tabs, string.Empty ) );
        context.WorkflowUiAdapterMock
            .Setup( adapter => adapter.ApplyLoadResultAsync(
                It.IsAny<TranslationFileSelectionLoadResult>(),
                It.IsAny<Action<TranslationFileSelectionLoadResult>>() ) )
            .Returns<TranslationFileSelectionLoadResult, Action<TranslationFileSelectionLoadResult>>( ( result, apply ) => {
                apply( result );
                return Task.CompletedTask;
            } );
        var viewModel = context.CreateViewModel();
        await viewModel.ActivateAsync( CancellationToken.None );

        await ReflectionTestHelper.InvokeNonPublicTaskAsync( viewModel, "OnDeactivateAsync", true, CancellationToken.None );
        fileNode.IsSelected = true;

        Assert.False( viewModel.HasSelectedEntry );
        context.ActionServiceMock.Verify( service => service.ClearNotifications(), Times.Once );
    }

    /// <summary>
    /// テスト用タブ一覧を生成する。
    /// </summary>
    /// <param name="logger">ロギングサービス。</param>
    /// <returns>生成したタブ一覧。</returns>
    private static IReadOnlyList<TabItemViewModel> CreateTabs( ILoggingService logger ) =>
    [
        new( CategoryType.Aircraft, logger, CreateRootNode( logger, "Aircraft", @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ) ),
        new( CategoryType.DlcCampaigns, logger, CreateRootNode( logger, "Campaigns", @"C:\DCSWorld\Mods\campaigns\Campaign1.trk" ) ),
        new( CategoryType.UserMissions, logger, CreateRootNode( logger, "UserMissions", @"C:\Users\User\Saved Games\DCS\Missions\UserMission.lua" ) )
    ];

    /// <summary>
    /// テスト用ルートノードを生成する。
    /// </summary>
    /// <param name="logger">ロギングサービス。</param>
    /// <param name="directoryName">ディレクトリ名。</param>
    /// <param name="filePath">ファイル絶対パス。</param>
    /// <returns>生成したルートノード。</returns>
    private static FileEntryViewModel CreateRootNode( ILoggingService logger, string directoryName, string filePath ) {
        var fileNode = new FileEntryViewModel(
            new LocalFileEntry( Path.GetFileName( filePath ), $"{directoryName}/{Path.GetFileName( filePath )}", false, filePath ),
            ChangeTypeMode.Upload,
            logger );
        var directoryNode = new FileEntryViewModel(
            new LocalFileEntry( directoryName, directoryName, true, Path.GetDirectoryName( filePath ) ),
            ChangeTypeMode.Upload,
            logger );
        directoryNode.Children.Add( fileNode );

        var rootNode = new FileEntryViewModel(
            new LocalFileEntry( "Root", string.Empty, true ),
            ChangeTypeMode.Upload,
            logger );
        rootNode.Children.Add( directoryNode );
        return rootNode;
    }

    /// <summary>
    /// テストコンテキストを表す。
    /// </summary>
    private sealed class TranslationFileSelectionViewModelTestContext {
        internal Mock<ILoggingService> LoggerMock { get; } = new();
        internal Mock<ITranslationFileSelectionActionService> ActionServiceMock { get; } = new();
        internal Mock<ITranslationFileSelectionWorkflowService> WorkflowServiceMock { get; } = new();
        internal Mock<ITranslationFileSelectionWorkflowUiAdapter> WorkflowUiAdapterMock { get; } = new();

        internal TranslationFileSelectionViewModel CreateViewModel() =>
            new(
                LoggerMock.Object,
                ActionServiceMock.Object,
                WorkflowServiceMock.Object,
                WorkflowUiAdapterMock.Object );
    }

}