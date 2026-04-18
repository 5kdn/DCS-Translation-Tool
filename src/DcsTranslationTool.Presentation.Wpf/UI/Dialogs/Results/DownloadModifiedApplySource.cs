namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// Download Page の差分ファイルに適用するソース種別を表す。
/// </summary>
public enum DownloadModifiedApplySource {
    /// <summary>
    /// サーバー版を適用することを表す。
    /// </summary>
    Repository,

    /// <summary>
    /// ローカル版を適用することを表す。
    /// </summary>
    Local,
}