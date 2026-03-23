namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// Lua 構文検証失敗ダイアログの選択結果を表す。
/// </summary>
public enum LuaSyntaxValidationFailureDialogResult {
    /// <summary>
    /// リトライが選択されたことを表す。
    /// </summary>
    Retry,

    /// <summary>
    /// 中止が選択されたことを表す。
    /// </summary>
    Cancel,
}