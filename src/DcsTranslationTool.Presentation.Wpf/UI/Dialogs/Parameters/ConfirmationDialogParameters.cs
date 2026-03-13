namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// 確認ダイアログの表示内容を表現する。
/// </summary>
public sealed record ConfirmationDialogParameters {
    /// <summary>
    /// タイトル文字列を取得する。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// メッセージ本文を取得する。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 承認ボタンに表示する文言を取得する。
    /// </summary>
    public string ConfirmButtonText { get; init; } = "OK";

    /// <summary>
    /// 取消ボタンに表示する文言を取得する。
    /// </summary>
    public string CancelButtonText { get; init; } = "キャンセル";

    /// <summary>
    /// 補助ボタンに表示する文言を取得する。
    /// </summary>
    public string SecondaryButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 補助ボタンを表示するかどうかを取得する。
    /// </summary>
    public bool HasSecondaryButton => !string.IsNullOrWhiteSpace( SecondaryButtonText );

    /// <summary>
    /// 表示対象のダイアログホスト識別子を取得する。
    /// </summary>
    public string DialogIdentifier { get; init; } = "RootDialogHost";
}