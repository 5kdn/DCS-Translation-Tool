using System.ComponentModel;

using Caliburn.Micro;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 画面の編集状態を管理する。
/// </summary>
internal sealed class TranslationCreationSession : PropertyChangedBase, ITranslationCreationSession {
    private IReadOnlyList<TranslationCreationRowState> _rows = [];
    private TranslationCreationRowState? _selectedRow;
    private string _selectedTranslatedDraft = string.Empty;
    private int _dirtyItemCount;

    /// <inheritdoc />
    public IReadOnlyList<TranslationCreationRowState> Rows => _rows;

    /// <inheritdoc />
    public TranslationCreationRowState? SelectedRow {
        get => _selectedRow;
        set {
            FlushPendingSelectedTranslatedEdit();
            if(!Set( ref _selectedRow, value )) {
                return;
            }

            SyncSelectedTranslatedDraftFromSelection();
            NotifyOfPropertyChange( nameof( SelectedOriginal ) );
            NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
        }
    }

    /// <inheritdoc />
    public string SelectedOriginal => SelectedRow?.Original ?? string.Empty;

    /// <inheritdoc />
    public string SelectedTranslatedDraft {
        get => _selectedTranslatedDraft;
        set {
            if(SelectedRow?.IsEnabled != true) {
                return;
            }

            if(string.Equals( _selectedTranslatedDraft, value, StringComparison.Ordinal )) {
                return;
            }

            _selectedTranslatedDraft = value;
            NotifyOfPropertyChange();
        }
    }

    /// <inheritdoc />
    public bool CanEditSelectedTranslated => SelectedRow?.IsEnabled == true;

    /// <inheritdoc />
    public bool HasLoadedItems => _rows.Count > 0;

    /// <inheritdoc />
    public event EventHandler<TranslationCreationRowPropertyChangedEventArgs>? RowPropertyChanged;

    /// <inheritdoc />
    public bool HasAnyTranslatedText() => Rows.Any( static item => !string.IsNullOrWhiteSpace( item.Translated ) );

    /// <inheritdoc />
    public bool HasPendingChangesForClose() =>
        HasPendingChangesForClose( SelectedRow, _selectedTranslatedDraft );

    /// <inheritdoc />
    public void Load( TranslationCreationDictionaryLoadState state ) {
        UnsubscribeRows( _rows );
        _rows = [.. state.RowStates.Select( static rowState => new TranslationCreationRowState( rowState.ToTranslationDictionaryItem(), rowState.IsPossibleNonTranslationTarget ) )];
        SubscribeRows( _rows );
        ResetDirtyState();
        SelectedRow = null;
        NotifyOfPropertyChange( nameof( Rows ) );
        NotifyOfPropertyChange( nameof( HasLoadedItems ) );
    }

    /// <inheritdoc />
    public void FlushPendingSelectedTranslatedEdit() {
        if(SelectedRow?.IsEnabled != true) {
            SyncSelectedTranslatedDraftFromSelection();
            return;
        }

        if(string.Equals( SelectedRow.Translated, _selectedTranslatedDraft, StringComparison.Ordinal )) {
            return;
        }

        SelectedRow.Translated = _selectedTranslatedDraft;
    }

    /// <inheritdoc />
    public bool MoveSelection( IReadOnlyList<TranslationCreationRowState> visibleRows, int offset ) {
        if(visibleRows.Count == 0) {
            return false;
        }

        if(SelectedRow is null) {
            SelectedRow = offset < 0
                ? visibleRows[^1]
                : visibleRows[0];
            return true;
        }

        var currentIndex = FindRowIndex( visibleRows, SelectedRow );
        if(currentIndex < 0) {
            SelectedRow = offset < 0
                ? visibleRows[^1]
                : visibleRows[0];
            return true;
        }

        var nextIndex = currentIndex + offset;
        if(nextIndex < 0 || nextIndex >= visibleRows.Count) {
            return false;
        }

        SelectedRow = visibleRows[nextIndex];
        return true;
    }

