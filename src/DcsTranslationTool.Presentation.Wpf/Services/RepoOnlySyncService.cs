using System.IO;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// RepoOnly ファイル同期を提供する。
/// </summary>
public sealed class RepoOnlySyncService(
    IApiService apiService,
    ILoggingService logger,
    IPathSafetyGuard pathSafetyGuard
) : IRepoOnlySyncService {
    /// <inheritdoc/>
    public async Task<bool> EnsureRepoOnlyFilesAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task> downloadFilesAsync,
        Func<string, Task> showSnackbarAsync,
        CancellationToken cancellationToken = default
    ) {
        var repoOnlyEntries = targetEntries
            .Where( entry => entry.ChangeType == FileChangeType.RepoOnly )
            .ToList();
        if(repoOnlyEntries.Count == 0) {
            return true;
        }

        logger.Info( $"リポジトリのみのファイルを取得する。Count={repoOnlyEntries.Count}" );
        var paths = targetEntries.Select( entry => entry.Path ).ToList();
        var pathResult = await apiService.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( paths, null ),
            cancellationToken
        );
        if(pathResult.IsFailed) {
            var reason = pathResult.Errors.Count > 0 ? pathResult.Errors[0].Message : null;
            var message = ResultNotificationPolicy.GetDownloadPathFailureMessage( pathResult.GetFirstErrorKind() );
            logger.Error( $"ダウンロードURLの取得に失敗した。Reason={reason}" );
            await showSnackbarAsync( message );
            return false;
        }

        var downloadItems = pathResult.Value.Items.ToArray();
        logger.Info( $"ダウンロードURLを取得した。{downloadItems.Length}件" );
        if(downloadItems.Length == 0) {
            logger.Info( "ダウンロード対象が最新のため保存をスキップする。" );
            await showSnackbarAsync( "対象ファイルは最新です" );
            return true;
        }

        await downloadFilesAsync( downloadItems );
        foreach(var repoEntry in repoOnlyEntries) {
            if(!pathSafetyGuard.TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, repoEntry.Path, out var repoPath ) || !File.Exists( repoPath )) {
                logger.Warn( $"取得後もファイルが存在しない。Path={repoEntry.Path}, Resolved={repoPath}" );
                await showSnackbarAsync( $"取得失敗: {repoEntry.Path}" );
                return false;
            }
        }

        return true;
    }
}