namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// Download Page の差分個別選択ダイアログ結果を表す。
/// </summary>
/// <param name="IsConfirmed">確定されたかどうか。</param>
/// <param name="SelectedSources">ファイルパスごとの適用元。</param>
public sealed record DownloadModifiedApplySelectionDialogResult(
    bool IsConfirmed,
    IReadOnlyDictionary<string, DownloadModifiedApplySource> SelectedSources
);