using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// RepoOnly ファイル同期を提供するサービス契約を表す。
/// </summary>
public interface IRepoOnlySyncService {
    /// <summary>
    /// RepoOnly ファイルを翻訳ディレクトリへ同期する。
    /// </summary>
    /// <param name="targetEntries">適用対象エントリ。</param>
    /// <param name="translateFullPath">翻訳ルート絶対パス。</param>
    /// <param name="translateRootWithSeparator">区切り文字付き翻訳ルート。</param>
    /// <param name="downloadFilesAsync">ダウンロード処理コールバック。</param>
    /// <param name="showSnackbarAsync">通知コールバック。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>同期継続可能な場合は <see langword="true"/>。</returns>
    Task<bool> EnsureRepoOnlyFilesAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task> downloadFilesAsync,
        Func<string, Task> showSnackbarAsync,
        CancellationToken cancellationToken = default
    );
}