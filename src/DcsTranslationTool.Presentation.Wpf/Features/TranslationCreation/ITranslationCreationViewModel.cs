using System.ComponentModel;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView が参照する最小限の ViewModel 契約を表す。
/// </summary>
internal interface ITranslationCreationViewModel : INotifyPropertyChanged {
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
    bool IsDictionaryDetailsWrapEnabled { get; }

    /// <summary>
    /// 選択中の dictionary 項目を取得する。
    /// </summary>
    TranslationDictionaryItemRowViewModel? SelectedDictionaryItem { get; }

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
    /// dictionary 詳細テキストの折り返し状態を設定する。
    /// </summary>
    /// <param name="isEnabled">右端で折り返すかどうか。</param>
    void SetDictionaryDetailsWrapEnabled( bool isEnabled );

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
}