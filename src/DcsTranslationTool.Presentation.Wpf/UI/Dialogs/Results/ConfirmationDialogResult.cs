namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// 確認ダイアログの選択結果を表現する。
/// </summary>
public enum ConfirmationDialogResult {
    /// <summary>
    /// 確定が選択されたことを表現する。
    /// </summary>
    Confirm,

    /// <summary>
    /// 取消が選択されたことを表現する。
    /// </summary>
    Cancel,

    /// <summary>
    /// 補助選択肢が選択されたことを表現する。
    /// </summary>
    Secondary,
}