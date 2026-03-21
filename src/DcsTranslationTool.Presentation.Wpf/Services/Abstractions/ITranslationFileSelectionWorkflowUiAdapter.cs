using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// Translation File Selection 画面の UI 反映を仲介する契約を表す。
/// </summary>
public interface ITranslationFileSelectionWorkflowUiAdapter {
    /// <summary>
    /// 読み込み結果を UI スレッド上で画面状態へ反映し、必要に応じて通知を表示する。
    /// </summary>
    /// <param name="loadResult">反映対象の読み込み結果。</param>
    /// <param name="applyLoadResult">画面状態へ反映する処理。</param>
    /// <returns>非同期タスク。</returns>
    Task ApplyLoadResultAsync(
        TranslationFileSelectionLoadResult loadResult,
        Action<TranslationFileSelectionLoadResult> applyLoadResult );
}