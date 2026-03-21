using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

/// <summary>
/// Translation File Selection ページの状態を管理する ViewModel である。
/// </summary>
public sealed class TranslationFileSelectionViewModel(
    ILoggingService logger,
    ITranslationFileSelectionActionService translationFileSelectionActionService,
    ITranslationFileSelectionWorkflowService translationFileSelectionWorkflowService,
    ITranslationFileSelectionWorkflowUiAdapter workflowUiAdapter
) : Screen, IActivate {
    private ObservableCollection<TabItemViewModel> _tabs = [];
    private int _selectedTabIndex;
    private bool _isLoading;
    private string _stateMessage = string.Empty;
    private TranslationFileSelectionSelectionState _selectionState = new( false, false, false, null, null );
    private readonly Dictionary<CategoryType, IFileEntryViewModel?> _selectedNodes = [];
    private readonly Dictionary<IFileEntryViewModel, CategoryType> _nodeCategories = [];
    private readonly List<INotifyPropertyChanged> _selectionSubscriptions = [];

    /// <summary>
    /// タブ一覧を取得または設定する。
    /// </summary>
    public ObservableCollection<TabItemViewModel> Tabs {
        get => _tabs;
        private set => Set( ref _tabs, value );
    }

    /// <summary>
    /// 選択中タブのインデックスを取得または設定する。
    /// </summary>
    public int SelectedTabIndex {
        get => _selectedTabIndex;
        set {
            if(!Set( ref _selectedTabIndex, value )) {
                return;
            }

            UpdateSelectionState();
        }
    }

    /// <summary>
    /// 読み込み中かどうかを取得または設定する。
    /// </summary>
    public bool IsLoading {
        get => _isLoading;
        private set {
            if(!Set( ref _isLoading, value )) {
                return;
            }

            NotifyOfPropertyChange( nameof( CanRefresh ) );
            NotifyOfPropertyChange( nameof( CanOpenDirectory ) );
            NotifyOfPropertyChange( nameof( CanCreateTranslation ) );
        }
    }

    /// <summary>
    /// 再読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanRefresh => !IsLoading;

    /// <summary>
    /// 現在タブに選択済みノードがあるかどうかを取得する。
    /// </summary>
    public bool HasSelectedEntry => _selectionState.HasSelectedEntry;

    /// <summary>
    /// フォルダーを開く操作が可能かどうかを取得する。
    /// </summary>
    public bool CanOpenDirectory => !IsLoading && _selectionState.CanOpenDirectory;

    /// <summary>
    /// 翻訳作成ウィンドウを表示可能かどうかを取得する。
    /// </summary>
    public bool CanCreateTranslation => !IsLoading && _selectionState.CanCreateTranslation;

    /// <summary>
    /// 現在表示すべき状態メッセージを取得する。
    /// </summary>
    public string CurrentStatusMessage {
        get {
            if(!string.IsNullOrWhiteSpace( _stateMessage )) {
                return _stateMessage;
            }

            return IsCurrentTabEmpty ? Strings_Translation.EmptyMessage : string.Empty;
        }
    }

    /// <summary>
    /// 状態メッセージを表示すべきかどうかを取得する。
    /// </summary>
    public bool HasCurrentStatusMessage => !string.IsNullOrWhiteSpace( CurrentStatusMessage );

    /// <summary>
    /// 現在タブが空かどうかを取得する。
    /// </summary>
    public bool IsCurrentTabEmpty => GetSelectedTab()?.Root.Children.Count == 0;

    /// <summary>
    /// アクティブ化時に初期読み込みを行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( "TranslationFileSelectionViewModel をアクティブ化する。" );
        await RefreshCoreAsync( cancellationToken );
    }

    /// <summary>
    /// 非アクティブ化時に購読を解除する。
    /// </summary>
    /// <param name="close">画面を閉じるかどうか。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"TranslationFileSelectionViewModel を非アクティブ化する。Close={close}" );
        UnsubscribeSelectionStateChanged();
        translationFileSelectionActionService.ClearNotifications();
        await base.OnDeactivateAsync( close, cancellationToken );
    }

    /// <summary>
    /// 翻訳対象アーカイブ一覧を再読み込みする。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public new Task Refresh() => RefreshCoreAsync( CancellationToken.None );

    /// <summary>
    /// 選択中ノードの存在するディレクトリを開く。
    /// </summary>
    public void OpenDirectory() {
        if(_selectionState.SelectedEntryPath is not { Length: > 0 } path) {
            logger.Warn( "選択ノードが存在しないためフォルダーを開く処理を中断する。" );
            return;
        }

        translationFileSelectionActionService.OpenDirectory( path );
    }

    /// <summary>
    /// 翻訳作成ウィンドウを表示する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task CreateTranslation() {
        var archiveFullPath = _selectionState.SelectedArchiveFullPath;
        if(archiveFullPath is null) {
            logger.Warn( "選択ノードが存在しないため翻訳作成ウィンドウ表示を中断する。" );
            return;
        }

        await translationFileSelectionActionService.OpenTranslationCreationAsync( archiveFullPath );
    }

    /// <summary>
    /// 一覧再読み込みの本体処理を実行する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    private async Task RefreshCoreAsync( CancellationToken cancellationToken ) {
        if(IsLoading) {
            logger.Info( "読み込み中のため再読み込み要求を無視する。" );
            return;
        }

        logger.Info( "翻訳対象アーカイブ一覧の読み込みを開始する。" );
        IsLoading = true;

        try {
            var loadResult = await translationFileSelectionWorkflowService.LoadAsync( cancellationToken );
            await workflowUiAdapter.ApplyLoadResultAsync( loadResult, ApplyLoadResult );
        }
        catch(OperationCanceledException) {
            logger.Warn( "翻訳対象アーカイブ一覧の読み込みがキャンセルされた。" );
            throw;
        }
        finally {
            IsLoading = false;
            logger.Info( "翻訳対象アーカイブ一覧の読み込みを終了する。" );
        }
    }

    /// <summary>
    /// 読み込み結果を画面状態へ反映する。
    /// </summary>
    /// <param name="loadResult">反映対象の読み込み結果。</param>
    private void ApplyLoadResult( TranslationFileSelectionLoadResult loadResult ) {
        _stateMessage = loadResult.StatusMessage;
        UnsubscribeSelectionStateChanged();
        Tabs = [.. loadResult.Tabs];
        RegisterSelectionSubscriptions( Tabs );
        SelectedTabIndex = Tabs.Count == 0 ? 0 : Math.Clamp( SelectedTabIndex, 0, Tabs.Count - 1 );
        UpdateSelectionState();
    }

    /// <summary>
    /// 現在の選択状態を更新する。
    /// </summary>
    private void UpdateSelectionState() {
        _selectionState = GetSelectionState( GetSelectedCategoryType() );
        logger.Info( $"TranslationFileSelection の選択状態を更新する。SelectedIndex={SelectedTabIndex}" );
        NotifySelectionState();
    }

    /// <summary>
    /// タブ配下ノードの選択状態購読を登録する。
    /// </summary>
    /// <param name="tabs">購読対象タブ一覧。</param>
    private void RegisterSelectionSubscriptions( IEnumerable<TabItemViewModel> tabs ) {
        foreach(var tab in tabs) {
            _selectedNodes[tab.TabType] = null;
            RegisterSelectionSubscription( tab.TabType, tab.Root );
        }
    }

    /// <summary>
    /// タブ配下ノードの選択状態購読を解除する。
    /// </summary>
    private void UnsubscribeSelectionStateChanged() {
        foreach(var subscription in _selectionSubscriptions) {
            subscription.PropertyChanged -= OnNodePropertyChanged;
        }

        _selectionSubscriptions.Clear();
        _selectedNodes.Clear();
        _nodeCategories.Clear();
    }

    /// <summary>
    /// ノード選択状態の購読を登録する。
    /// </summary>
    /// <param name="categoryType">所属カテゴリ。</param>
    /// <param name="node">購読対象ノード。</param>
    private void RegisterSelectionSubscription( CategoryType categoryType, IFileEntryViewModel node ) {
        if(node is INotifyPropertyChanged notifyPropertyChanged) {
            notifyPropertyChanged.PropertyChanged += OnNodePropertyChanged;
            _selectionSubscriptions.Add( notifyPropertyChanged );
        }

        _nodeCategories[node] = categoryType;
        if(node.IsSelected) {
            _selectedNodes[categoryType] = node;
        }

        foreach(var child in node.Children) {
            RegisterSelectionSubscription( categoryType, child );
        }
    }

    /// <summary>
    /// ノードのプロパティ変更を処理する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnNodePropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName != nameof( IFileEntryViewModel.IsSelected )
            || sender is not IFileEntryViewModel node) {
            return;
        }

        if(!_nodeCategories.TryGetValue( node, out var categoryType )) {
            return;
        }

        if(node.IsSelected) {
            if(node.IsDirectory) {
                _selectedNodes[categoryType] = node;
            }
            else if(!_selectedNodes.TryGetValue( categoryType, out var selectedNode ) || selectedNode?.IsDirectory != true) {
                _selectedNodes[categoryType] = node;
            }
        }
        else if(_selectedNodes.TryGetValue( categoryType, out var selectedNode )
            && ReferenceEquals( selectedNode, node )) {
            _selectedNodes[categoryType] = node.IsDirectory
                ? FindSelectedFileDescendant( node )
                : null;
        }

        logger.Info( $"TranslationFileSelection のノード選択状態が変化した。Category={categoryType}" );
        if(categoryType == GetSelectedCategoryType()) {
            UpdateSelectionState();
        }
    }

    /// <summary>
    /// 指定カテゴリの現在選択状態を取得する。
    /// </summary>
    /// <param name="categoryType">取得対象カテゴリ。</param>
    /// <returns>現在選択状態。</returns>
    private TranslationFileSelectionSelectionState GetSelectionState( CategoryType? categoryType ) {
        if(categoryType is null || !_selectedNodes.TryGetValue( categoryType.Value, out var selectedNode )) {
            return new TranslationFileSelectionSelectionState( false, false, false, null, null );
        }

        var selectedEntryPath = string.IsNullOrWhiteSpace( selectedNode?.Model.LocalSha )
            ? null
            : selectedNode.Model.LocalSha;
        var selectedArchiveFullPath = GetSelectedArchiveFullPath( selectedNode );

        return new TranslationFileSelectionSelectionState(
            selectedNode is not null,
            selectedEntryPath is not null,
            selectedArchiveFullPath is not null,
            selectedEntryPath,
            selectedArchiveFullPath );
    }

    /// <summary>
    /// 現在の選択関連状態を通知する。
    /// </summary>
    private void NotifySelectionState() {
        NotifyOfPropertyChange( nameof( HasSelectedEntry ) );
        NotifyOfPropertyChange( nameof( CanOpenDirectory ) );
        NotifyOfPropertyChange( nameof( CanCreateTranslation ) );
        NotifyOfPropertyChange( nameof( IsCurrentTabEmpty ) );
        NotifyOfPropertyChange( nameof( CurrentStatusMessage ) );
        NotifyOfPropertyChange( nameof( HasCurrentStatusMessage ) );
    }

    /// <summary>
    /// 現在選択中のタブを取得する。
    /// </summary>
    /// <returns>選択中タブ。未選択時は <see langword="null"/>。</returns>
    private TabItemViewModel? GetSelectedTab() =>
        Tabs.Count > 0 && SelectedTabIndex >= 0 && SelectedTabIndex < Tabs.Count
            ? Tabs[SelectedTabIndex]
            : null;

    /// <summary>
    /// 現在選択中のカテゴリを取得する。
    /// </summary>
    /// <returns>選択中カテゴリ。未選択時は <see langword="null"/>。</returns>
    private CategoryType? GetSelectedCategoryType() => GetSelectedTab()?.TabType;

    /// <summary>
    /// 指定ディレクトリ配下で選択状態のファイルを探索する。
    /// </summary>
    /// <param name="node">探索起点ノード。</param>
    /// <returns>選択状態のファイルノード。存在しない場合は <see langword="null"/>。</returns>
    private static IFileEntryViewModel? FindSelectedFileDescendant( IFileEntryViewModel node ) {
        foreach(var child in node.Children) {
            if(child.IsSelected && !child.IsDirectory) {
                return child;
            }

            var descendant = FindSelectedFileDescendant( child );
            if(descendant is not null) {
                return descendant;
            }
        }

        return null;
    }

    /// <summary>
    /// 翻訳作成対象のアーカイブ絶対パスを取得する。
    /// </summary>
    /// <param name="selectedNode">選択中ノード。</param>
    /// <returns>対象絶対パス。対象外または未選択時は <see langword="null"/>。</returns>
    private static string? GetSelectedArchiveFullPath( IFileEntryViewModel? selectedNode ) {
        if(selectedNode?.IsDirectory != false || string.IsNullOrWhiteSpace( selectedNode.Model.LocalSha )) {
            return null;
        }

        var extension = Path.GetExtension( selectedNode.Name );
        return extension.Equals( ".miz", StringComparison.OrdinalIgnoreCase )
            || extension.Equals( ".trk", StringComparison.OrdinalIgnoreCase )
            ? selectedNode.Model.LocalSha
            : null;
    }
}