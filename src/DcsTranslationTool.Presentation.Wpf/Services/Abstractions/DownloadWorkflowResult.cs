namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// ダウンロードワークフロー実行結果を表す。
/// </summary>
/// <param name="IsSuccess">処理が成功したかどうか。</param>
/// <param name="Events">発生イベント一覧。</param>
public sealed record DownloadWorkflowResult(
    bool IsSuccess,
    IReadOnlyList<WorkflowEvent> Events
);