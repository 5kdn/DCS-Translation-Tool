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
    /// 表示対象のダイアログホスト識別子を取得する。
    /// </summary>
    public string DialogIdentifier { get; init; } = "RootDialogHost";
}