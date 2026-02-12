using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 翻訳エントリ適用処理を提供するサービス契約を表す。
/// </summary>
public interface IEntryApplyService {
    /// <summary>
    /// 翻訳エントリを対象ディレクトリへ適用する。
    /// </summary>
    /// <param name="targetEntries">適用対象エントリ。</param>
    /// <param name="rootFullPath">適用先ルート絶対パス。</param>
    /// <param name="rootWithSeparator">区切り文字付き適用先ルート。</param>
    /// <param name="translateFullPath">翻訳ルート絶対パス。</param>
    /// <param name="translateRootWithSeparator">区切り文字付き翻訳ルート。</param>
    /// <param name="showSnackbarAsync">通知コールバック。</param>
    /// <param name="progressCallback">進捗通知コールバック。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>処理完了時は <see langword="true"/>。</returns>
    Task<bool> ApplyEntriesAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    );
}