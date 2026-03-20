using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 画面の編集状態を管理する。
/// </summary>
internal sealed class TranslationCreationSession : PropertyChangedBase, ITranslationCreationSession {
    #region Fields

    private static readonly TimeSpan SelectedTranslatedCommitDelay = TimeSpan.FromMilliseconds( 250 );
    private ObservableCollection<TranslationDictionaryItemRowViewModel> _rows = [];
    private TranslationDictionaryItemRowViewModel? _selectedDictionaryItem;
    private string _selectedTranslated = string.Empty;
    private IReadOnlyList<TranslationDictionaryItem> _loadedDictionaryItems = [];
    private int _dirtyItemCount;
    private bool _hasPendingSelectedTranslatedEdit;
    private DispatcherTimer? _selectedTranslatedCommitTimer;
    #endregion

    #region Properties

    /// <inheritdoc />
    public ObservableCollection<TranslationDictionaryItemRowViewModel> Rows => _rows;

    /// <inheritdoc />
    public TranslationDictionaryItemRowViewModel? SelectedDictionaryItem {
        get => _selectedDictionaryItem;
        set {
            FlushPendingSelectedTranslatedEdit();
            if(!Set( ref _selectedDictionaryItem, value )) {
                return;
            }

            SyncSelectedTranslatedFromSelection();
            NotifyOfPropertyChange( nameof( SelectedOriginal ) );
            NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
        }
    }

    /// <inheritdoc />
    public string SelectedOriginal => SelectedDictionaryItem?.Original ?? string.Empty;

    /// <inheritdoc />
    public string SelectedTranslated {
        get => _selectedTranslated;
        set {
            if(SelectedDictionaryItem?.IsEnabled != true) {
                return;
            }

            if(string.Equals( _selectedTranslated, value, StringComparison.Ordinal )) {
                return;
            }

            _selectedTranslated = value;
            NotifyOfPropertyChange();
            ScheduleSelectedTranslatedCommit();
        }
    }

    /// <inheritdoc />
    public bool CanEditSelectedTranslated => SelectedDictionaryItem?.IsEnabled == true;

    /// <inheritdoc />
    public bool HasLoadedItems => _loadedDictionaryItems.Count > 0;

    /// <inheritdoc />
    public event EventHandler<TranslationCreationRowPropertyChangedEventArgs>? RowPropertyChanged;
    #endregion

    #region PublicMethods

    /// <inheritdoc />
    public bool HasAnyTranslatedText() => Rows.Any( item => !string.IsNullOrWhiteSpace( item.Translated ) );

    /// <inheritdoc />
    public bool HasPendingChangesForClose() =>
        HasPendingChangesForClose( SelectedDictionaryItem, _selectedTranslated );

    /// <inheritdoc />
    public void Load( TranslationCreationDictionaryLoadState state ) {
        UnsubscribeRows( _rows );
        _loadedDictionaryItems = state.LoadedItems;
        _rows = [.. state.RowStates.Select( static state => new TranslationDictionaryItemRowViewModel( state.Item, state.IsPossibleNonTranslationTarget ) )];
        SubscribeRows( _rows );
        ResetDirtyState();
        SelectedDictionaryItem = null;
        NotifyOfPropertyChange( nameof( Rows ) );
    }

    /// <inheritdoc />
    public void FlushPendingSelectedTranslatedEdit() {
        if(!_hasPendingSelectedTranslatedEdit) {
            return;
        }

        CancelSelectedTranslatedCommit();
        if(SelectedDictionaryItem?.IsEnabled != true) {
            SyncSelectedTranslatedFromSelection();
            return;
        }

        if(string.Equals( SelectedDictionaryItem.Translated, _selectedTranslated, StringComparison.Ordinal )) {
            return;
        }

        SelectedDictionaryItem.Translated = _selectedTranslated;
    }

    /// <inheritdoc />
    public bool MoveSelection( ICollectionView filteredItemsView, int offset ) {
        var visibleItems = filteredItemsView
            .Cast<TranslationDictionaryItemRowViewModel>()
            .ToArray();
        if(visibleItems.Length == 0) {
            return false;
        }

        if(SelectedDictionaryItem is null) {
            SelectedDictionaryItem = offset < 0
                ? visibleItems[^1]
                : visibleItems[0];
            return true;
        }

        var currentIndex = Array.IndexOf( visibleItems, SelectedDictionaryItem );
        if(currentIndex < 0) {
            SelectedDictionaryItem = offset < 0
                ? visibleItems[^1]
                : visibleItems[0];
            return true;
        }

        var nextIndex = currentIndex + offset;
        if(nextIndex < 0 || nextIndex >= visibleItems.Length) {
            return false;
        }

        SelectedDictionaryItem = visibleItems[nextIndex];
        return true;
    }

