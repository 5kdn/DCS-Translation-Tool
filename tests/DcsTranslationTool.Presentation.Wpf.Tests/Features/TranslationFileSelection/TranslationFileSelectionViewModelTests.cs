using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationFileSelection;

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
        EnsureApplicationResources();

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
            PumpDispatcher();

            var treeView = GetTreeView( view );
            var directoryItem = GetTreeViewItemAt( treeView, 0 );
            directoryItem.IsSelected = true;
            PumpDispatcher();

            Assert.True( viewModel.HasSelectedEntry );
            Assert.True( viewModel.CanOpenDirectory );
            Assert.False( viewModel.CanCreateTranslation );

            directoryItem.IsExpanded = true;
            PumpDispatcher();

            var fileItem = GetTreeViewItemAt( directoryItem, 0 );
            fileItem.IsSelected = true;
            PumpDispatcher();

            Assert.True( viewModel.CanCreateTranslation );

            directoryItem.IsSelected = false;
            PumpDispatcher();

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

        await InvokeOnDeactivateAsync( viewModel, true );
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

    /// <summary>
    /// OnDeactivateAsync をリフレクション経由で実行する。
    /// </summary>
    /// <param name="viewModel">対象 ViewModel。</param>
    /// <param name="close">画面を閉じるかどうか。</param>
    /// <returns>非同期タスク。</returns>
    private static Task InvokeOnDeactivateAsync( TranslationFileSelectionViewModel viewModel, bool close ) {
        var method = typeof( TranslationFileSelectionViewModel ).GetMethod( "OnDeactivateAsync", BindingFlags.Instance | BindingFlags.NonPublic )
            ?? throw new InvalidOperationException( "OnDeactivateAsync が見つからない。" );
        return (Task)(method.Invoke( viewModel, [close, CancellationToken.None] ) ?? Task.CompletedTask);
    }

    /// <summary>
    /// WPF テスト実行に必要なアプリケーションリソースを初期化する。
    /// </summary>
    private static void EnsureApplicationResources() {
        if(System.Windows.Application.Current is null) {
            _ = new System.Windows.Application();
        }

        var resources = System.Windows.Application.Current!.Resources.MergedDictionaries;
        if(resources.Any( dictionary => dictionary.Source?.OriginalString.Contains( "TreeViewStyle.xaml", StringComparison.OrdinalIgnoreCase ) == true )) {
            return;
        }

        AddMergedDictionary( resources, "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesign3.Defaults.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.Button.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.PopupBox.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/MaterialDesignThemes.Wpf;component/Themes/MaterialDesignTheme.SplitButton.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/CustomBrushes.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/_Thickness.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/WindowStyle.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/ButtonStyle.xaml" );
        AddMergedDictionary( resources, "pack://application:,,,/DcsTranslationTool.Presentation.Wpf;component/Assets/Styles/TreeViewStyle.xaml" );
    }

    /// <summary>
    /// マージ辞書を追加する。
    /// </summary>
    /// <param name="dictionaries">追加先辞書一覧。</param>
    /// <param name="source">辞書ソース URI。</param>
    private static void AddMergedDictionary( ICollection<ResourceDictionary> dictionaries, string source ) {
        dictionaries.Add( new ResourceDictionary
        {
            Source = new Uri( source, UriKind.Absolute )
        } );
    }

    /// <summary>
    /// TranslationFileSelectionView 配下の TreeView を取得する。
    /// </summary>
    /// <param name="view">対象ビュー。</param>
    /// <returns>取得した TreeView。</returns>
    private static TreeView GetTreeView( TranslationFileSelectionView view ) {
        view.ApplyTemplate();
        view.UpdateLayout();
        PumpDispatcher();

        return FindDescendant<TreeView>( view )
            ?? throw new InvalidOperationException( "TreeView が見つからない。" );
    }

    /// <summary>
    /// 指定インデックスの TreeViewItem を取得する。
    /// </summary>
    /// <param name="itemsControl">取得元 ItemsControl。</param>
    /// <param name="index">対象インデックス。</param>
    /// <returns>取得した TreeViewItem。</returns>
    private static TreeViewItem GetTreeViewItemAt( ItemsControl itemsControl, int index ) {
        EnsureContainersGenerated( itemsControl );

        return itemsControl.ItemContainerGenerator.ContainerFromIndex( index ) as TreeViewItem
            ?? throw new InvalidOperationException( $"TreeViewItem の生成に失敗した。Index={index}" );
    }

    /// <summary>
    /// ItemsControl 配下のコンテナ生成を保証する。
    /// </summary>
    /// <param name="itemsControl">対象 ItemsControl。</param>
    private static void EnsureContainersGenerated( ItemsControl itemsControl ) {
        itemsControl.ApplyTemplate();
        itemsControl.UpdateLayout();
        PumpDispatcher();

        if(itemsControl is TreeViewItem treeViewItem) {
            treeViewItem.IsExpanded = true;
            treeViewItem.UpdateLayout();
            PumpDispatcher();
        }
    }

    /// <summary>
    /// VisualTree から指定型の子要素を探索する。
    /// </summary>
    /// <typeparam name="T">探索対象型。</typeparam>
    /// <param name="root">探索起点。</param>
    /// <returns>最初に見つかった要素。存在しない場合は <see langword="null"/>。</returns>
    private static T? FindDescendant<T>( DependencyObject root )
        where T : DependencyObject {
        var childrenCount = VisualTreeHelper.GetChildrenCount( root );
        for(var index = 0; index < childrenCount; index++) {
            var child = VisualTreeHelper.GetChild( root, index );
            if(child is T target) {
                return target;
            }

            var descendant = FindDescendant<T>( child );
            if(descendant is not null) {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// Dispatcher キューを処理する。
    /// </summary>
    private static void PumpDispatcher() =>
        Dispatcher.CurrentDispatcher.Invoke( () => { }, DispatcherPriority.Background );
}