using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 適用ワークフローを提供するサービス契約を表す。
/// </summary>
public interface IApplyWorkflowService {
    /// <summary>
    /// 翻訳ファイルを対象ディレクトリへ適用する。
    /// </summary>
    /// <param name="targetEntries">適用対象エントリ。</param>
    /// <param name="rootFullPath">適用先ルート絶対パス。</param>
    /// <param name="rootWithSeparator">区切り文字付き適用先ルート。</param>
    /// <param name="translateFullPath">翻訳ルート絶対パス。</param>
    /// <param name="translateRootWithSeparator">区切り文字付き翻訳ルート。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <param name="progress">進捗イベント通知先。</param>
    /// <returns>実行結果。</returns>
    Task<ApplyWorkflowResult> ApplyAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        CancellationToken cancellationToken = default,
        IProgress<WorkflowEvent>? progress = null
    );
}