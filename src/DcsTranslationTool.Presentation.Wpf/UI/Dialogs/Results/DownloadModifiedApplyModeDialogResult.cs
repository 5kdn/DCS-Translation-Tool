namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// Download Page の差分適用モード選択結果を表す。
/// </summary>
public enum DownloadModifiedApplyModeDialogResult {
    /// <summary>
    /// 全てサーバー版を適用することを表す。
    /// </summary>
    ApplyAllRepository,

    /// <summary>
    /// 全てローカル版を適用することを表す。
    /// </summary>
    ApplyAllLocal,

    /// <summary>
    /// 個別に選択することを表す。
    /// </summary>
    SelectIndividually,

    /// <summary>
    /// 中止することを表す。
    /// </summary>
    Cancel,
}