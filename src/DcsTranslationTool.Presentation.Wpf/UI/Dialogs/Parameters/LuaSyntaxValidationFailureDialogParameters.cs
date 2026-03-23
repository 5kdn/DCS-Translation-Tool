namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// Lua 構文検証失敗ダイアログの表示内容を表す。
/// </summary>
public sealed record LuaSyntaxValidationFailureDialogParameters {
    /// <summary>
    /// タイトル文字列を取得する。
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// メッセージ本文を取得する。
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 失敗ファイル一覧見出しを取得する。
    /// </summary>
    public string FailedFilesHeader { get; init; } = string.Empty;

    /// <summary>
    /// 失敗ファイルパス一覧を取得する。
    /// </summary>
    public IReadOnlyList<string> FailedFilePaths { get; init; } = [];

    /// <summary>
    /// リトライボタン文言を取得する。
    /// </summary>
    public string RetryButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 中止ボタン文言を取得する。
    /// </summary>
    public string CancelButtonText { get; init; } = string.Empty;

    /// <summary>
    /// 表示対象のダイアログホスト識別子を取得する。
    /// </summary>
    public string DialogIdentifier { get; init; } = "RootDialogHost";

    /// <summary>
    /// 失敗ファイルパス一覧を改行区切り文字列で取得する。
    /// </summary>
    public string FailedFilePathsText => string.Join( Environment.NewLine, FailedFilePaths );
}