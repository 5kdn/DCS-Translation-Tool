using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// Translation File Selection の読み込みワークフローを提供するサービス契約を表す。
/// </summary>
public interface ITranslationFileSelectionWorkflowService {
    /// <summary>
    /// 画面表示に必要な翻訳アーカイブ情報を読み込む。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>読み込み結果。</returns>
    Task<TranslationFileSelectionLoadResult> LoadAsync( CancellationToken cancellationToken );
}