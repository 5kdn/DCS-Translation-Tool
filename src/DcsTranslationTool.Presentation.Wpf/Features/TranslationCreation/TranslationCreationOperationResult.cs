namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の操作結果を表す。
/// </summary>
/// <param name="IsSuccess">操作が成功したかどうか。</param>
/// <param name="WasCancelled">ユーザー操作で中断されたかどうか。</param>
/// <param name="IsPartial">部分適用かどうか。</param>
/// <param name="AppliedCount">適用件数。</param>
/// <param name="StatusMessage">画面へ反映する状態メッセージ。</param>
/// <param name="NotificationKind">表示する通知の種類。</param>
/// <param name="OutputPath">通知に紐づく出力先パス。</param>
public sealed record TranslationCreationOperationResult(
    bool IsSuccess,
    bool WasCancelled,
    bool IsPartial,
    int AppliedCount,
    string? StatusMessage,
    TranslationCreationNotificationKind NotificationKind = TranslationCreationNotificationKind.None,
    string? OutputPath = null );

/// <summary>
/// TranslationCreation の通知種別を表す。
/// </summary>
public enum TranslationCreationNotificationKind {
    /// <summary>
    /// 通知不要を表す。
    /// </summary>
    None,

    /// <summary>
    /// 完了メッセージ通知を表す。
    /// </summary>
    Completed,

    /// <summary>
    /// 書き出し成功通知を表す。
    /// </summary>
    ExportSucceeded,
}