namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// ワークフロー処理が通知するイベント種別を表す。
/// </summary>
public enum WorkflowEventKind {
    /// <summary>メッセージ通知を表す。</summary>
    Notification,

    /// <summary>ダウンロード進捗更新を表す。</summary>
    DownloadProgress,

    /// <summary>適用進捗更新を表す。</summary>
    ApplyProgress
}

/// <summary>
/// ワークフロー処理イベントを表す。
/// </summary>
/// <param name="Kind">イベント種別。</param>
/// <param name="Message">通知メッセージ。</param>
/// <param name="Progress">進捗率。</param>
public sealed record WorkflowEvent(
    WorkflowEventKind Kind,
    string? Message = null,
    double? Progress = null
);