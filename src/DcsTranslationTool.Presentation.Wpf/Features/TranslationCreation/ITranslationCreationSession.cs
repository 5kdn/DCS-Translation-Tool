using System.Collections.ObjectModel;
using System.ComponentModel;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 画面の編集セッションを管理する。
/// </summary>
public interface ITranslationCreationSession : INotifyPropertyChanged {
    /// <summary>
    /// 画面表示中の dictionary 行一覧を取得する。
    /// </summary>
    ObservableCollection<TranslationDictionaryItemRowViewModel> Rows { get; }

    /// <summary>
    /// 選択中の dictionary 項目を取得または設定する。
    /// </summary>
    TranslationDictionaryItemRowViewModel? SelectedDictionaryItem { get; set; }

    /// <summary>
    /// 選択中項目の Original を取得する。
    /// </summary>
    string SelectedOriginal { get; }

    /// <summary>
    /// 選択中項目の Translated 下書きを取得または設定する。
    /// </summary>
    string SelectedTranslated { get; set; }

    /// <summary>
    /// 選択中項目の翻訳文を編集可能かどうかを取得する。
    /// </summary>
    bool CanEditSelectedTranslated { get; }

    /// <summary>
    /// 初期読込済み項目が存在するかどうかを取得する。
    /// </summary>
    bool HasLoadedItems { get; }

    /// <summary>
    /// 現在の行一覧に 1 件以上の翻訳文が存在するかどうかを判定する。
    /// </summary>
    /// <returns>翻訳文が存在する場合は <see langword="true"/> を返す。</returns>
    bool HasAnyTranslatedText();

    /// <summary>
    /// 閉じる確認が必要な未反映変更が存在するかどうかを判定する。
    /// </summary>
    /// <returns>未反映変更が存在する場合は <see langword="true"/> を返す。</returns>
    bool HasPendingChangesForClose();

    /// <summary>
    /// 読み込み済み dictionary 状態をセッションへ反映する。
    /// </summary>
    /// <param name="state">適用対象の状態。</param>
    void Load( TranslationCreationDictionaryLoadState state );

    /// <summary>
    /// 選択中翻訳文の保留中編集を現在の行へ反映する。
    /// </summary>
    void FlushPendingSelectedTranslatedEdit();

    /// <summary>
    /// 表示中の dictionary 項目選択を移動する。
    /// </summary>
    /// <param name="filteredItemsView">現在表示中の一覧ビュー。</param>
    /// <param name="offset">移動量。</param>
    /// <returns>選択項目が変化した場合は <see langword="true"/> を返す。</returns>
    bool MoveSelection( ICollectionView filteredItemsView, int offset );

    /// <summary>
    /// 現在の行状態から書き出し用 dictionary 項目一覧を生成する。
    /// </summary>
    /// <returns>生成した項目一覧を返す。</returns>
    TranslationCreationDocumentSnapshot CreateDocumentSnapshot();

    /// <summary>
    /// 行の状態が変化したときに発生する。
    /// </summary>
    event EventHandler<TranslationCreationRowPropertyChangedEventArgs>? RowPropertyChanged;
}