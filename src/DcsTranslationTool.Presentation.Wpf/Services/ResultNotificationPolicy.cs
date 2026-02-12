using DcsTranslationTool.Application.Results;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// Result 失敗分類に応じた通知文言を提供する。
/// </summary>
public static class ResultNotificationPolicy {
    /// <summary>
    /// リポジトリツリー取得失敗時の通知文言を取得する。
    /// </summary>
    /// <param name="kind">失敗分類。</param>
    /// <returns>通知文言。</returns>
    public static string GetTreeFetchFailureMessage( ResultErrorKind? kind ) => kind switch
    {
        ResultErrorKind.Validation => "リポジトリファイル一覧の取得条件が不正です",
        ResultErrorKind.NotFound => "リポジトリファイル一覧が見つかりませんでした",
        ResultErrorKind.External => "リポジトリファイル一覧の取得に失敗しました",
        _ => "リポジトリファイル一覧の取得に失敗しました",
    };

    /// <summary>
    /// ダウンロード URL 取得失敗時の通知文言を取得する。
    /// </summary>
    /// <param name="kind">失敗分類。</param>
    /// <returns>通知文言。</returns>
    public static string GetDownloadPathFailureMessage( ResultErrorKind? kind ) => kind switch
    {
        ResultErrorKind.Validation => "ダウンロード対象が不正です",
        ResultErrorKind.NotFound => "ダウンロード対象が見つかりませんでした",
        ResultErrorKind.External => "ダウンロードURLの取得に失敗しました",
        _ => "ダウンロードURLの取得に失敗しました",
    };
}
