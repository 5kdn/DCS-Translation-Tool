using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// 翻訳作成ウィンドウの状態を管理する ViewModel である。
/// </summary>
/// <param name="archiveFullPath">翻訳対象のアーカイブ絶対パス。</param>
/// <param name="logger">ロギングサービス。</param>
/// <param name="translationDictionaryService">dictionary 読込サービス。</param>
public sealed class TranslationCreationViewModel(
    string archiveFullPath,
    ILoggingService logger,
    ITranslationDictionaryService translationDictionaryService
) : Screen {
    private ObservableCollection<TranslationDictionaryItemRowViewModel> _dictionaryItems = [];
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _showOnlyUntranslated;
    private bool _hidePossibleNonTranslationTargets = true;
    private bool _hideEmptyOriginal = true;

    /// <summary>
    /// ウィンドウの表示名を取得する。
    /// </summary>
    public string WindowTitle { get; } = Strings_Translation.CreateTranslationWindowTitle;

    /// <summary>
    /// 選択中アーカイブの絶対パスを取得する。
    /// </summary>
    public string ArchiveFullPath { get; } = string.IsNullOrWhiteSpace( archiveFullPath )
        ? throw new ArgumentException( "アーカイブ絶対パスは必須です。", nameof( archiveFullPath ) )
        : archiveFullPath;

    /// <summary>
    /// dictionary 項目一覧を取得または設定する。
    /// </summary>
    public ObservableCollection<TranslationDictionaryItemRowViewModel> DictionaryItems {
        get => _dictionaryItems;
        private set {
            if(!Set( ref _dictionaryItems, value )) {
                return;
            }

            SubscribeDictionaryItems( value );
            FilteredDictionaryItemsView = CollectionViewSource.GetDefaultView( value );
            FilteredDictionaryItemsView.Filter = FilterDictionaryItem;
            FilteredDictionaryItemsView.Refresh();
            NotifyOfPropertyChange( nameof( HasDictionaryItems ) );
        }
    }

    /// <summary>
    /// フィルター済み dictionary 項目一覧を取得または設定する。
    /// </summary>
    public ICollectionView FilteredDictionaryItemsView { get; private set; } =
        CollectionViewSource.GetDefaultView( Array.Empty<object>() );

    /// <summary>
    /// 読み込み中かどうかを取得または設定する。
    /// </summary>
    public bool IsLoading {
        get => _isLoading;
        private set => Set( ref _isLoading, value );
    }

    /// <summary>
    /// 状態メッセージを取得または設定する。
    /// </summary>
    public string StatusMessage {
        get => _statusMessage;
        private set {
            if(!Set( ref _statusMessage, value )) {
                return;
            }

            NotifyOfPropertyChange( nameof( HasStatusMessage ) );
            NotifyOfPropertyChange( nameof( HasDictionaryItems ) );
        }
    }

    /// <summary>
    /// 状態メッセージが存在するかどうかを取得する。
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace( StatusMessage );

    /// <summary>
    /// dictionary 項目が存在するかどうかを取得する。
    /// </summary>
    public bool HasDictionaryItems => DictionaryItems.Count > 0;

    /// <summary>
    /// 未翻訳のみ表示するかどうかを取得または設定する。
    /// </summary>
    public bool ShowOnlyUntranslated {
        get => _showOnlyUntranslated;
        set {
            if(!Set( ref _showOnlyUntranslated, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// 翻訳対象ではない可能性がある行を非表示にするかどうかを取得または設定する。
    /// </summary>
    public bool HidePossibleNonTranslationTargets {
        get => _hidePossibleNonTranslationTargets;
        set {
            if(!Set( ref _hidePossibleNonTranslationTargets, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// Original が空欄の行を非表示にするかどうかを取得または設定する。
    /// </summary>
    public bool HideEmptyOriginal {
        get => _hideEmptyOriginal;
        set {
            if(!Set( ref _hideEmptyOriginal, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// アクティブ化時に dictionary を読み込む。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    protected override Task OnActivateAsync( CancellationToken cancellationToken ) =>
        LoadDictionaryAsync( cancellationToken );

    private Task LoadDictionaryAsync( CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        logger.Info( $"TranslationCreationViewModel の dictionary 読込を開始する。Archive={ArchiveFullPath}" );
        IsLoading = true;
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadingMessage;

        try {
            var result = translationDictionaryService.LoadDictionary( ArchiveFullPath );
            if(result.IsFailed) {
                StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadFailedMessage;
                DictionaryItems = [];
                return Task.CompletedTask;
            }

            DictionaryItems = [
                .. result.Value
                    .OrderBy( GetDictionaryItemSortOrder )
                    .ThenBy( item => item.Key, StringComparer.Ordinal )
                    .Select( item => new TranslationDictionaryItemRowViewModel( item ) )
            ];
            StatusMessage = DictionaryItems.Count == 0
                ? Strings_Translation.CreateTranslationDictionaryEmptyMessage
                : string.Empty;
            return Task.CompletedTask;
        }
        finally {
            IsLoading = false;
            logger.Info( $"TranslationCreationViewModel の dictionary 読込を終了する。Archive={ArchiveFullPath}, Count={DictionaryItems.Count}" );
        }
    }

    private bool FilterDictionaryItem( object item ) {
        if(item is not TranslationDictionaryItemRowViewModel row) {
            return false;
        }

        if(HidePossibleNonTranslationTargets && IsPossibleNonTranslationTarget( row )) {
            return false;
        }

        if(HideEmptyOriginal && string.IsNullOrWhiteSpace( row.Original )) {
            return false;
        }

        if(ShowOnlyUntranslated && !string.IsNullOrWhiteSpace( row.Translated )) {
            return false;
        }

        return true;
    }

    private static bool IsPossibleNonTranslationTarget( TranslationDictionaryItemRowViewModel row ) {
        if(string.IsNullOrWhiteSpace( row.Original )) {
            return true;
        }

        if(!row.Key.StartsWith( "DictKey_", StringComparison.Ordinal )) {
            return true;
        }

        return row.Key.StartsWith( "DictKey_WptName_", StringComparison.Ordinal )
            || row.Key.StartsWith( "DictKey_ActionComment_", StringComparison.Ordinal )
            || row.Key.StartsWith( "DictKey_GroupName_", StringComparison.Ordinal )
            || row.Key.StartsWith( "DictKey_UnitName_", StringComparison.Ordinal );
    }

    private void RefreshFilter() => FilteredDictionaryItemsView.Refresh();

    private void SubscribeDictionaryItems( ObservableCollection<TranslationDictionaryItemRowViewModel> dictionaryItems ) {
        dictionaryItems.CollectionChanged += OnDictionaryItemsCollectionChanged;
        foreach(var item in dictionaryItems) {
            item.PropertyChanged += OnDictionaryItemPropertyChanged;
        }
    }

    private void OnDictionaryItemsCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        if(e.OldItems is not null) {
            foreach(var item in e.OldItems.OfType<TranslationDictionaryItemRowViewModel>()) {
                item.PropertyChanged -= OnDictionaryItemPropertyChanged;
            }
        }

        if(e.NewItems is not null) {
            foreach(var item in e.NewItems.OfType<TranslationDictionaryItemRowViewModel>()) {
                item.PropertyChanged += OnDictionaryItemPropertyChanged;
            }
        }

        RefreshFilter();
    }

    private void OnDictionaryItemPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.Translated )) {
            RefreshFilter();
        }
    }

    private static int GetDictionaryItemSortOrder( TranslationDictionaryItem item ) {
        if(item.Key.StartsWith( "DictKey_sortie_", StringComparison.Ordinal )) {
            return 0;
        }

        if(item.Key.StartsWith( "DictKey_descriptionText_", StringComparison.Ordinal )) {
            return 1;
        }

        if(item.Key.StartsWith( "DictKey_descriptionBlueTask_", StringComparison.Ordinal )) {
            return 2;
        }

        if(item.Key.StartsWith( "DictKey_descriptionRedTask_", StringComparison.Ordinal )) {
            return 3;
        }

        if(item.Key.StartsWith( "DictKey_descriptionNeutralsTask_", StringComparison.Ordinal )) {
            return 4;
        }

        if(item.Key.StartsWith( "DictKey_description", StringComparison.Ordinal )) {
            return 5;
        }

        if(item.Key.StartsWith( "DictKey_", StringComparison.Ordinal )) {
            return 6;
        }

        return 7;
    }
}