    /// <inheritdoc />
    public TranslationCreationDocumentSnapshot CreateDocumentSnapshot() =>
        new(
            [.. Rows.Select( item => new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Translated,
                IsEnabled = item.IsEnabled
            } )] );
    #endregion

    #region PrivateHelpers

    /// <summary>
    /// 読み込み基準と比較して dirty 行が存在するかどうかを判定する。
    /// </summary>
    /// <returns>dirty 行が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasDirtyRows() {
        if(_loadedDictionaryItems.Count != Rows.Count) {
            return true;
        }

        return _dirtyItemCount > 0;
    }

    /// <summary>
    /// 選択中詳細編集の保留値を加味して未反映変更が存在するかどうかを判定する。
    /// </summary>
    /// <param name="selectedRow">選択中行。</param>
    /// <param name="selectedTranslated">保留中の翻訳文。</param>
    /// <returns>未反映変更が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasPendingChangesForClose( TranslationDictionaryItemRowViewModel? selectedRow, string selectedTranslated ) {
        if(selectedRow is null || !_hasPendingSelectedTranslatedEdit || !selectedRow.IsEnabled) {
            return HasDirtyRows();
        }

        if(selectedRow.HasPendingChangesWithTranslatedOverride( selectedTranslated )) {
            return true;
        }

        return HasDirtyRows() && !selectedRow.HasPendingChanges;
    }

    /// <summary>
    /// 行コレクションの変更監視を開始する。
    /// </summary>
    /// <param name="rows">監視対象の行コレクション。</param>
    private void SubscribeRows( ObservableCollection<TranslationDictionaryItemRowViewModel> rows ) {
        rows.CollectionChanged += OnRowsCollectionChanged;
        foreach(var item in rows) {
            item.PropertyChanged += OnRowPropertyChanged;
        }
    }

    /// <summary>
    /// 行コレクションの変更監視を解除する。
    /// </summary>
    /// <param name="rows">解除対象の行コレクション。</param>
    private void UnsubscribeRows( ObservableCollection<TranslationDictionaryItemRowViewModel> rows ) {
        rows.CollectionChanged -= OnRowsCollectionChanged;
        foreach(var item in rows) {
            item.PropertyChanged -= OnRowPropertyChanged;
        }
    }

    /// <summary>
    /// 行コレクション変更時に行監視を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRowsCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        if(e.OldItems is not null) {
            foreach(var item in e.OldItems.OfType<TranslationDictionaryItemRowViewModel>()) {
                item.PropertyChanged -= OnRowPropertyChanged;
            }
        }

        if(e.NewItems is not null) {
            foreach(var item in e.NewItems.OfType<TranslationDictionaryItemRowViewModel>()) {
                item.PropertyChanged += OnRowPropertyChanged;
            }
        }
    }

    /// <summary>
    /// 行の状態変更に応じて dirty 状態と選択中表示を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRowPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(sender is not TranslationDictionaryItemRowViewModel row || string.IsNullOrWhiteSpace( e.PropertyName )) {
            return;
        }

        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.Translated )) {
            UpdateDirtyState( row );
            if(ReferenceEquals( row, SelectedDictionaryItem ) && !_hasPendingSelectedTranslatedEdit) {
                SyncSelectedTranslatedFromSelection();
            }
        }

        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.IsEnabled )) {
            UpdateDirtyState( row );
            if(ReferenceEquals( row, SelectedDictionaryItem )) {
                if(SelectedDictionaryItem?.IsEnabled != true) {
                    CancelSelectedTranslatedCommit();
                    SyncSelectedTranslatedFromSelection();
                }

                NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
            }
        }

        RowPropertyChanged?.Invoke( this, new TranslationCreationRowPropertyChangedEventArgs( row, e.PropertyName ) );
    }

    /// <summary>
    /// 選択中翻訳文の遅延反映用タイマーを取得する。
    /// </summary>
    private DispatcherTimer SelectedTranslatedCommitTimer => _selectedTranslatedCommitTimer ??= CreateSelectedTranslatedCommitTimer();

    /// <summary>
    /// 選択中翻訳文の遅延反映用タイマーを生成する。
    /// </summary>
    /// <returns>生成したタイマーを返す。</returns>
    private DispatcherTimer CreateSelectedTranslatedCommitTimer() {
        var timer = new DispatcherTimer( DispatcherPriority.Background )
        {
            Interval = SelectedTranslatedCommitDelay
        };
        timer.Tick += OnSelectedTranslatedCommitTimerTick;
        return timer;
    }

    /// <summary>
    /// 遅延反映タイマー満了時に保留中の編集を確定する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectedTranslatedCommitTimerTick( object? sender, EventArgs e ) => FlushPendingSelectedTranslatedEdit();

    /// <summary>
    /// 選択中翻訳文の遅延反映を予約する。
    /// </summary>
    private void ScheduleSelectedTranslatedCommit() {
        _hasPendingSelectedTranslatedEdit = true;
        SelectedTranslatedCommitTimer.Stop();
        SelectedTranslatedCommitTimer.Start();
    }

    /// <summary>
    /// 選択中翻訳文の遅延反映を取り消す。
    /// </summary>
    private void CancelSelectedTranslatedCommit() {
        _hasPendingSelectedTranslatedEdit = false;
        _selectedTranslatedCommitTimer?.Stop();
    }

    /// <summary>
    /// 選択中行の翻訳文を詳細編集欄へ同期する。
    /// </summary>
    private void SyncSelectedTranslatedFromSelection() {
        var nextValue = SelectedDictionaryItem?.Translated ?? string.Empty;
        CancelSelectedTranslatedCommit();
        if(string.Equals( _selectedTranslated, nextValue, StringComparison.Ordinal )) {
            return;
        }

        _selectedTranslated = nextValue;
        NotifyOfPropertyChange( nameof( SelectedTranslated ) );
    }

    /// <summary>
    /// 現在の行状態を dirty 判定基準として再設定する。
    /// </summary>
    private void ResetDirtyState() {
        _dirtyItemCount = 0;
        foreach(var row in Rows) {
            row.ResetPendingChangesBaseline();
        }
    }

    /// <summary>
    /// 指定行の dirty 状態変化を集計へ反映する。
    /// </summary>
    /// <param name="row">更新対象の行。</param>
    private void UpdateDirtyState( TranslationDictionaryItemRowViewModel row ) {
        var wasDirty = row.HasPendingChanges;
        if(!row.UpdatePendingChanges()) {
            return;
        }

        if(row.HasPendingChanges) {
            if(!wasDirty) {
                _dirtyItemCount++;
            }
            return;
        }

        _dirtyItemCount = Math.Max( 0, _dirtyItemCount - 1 );
    }
    #endregion
}