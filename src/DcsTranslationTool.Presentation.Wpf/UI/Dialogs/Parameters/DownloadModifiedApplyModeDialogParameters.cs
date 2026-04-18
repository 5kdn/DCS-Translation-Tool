namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// Download Page の差分適用モード選択ダイアログ表示内容を表す。
/// </summary>
public sealed record DownloadModifiedApplyModeDialogParameters {
    /// <summary>
    /// タイトル文字列を取得する。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// メッセージ本文を取得する。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 全てサーバー版適用ボタン文言を取得する。
    /// </summary>
    public string ApplyAllRepositoryButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 全てローカル版適用ボタン文言を取得する。
    /// </summary>
    public string ApplyAllLocalButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 個別選択ボタン文言を取得する。
    /// </summary>
    public string SelectIndividuallyButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 中止ボタン文言を取得する。
    /// </summary>
    public string CancelButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 表示対象のダイアログホスト識別子を取得する。
    /// </summary>
    public string DialogIdentifier { get; init; } = "RootDialogHost";
}