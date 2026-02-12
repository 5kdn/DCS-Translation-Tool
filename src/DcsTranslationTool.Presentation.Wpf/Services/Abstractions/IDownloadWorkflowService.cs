using DcsTranslationTool.Application.Contracts;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// ダウンロードワークフローを提供するサービス契約を表す。
/// </summary>
public interface IDownloadWorkflowService {
    /// <summary>
    /// 指定されたダウンロード対象を保存する。
    /// </summary>
    /// <param name="items">ダウンロード対象一覧。</param>
    /// <param name="saveRootPath">保存先ルートパス。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <param name="progress">進捗イベント通知先。</param>
    /// <returns>実行結果。</returns>
    Task<DownloadWorkflowResult> DownloadFilesAsync(
        IReadOnlyList<ApiDownloadFilePathsItem> items,
        string saveRootPath,
        CancellationToken cancellationToken = default,
        IProgress<WorkflowEvent>? progress = null
    );
}