    /// <inheritdoc />
    public TranslationCreationDocumentSnapshot CreateDocumentSnapshot() =>
        new( [.. Rows.Select( static item => item.ToTranslationDictionaryItem() )] );

    /// <summary>
    /// 読み込み基準と比較して dirty 行が存在するかどうかを判定する。
    /// </summary>
    /// <returns>dirty 行が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasDirtyRows() => _dirtyItemCount > 0;

    /// <summary>
    /// 選択中詳細編集の保留値を加味して未反映変更が存在するかどうかを判定する。
    /// </summary>
    /// <param name="selectedRow">選択中行。</param>
    /// <param name="selectedTranslatedDraft">保留中の翻訳文。</param>
    /// <returns>未反映変更が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasPendingChangesForClose( TranslationCreationRowState? selectedRow, string selectedTranslatedDraft ) {
        if(selectedRow?.IsEnabled != true) {
            return HasDirtyRows();
        }

        if(selectedRow.HasPendingChangesWithTranslatedOverride( selectedTranslatedDraft )) {
            return true;
        }

        return HasDirtyRows() && !selectedRow.HasPendingChanges;
    }

    /// <summary>
    /// 行コレクションの変更監視を開始する。
    /// </summary>
    /// <param name="rows">監視対象の行コレクション。</param>
    private void SubscribeRows( IReadOnlyList<TranslationCreationRowState> rows ) {
        foreach(var item in rows) {
            item.PropertyChanged += OnRowPropertyChanged;
        }
    }

    /// <summary>
    /// 行コレクションの変更監視を解除する。
    /// </summary>
    /// <param name="rows">解除対象の行コレクション。</param>
    private void UnsubscribeRows( IReadOnlyList<TranslationCreationRowState> rows ) {
        foreach(var item in rows) {
            item.PropertyChanged -= OnRowPropertyChanged;
        }
    }

    /// <summary>
    /// 行の状態変更に応じて dirty 状態と選択中表示を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRowPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(sender is not TranslationCreationRowState row || string.IsNullOrWhiteSpace( e.PropertyName )) {
            return;
        }

        if(e.PropertyName == nameof( TranslationCreationRowState.Translated )) {
            UpdateDirtyState( row );
            if(ReferenceEquals( row, SelectedRow ) && string.Equals( _selectedTranslatedDraft, row.Translated, StringComparison.Ordinal )) {
                SyncSelectedTranslatedDraftFromSelection();
            }
        }

        if(e.PropertyName == nameof( TranslationCreationRowState.IsEnabled )) {
            UpdateDirtyState( row );
            if(ReferenceEquals( row, SelectedRow )) {
                if(!row.IsEnabled) {
                    SyncSelectedTranslatedDraftFromSelection();
                }

                NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
            }
        }

        RowPropertyChanged?.Invoke( this, new TranslationCreationRowPropertyChangedEventArgs( row, e.PropertyName ) );
    }

    /// <summary>
    /// 選択中行の翻訳文を詳細編集欄へ同期する。
    /// </summary>
    private void SyncSelectedTranslatedDraftFromSelection() {
        var nextValue = SelectedRow?.Translated ?? string.Empty;
        if(string.Equals( _selectedTranslatedDraft, nextValue, StringComparison.Ordinal )) {
            return;
        }

        _selectedTranslatedDraft = nextValue;
        NotifyOfPropertyChange( nameof( SelectedTranslatedDraft ) );
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
    private void UpdateDirtyState( TranslationCreationRowState row ) {
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

    /// <summary>
    /// 指定行一覧から対象行の位置を検索する。
    /// </summary>
    /// <param name="rows">検索対象の行一覧。</param>
    /// <param name="targetRow">検索対象の行。</param>
    /// <returns>見つかった位置。存在しない場合は -1 を返す。</returns>
    private static int FindRowIndex( IReadOnlyList<TranslationCreationRowState> rows, TranslationCreationRowState targetRow ) {
        for(var i = 0; i < rows.Count; i++) {
            if(ReferenceEquals( rows[i], targetRow )) {
                return i;
            }
        }

        return -1;
    }
}