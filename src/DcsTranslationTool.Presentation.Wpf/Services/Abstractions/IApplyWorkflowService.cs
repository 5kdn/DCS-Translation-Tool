using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 翻訳ファイルの適用ワークフローを提供するサービス契約を表す。
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
    /// <param name="downloadFilesAsync">ダウンロード処理コールバック。</param>
    /// <param name="showSnackbarAsync">通知コールバック。</param>
    /// <param name="progressCallback">進捗通知コールバック。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>処理完了時は <see langword="true"/>。</returns>
    Task<bool> ApplyAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task> downloadFilesAsync,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    );
}