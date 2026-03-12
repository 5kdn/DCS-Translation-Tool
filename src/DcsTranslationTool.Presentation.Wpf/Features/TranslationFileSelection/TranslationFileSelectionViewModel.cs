using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;

using Caliburn.Micro;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

/// <summary>
/// Translation File Selection ページの状態を管理する ViewModel である。
/// </summary>
public sealed class TranslationFileSelectionViewModel(
    IAppSettingsService appSettingsService,
    IDispatcherService dispatcherService,
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService,
    ITranslationCreationViewModelFactory translationCreationViewModelFactory,
    IWindowManager windowManager,
    ITranslationArchiveDiscoveryService translationArchiveDiscoveryService
) : Screen, IActivate {
    private ObservableCollection<TabItemViewModel> _tabs = [];
    private int _selectedTabIndex;
    private bool _isLoading;
    private string _stateMessage = string.Empty;

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

            NotifySelectionState();
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
        }
    }

    /// <summary>
    /// 再読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanRefresh => !IsLoading;

    /// <summary>
    /// 現在タブに選択済みノードがあるかどうかを取得する。
    /// </summary>
    public bool HasSelectedEntry => GetSelectedNode() is not null;

    /// <summary>
    /// フォルダーを開く操作が可能かどうかを取得する。
    /// </summary>
    public bool CanOpenDirectory => !IsLoading && GetSelectedNode() is not null;

    /// <summary>
    /// 翻訳作成ウィンドウを表示可能かどうかを取得する。
    /// </summary>
    public bool CanCreateTranslation => !IsLoading && GetSelectedArchiveFullPath() is not null;

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
        snackbarService.Clear();
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
        var selectedNode = GetSelectedNode();
        if(selectedNode?.Model.LocalSha is not { Length: > 0 } path) {
            logger.Warn( "選択ノードが存在しないためフォルダーを開く処理を中断する。" );
            return;
        }

        try {
            logger.Info( $"選択ノードのディレクトリを開く。Path={path}" );
            systemService.OpenDirectory( path );
        }
        catch(Exception ex) {
            logger.Error( "選択ノードのディレクトリを開く処理に失敗した。", ex );
            snackbarService.Show( Strings_Translation.OpenDirectoryFailedMessage );
        }
    }

    /// <summary>
    /// 翻訳作成ウィンドウを表示する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task CreateTranslation() {
        var archiveFullPath = GetSelectedArchiveFullPath();
        if(archiveFullPath is null) {
            logger.Warn( "選択ノードが存在しないため翻訳作成ウィンドウ表示を中断する。" );
            return;
        }

        TranslationCreationViewModel translationCreationViewModel;

        try {
            logger.Info( $"翻訳作成 ViewModel を生成する。Archive={archiveFullPath}" );
            translationCreationViewModel = translationCreationViewModelFactory.Create( archiveFullPath );
        }
        catch(Exception ex) {
            logger.Error( $"翻訳作成 ViewModel の生成に失敗した。Archive={archiveFullPath}", ex );
            snackbarService.Show( Strings_Translation.CreateTranslationWindowOpenFailedMessage );
            return;
        }

        try {
            logger.Info( $"翻訳作成ウィンドウを表示する。Archive={archiveFullPath}" );
            await windowManager.ShowWindowAsync( translationCreationViewModel );
        }
        catch(Exception ex) {
            logger.Error( $"翻訳作成ウィンドウの表示に失敗した。Archive={archiveFullPath}", ex );
            snackbarService.Show( Strings_Translation.CreateTranslationWindowOpenFailedMessage );
        }
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
            if(IsSourceDirectoryNotConfigured()) {
                logger.Warn( "探索元ディレクトリが未設定のため読み込みを中断する。" );
                _stateMessage = Strings_Translation.SettingsNotConfiguredMessage;
                await dispatcherService.InvokeAsync( () => {
                    BuildTabs( [] );
                    NotifySelectionState();
                    return Task.CompletedTask;
                } );
                return;
            }

            var entries = await translationArchiveDiscoveryService.DiscoverAsync(
                appSettingsService.Settings.DcsWorldInstallDir,
                appSettingsService.Settings.SourceUserMissionDir,
                cancellationToken );

            await dispatcherService.InvokeAsync( () => {
                _stateMessage = string.Empty;
                BuildTabs( entries );
                NotifySelectionState();
                return Task.CompletedTask;
            } );
        }
        catch(OperationCanceledException) {
            logger.Warn( "翻訳対象アーカイブ一覧の読み込みがキャンセルされた。" );
            throw;
        }
        catch(Exception ex) {
            logger.Error( "翻訳対象アーカイブ一覧の読み込みに失敗した。", ex );
            _stateMessage = Strings_Translation.LoadFailedMessage;
            await dispatcherService.InvokeAsync( () => {
                BuildTabs( [] );
                NotifySelectionState();
                snackbarService.Show( Strings_Translation.LoadFailedMessage );
                return Task.CompletedTask;
            } );
        }
        finally {
            IsLoading = false;
            logger.Info( "翻訳対象アーカイブ一覧の読み込みを終了する。" );
        }
    }

    /// <summary>
    /// 一覧からカテゴリ別タブを構築する。
    /// </summary>
    /// <param name="entries">構築元の一覧。</param>
    private void BuildTabs( IReadOnlyList<TranslationArchiveEntry> entries ) {
        UnsubscribeSelectionStateChanged();

        var tabs = Enum
            .GetValues<CategoryType>()
            .Select( categoryType => BuildTab( categoryType, entries ) )
            .ToList();

        Tabs = [.. tabs];
        foreach(var tab in Tabs) {
            SubscribeSelectionStateChanged( tab.Root );
        }

        SelectedTabIndex = Tabs.Count == 0 ? 0 : Math.Clamp( SelectedTabIndex, 0, Tabs.Count - 1 );
    }

    /// <summary>
    /// 単一カテゴリのタブを構築する。
    /// </summary>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <param name="entries">探索結果一覧。</param>
    /// <returns>構築済みタブ。</returns>
    private TabItemViewModel BuildTab( CategoryType categoryType, IReadOnlyList<TranslationArchiveEntry> entries ) {
        IFileEntryViewModel root = new FileEntryViewModel(
            new LocalFileEntry( categoryType.GetTabTitle(), string.Empty, true ),
            UI.Enums.ChangeTypeMode.Upload,
            logger );

        var nodesByPath = new Dictionary<string, IFileEntryViewModel>( StringComparer.OrdinalIgnoreCase )
        {
            [string.Empty] = root
        };

        foreach(var entry in entries.Where( entry => MapCategory( entry.Category ) == categoryType )) {
            var parts = entry.RelativePath.Split( '/', StringSplitOptions.RemoveEmptyEntries );
            var currentPath = string.Empty;
            var currentAbsolutePath = string.Empty;
            var parent = root;

            for(var index = 0; index < parts.Length; index++) {
                var part = parts[index];
                currentPath = string.IsNullOrEmpty( currentPath ) ? part : $"{currentPath}/{part}";
                currentAbsolutePath = string.IsNullOrEmpty( currentAbsolutePath ) ? part : Path.Combine( currentAbsolutePath, part );
                if(nodesByPath.TryGetValue( currentPath, out var existingNode )) {
                    parent = existingNode;
                    continue;
                }

                var isDirectory = index < parts.Length - 1;
                var absolutePath = isDirectory
                    ? Path.Combine( GetCategoryRootPath( categoryType ), currentAbsolutePath )
                    : entry.FullPath;
                var node = new FileEntryViewModel(
                    isDirectory
                        ? new LocalFileEntry( part, currentPath, true, absolutePath )
                        : new LocalFileEntry( part, currentPath, false, absolutePath ),
                    UI.Enums.ChangeTypeMode.Upload,
                    logger );
                parent.Children.Add( node );
                nodesByPath[currentPath] = node;
                parent = node;
            }
        }

        return new TabItemViewModel( categoryType, logger, root );
    }

    /// <summary>
    /// ノード選択状態変更時に選択関連状態を更新する。
    /// </summary>
    /// ノード選択状態の購読を解除する。
    /// </summary>
    private void UnsubscribeSelectionStateChanged() {
        foreach(var tab in Tabs) {
            UnsubscribeSelectionStateChangedRecursive( tab.Root );
        }
    }

    /// <summary>
    /// ノード選択状態の購読を設定する。
    /// </summary>
    /// <param name="node">購読対象ノード。</param>
    private void SubscribeSelectionStateChanged( IFileEntryViewModel node ) {
        if(node is INotifyPropertyChanged notifyPropertyChanged) {
            notifyPropertyChanged.PropertyChanged += OnNodePropertyChanged;
        }

        foreach(var child in node.Children) {
            SubscribeSelectionStateChanged( child );
        }
    }

    /// <summary>
    /// ノード選択状態の購読を再帰解除する。
    /// </summary>
    /// <param name="node">解除対象ノード。</param>
    private void UnsubscribeSelectionStateChangedRecursive( IFileEntryViewModel node ) {
        if(node is INotifyPropertyChanged notifyPropertyChanged) {
            notifyPropertyChanged.PropertyChanged -= OnNodePropertyChanged;
        }

        foreach(var child in node.Children) {
            UnsubscribeSelectionStateChangedRecursive( child );
        }
    }

    /// <summary>
    /// ノードのプロパティ変更を処理する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnNodePropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName != nameof( IFileEntryViewModel.IsSelected )) {
            return;
        }

        logger.Info( $"TranslationFileSelection の選択状態が変化した。SelectedIndex={SelectedTabIndex}" );
        NotifySelectionState();
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
    /// 現在タブで選択されているファイルノードを取得する。
    /// </summary>
    /// <returns>選択済みファイルノード。未選択時は <see langword="null"/>。</returns>
    private IFileEntryViewModel? GetSelectedNode() {
        var root = GetSelectedTab()?.Root;
        return root is null ? null : FindSelectedNodeRecursive( root );
    }

    /// <summary>
    /// 翻訳作成対象のアーカイブ絶対パスを取得する。
    /// </summary>
    /// <returns>対象絶対パス。対象外または未選択時は <see langword="null"/>。</returns>
    private string? GetSelectedArchiveFullPath() {
        var selectedNode = GetSelectedNode();
        if(selectedNode is null
            || selectedNode.IsDirectory
            || string.IsNullOrWhiteSpace( selectedNode.Model.LocalSha )) {
            return null;
        }

        var extension = Path.GetExtension( selectedNode.Name );
        return extension.Equals( ".miz", StringComparison.OrdinalIgnoreCase )
            || extension.Equals( ".trk", StringComparison.OrdinalIgnoreCase )
            ? selectedNode.Model.LocalSha
            : null;
    }

    /// <summary>
    /// 選択されているノードを再帰的に探索する。
    /// </summary>
    /// <param name="node">探索開始ノード。</param>
    /// <returns>選択済みノード。未選択時は <see langword="null"/>。</returns>
    private static IFileEntryViewModel? FindSelectedNodeRecursive( IFileEntryViewModel node ) {
        if(node.IsSelected) {
            return node;
        }

        foreach(var child in node.Children) {
            var selected = FindSelectedNodeRecursive( child );
            if(selected is not null) {
                return selected;
            }
        }

        return null;
    }

    /// <summary>
    /// カテゴリに対応する探索ルートの絶対パスを取得する。
    /// </summary>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <returns>探索ルートの絶対パス。</returns>
    private string GetCategoryRootPath( CategoryType categoryType ) => categoryType switch
    {
        CategoryType.Aircraft => Path.Combine( appSettingsService.Settings.DcsWorldInstallDir, "Mods", "aircraft" ),
        CategoryType.DlcCampaigns => Path.Combine( appSettingsService.Settings.DcsWorldInstallDir, "Mods", "campaigns" ),
        CategoryType.UserMissions => appSettingsService.Settings.SourceUserMissionDir,
        _ => throw new ArgumentOutOfRangeException( nameof( categoryType ), categoryType, "未対応のカテゴリである。" ),
    };

    /// <summary>
    /// 探索元ディレクトリが未設定かどうかを判定する。
    /// </summary>
    /// <returns>両方未設定の場合は <see langword="true"/>。</returns>
    private bool IsSourceDirectoryNotConfigured() =>
        string.IsNullOrWhiteSpace( appSettingsService.Settings.DcsWorldInstallDir )
        && string.IsNullOrWhiteSpace( appSettingsService.Settings.SourceUserMissionDir );

    /// <summary>
    /// アプリケーション層のカテゴリを UI カテゴリへ変換する。
    /// </summary>
    /// <param name="category">変換元カテゴリ。</param>
    /// <returns>対応する UI カテゴリ。</returns>
    private static CategoryType MapCategory( TranslationArchiveCategory category ) => category switch
    {
        TranslationArchiveCategory.Aircraft => CategoryType.Aircraft,
        TranslationArchiveCategory.DlcCampaigns => CategoryType.DlcCampaigns,
        TranslationArchiveCategory.UserMissions => CategoryType.UserMissions,
        _ => throw new ArgumentOutOfRangeException( nameof( category ), category, "未対応のカテゴリである。" ),
    };
}