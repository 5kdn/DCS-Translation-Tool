using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// Download Page の差分個別選択ダイアログ表示内容を表す。
/// </summary>
public sealed record DownloadModifiedApplySelectionDialogParameters {
    /// <summary>
    /// タイトル文字列を取得する。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// メッセージ本文を取得する。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 一覧ヘッダーのファイルパス文言を取得する。
    /// </summary>
    public string PathHeader { get; init; } = string.Empty;

    /// <summary>
    /// 一覧ヘッダーのサーバー版文言を取得する。
    /// </summary>
    public string RepositoryHeader { get; init; } = string.Empty;

    /// <summary>
    /// 一覧ヘッダーのローカル版文言を取得する。
    /// </summary>
    public string LocalHeader { get; init; } = string.Empty;

    /// <summary>
    /// 確定ボタン文言を取得する。
    /// </summary>
    public string ConfirmButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 中止ボタン文言を取得する。
    /// </summary>
    public string CancelButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 選択項目一覧を取得する。
    /// </summary>
    public IReadOnlyList<DownloadModifiedApplySelectionItem> Items { get; init; } = [];

    /// <summary>
    /// 表示対象のダイアログホスト識別子を取得する。
    /// </summary>
    public string DialogIdentifier { get; init; } = "RootDialogHost";

    /// <summary>
    /// 現在の選択状態から結果を構築する。
    /// </summary>
    /// <returns>構築した結果を返す。</returns>
    public DownloadModifiedApplySelectionDialogResult CreateResult() =>
        new(
            true,
            Items.ToDictionary( item => item.Path, item => item.ApplySource, StringComparer.Ordinal ) );
}