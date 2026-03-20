using System.ComponentModel;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView が参照する最小限の ViewModel 契約を表す。
/// </summary>
public interface ITranslationCreationViewModel : INotifyPropertyChanged {
    /// <summary>
    /// ウィンドウ幅を取得または設定する。
    /// </summary>
    double WindowWidth { get; set; }

    /// <summary>
    /// ウィンドウ高さを取得または設定する。
    /// </summary>
    double WindowHeight { get; set; }

    /// <summary>
    /// dictionary 領域比率を取得または設定する。
    /// </summary>
    double DictionaryPaneRatio { get; set; }

    /// <summary>
    /// dictionary 詳細テキストを右端で折り返すかどうかを取得する。
    /// </summary>
    bool IsDictionaryDetailsWrapEnabled { get; set; }

    /// <summary>
    /// 可視 dictionary 項目の再評価版数を取得する。
    /// </summary>
    int VisibleDictionaryItemsVersion { get; }

    /// <summary>
    /// 起動時にウィンドウを閉じる要求があるかどうかを取得する。
    /// </summary>
    bool ShouldCloseAfterStartup { get; }

    /// <summary>
    /// 選択中の dictionary 項目を取得する。
    /// </summary>
    TranslationDictionaryItemRowViewModel? SelectedDictionaryItem { get; }

    /// <summary>
    /// 指定行を表示対象に含めるかどうかを判定する。
    /// </summary>
    /// <param name="row">判定対象の行。</param>
    /// <returns>表示対象に含める場合は <see langword="true"/> を返す。</returns>
    bool ShouldIncludeRow( TranslationDictionaryItemRowViewModel row );

    /// <summary>
    /// 表示中の dictionary 項目選択を 1 件上へ移動する。
    /// </summary>
    /// <returns>選択項目が変化したかどうか。</returns>
    bool MoveSelectionUp();

    /// <summary>
    /// 表示中の dictionary 項目選択を 1 件下へ移動する。
    /// </summary>
    /// <returns>選択項目が変化したかどうか。</returns>
    bool MoveSelectionDown();

    /// <summary>
    /// ウィンドウ表示後に必要な初期化を実行する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task HandleWindowLoadedAsync( CancellationToken cancellationToken = default );

    /// <summary>
    /// ウィンドウを閉じてもよいかどうかを確認する。
    /// </summary>
    /// <returns>ウィンドウを閉じてよい場合は <see langword="true"/> を返す。</returns>
    Task<bool> ConfirmCloseAsync();

    /// <summary>
    /// 起動時クローズ要求を消費する。
    /// </summary>
    void AcknowledgeStartupCloseRequest();
}