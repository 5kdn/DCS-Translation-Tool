using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// ダウンロードと適用のワークフローを提供するサービス契約を表す。
/// </summary>
public interface IDownloadWorkflowService {
    /// <summary>
    /// ダウンロードユースケースを実行する。
    /// </summary>
    /// <param name="request">ダウンロード入力。</param>
    /// <param name="progressCallback">進捗通知コールバック。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>実行結果。</returns>
    Task<DownloadWorkflowResult> ExecuteDownloadAsync(
        DownloadExecutionRequest request,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 適用ユースケースを実行する。
    /// </summary>
    /// <param name="request">適用入力。</param>
    /// <param name="showSnackbarAsync">通知コールバック。</param>
    /// <param name="progressCallback">進捗通知コールバック。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>実行結果。</returns>
    Task<ApplyWorkflowResult> ExecuteApplyAsync(
        ApplyExecutionRequest request,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// ダウンロードURL一覧からファイルを保存する。
    /// </summary>
    /// <param name="items">ダウンロード対象一覧。</param>
    /// <param name="saveRootPath">保存先ルート。</param>
    /// <param name="progressCallback">進捗通知コールバック。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task DownloadFilesAsync(
        IReadOnlyList<ApiDownloadFilePathsItem> items,
        string saveRootPath,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// 翻訳ファイルを対象ディレクトリへ適用する。
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
    Task<bool> ApplyAsync(
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