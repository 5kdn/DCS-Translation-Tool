using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// 翻訳ファイル適用のオーケストレーションを提供する。
/// </summary>
public sealed class ApplyWorkflowService(
    IRepoOnlySyncService repoOnlySyncService,
    IEntryApplyService entryApplyService
) : IApplyWorkflowService {
    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task> downloadFilesAsync,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        var repoOnlyReady = await repoOnlySyncService.EnsureRepoOnlyFilesAsync(
            targetEntries,
            translateFullPath,
            translateRootWithSeparator,
            downloadFilesAsync,
            showSnackbarAsync,
            cancellationToken
        );
        if(!repoOnlyReady) {
            return false;
        }

        return await entryApplyService.ApplyEntriesAsync(
            targetEntries,
            rootFullPath,
            rootWithSeparator,
            translateFullPath,
            translateRootWithSeparator,
            showSnackbarAsync,
            progressCallback,
            cancellationToken
        );
    }
}