using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// 確認ダイアログの表示内容を表現する。
/// </summary>
public sealed record ConfirmationDialogParameters {
    private static readonly IReadOnlyList<ConfirmationDialogResult> DefaultButtonOrder =
    [
        ConfirmationDialogResult.Secondary,
        ConfirmationDialogResult.Cancel,
        ConfirmationDialogResult.Confirm
    ];

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
    /// 承認ボタンに適用するスタイルキーを取得する。
    /// </summary>
    public string ConfirmButtonStyleKey { get; init; } = "MaterialDesignFlatButton";

    /// <summary>
    /// 補助ボタンに適用するスタイルキーを取得する。
    /// </summary>
    public string SecondaryButtonStyleKey { get; init; } = "MaterialDesignFlatButton";

    /// <summary>
    /// 取消ボタンに適用するスタイルキーを取得する。
    /// </summary>
    public string CancelButtonStyleKey { get; init; } = "MaterialDesignFlatButton";

    /// <summary>
    /// ボタンの表示順を取得する。
    /// </summary>
    public IReadOnlyList<ConfirmationDialogResult> ButtonOrder { get; init; } = DefaultButtonOrder;

    /// <summary>
    /// 表示対象のダイアログホスト識別子を取得する。
    /// </summary>
    public string DialogIdentifier { get; init; } = "RootDialogHost";
}