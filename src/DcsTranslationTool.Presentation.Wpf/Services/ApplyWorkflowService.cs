using System.IO;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// 適用ワークフローを提供する。
/// </summary>
public sealed class ApplyWorkflowService(
    IApiService apiService,
    IDownloadWorkflowService downloadWorkflowService,
    ILoggingService logger,
    IZipService zipService
) : IApplyWorkflowService {
    private static readonly string[] ZipLikeExtensions = [".miz", ".trk"];

    /// <inheritdoc/>
    public async Task<ApplyWorkflowResult> ApplyAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        CancellationToken cancellationToken = default,
        IProgress<WorkflowEvent>? progress = null
    ) {
        var events = new List<WorkflowEvent>();
        var repoOnlyEntries = targetEntries
            .Where( entry => entry.ChangeType == FileChangeType.RepoOnly )
            .ToList();

        if(repoOnlyEntries.Count > 0) {
            var pathResult = await apiService.DownloadFilePathsAsync(
                new ApiDownloadFilePathsRequest( targetEntries.Select( e => e.Path ).ToList(), null ),
                cancellationToken
            );

            if(pathResult.IsFailed) {
                var reason = pathResult.Errors.Count > 0 ? pathResult.Errors[0].Message : null;
                logger.Error( $"ダウンロードURLの取得に失敗した。Reason={reason}" );
                events.Add( new WorkflowEvent(
                    WorkflowEventKind.Notification,
                    ResultNotificationPolicy.GetDownloadPathFailureMessage( pathResult.GetFirstErrorKind() ) ) );
                return new ApplyWorkflowResult( false, events );
            }

            var downloadItems = pathResult.Value.Items.ToArray();
            if(downloadItems.Length == 0) {
                events.Add( new WorkflowEvent( WorkflowEventKind.Notification, "対象ファイルは最新です" ) );
            }
            else {
                var downloadResult = await downloadWorkflowService.DownloadFilesAsync( downloadItems, translateFullPath, cancellationToken, progress );
                events.AddRange( downloadResult.Events );
                if(!downloadResult.IsSuccess) {
                    return new ApplyWorkflowResult( false, events );
                }
            }

            foreach(var repoEntry in repoOnlyEntries) {
                if(!TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, repoEntry.Path, out var repoPath ) || !File.Exists( repoPath )) {
                    logger.Warn( $"取得後もファイルが存在しない。Path={repoEntry.Path}, Resolved={repoPath}" );
                    events.Add( new WorkflowEvent( WorkflowEventKind.Notification, $"取得失敗: {repoEntry.Path}" ) );
                    return new ApplyWorkflowResult( false, events );
                }
            }
        }

        var success = 0;
        var failed = 0;

        if(targetEntries.Count == 0) {
            var completedEvent = new WorkflowEvent( WorkflowEventKind.ApplyProgress, Progress: 100 );
            events.Add( completedEvent );
            progress?.Report( completedEvent );
            events.Add( new WorkflowEvent( WorkflowEventKind.Notification, "適用完了 成功:0 件 失敗:0 件" ) );
            return new ApplyWorkflowResult( true, events );
        }

        var progressed = 0;
        foreach(var entry in targetEntries) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                string? message = null;
                if(!TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, entry.Path, out var sourceFilePath )) {
                    failed++;
                    message = $"不正な翻訳ファイル: {entry.Path}";
                }
                else if(!File.Exists( sourceFilePath )) {
                    failed++;
                    message = $"翻訳ファイルが見つかりません: {entry.Path}";
                }
                else {
                    var parts = entry.Path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
                    var archiveIndex = Array.FindIndex( parts, IsZipLikeEntrySegment );
                    var rootSkipCount = GetRootSegmentSkipCount( parts );

                    if(archiveIndex == -1) {
                        var relativeSegments = parts.Skip( rootSkipCount ).ToArray();
                        if(relativeSegments.Length == 0) {
                            failed++;
                            message = $"不正なパス構造: {entry.Path}";
                        }
                        else {
                            var relativePath = string.Join( '/', relativeSegments );
                            if(!TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, relativePath, out var destinationPath )) {
                                failed++;
                                message = $"不正な適用先: {entry.Path}";
                            }
                            else {
                                try {
                                    var directoryName = Path.GetDirectoryName( destinationPath );
                                    if(!string.IsNullOrEmpty( directoryName )) {
                                        Directory.CreateDirectory( directoryName );
                                    }
                                    File.Copy( sourceFilePath, destinationPath, overwrite: true );
                                    success++;
                                }
                                catch(Exception ex) {
                                    failed++;
                                    message = $"適用失敗: {entry.Path}";
                                    logger.Error( $"ファイルの適用に失敗した。Path={destinationPath}", ex );
                                }
                            }
                        }
                    }
                    else {
                        var archiveSegments = parts.Take( archiveIndex + 1 ).Skip( rootSkipCount ).ToArray();
                        if(archiveSegments.Length == 0) {
                            failed++;
                            message = $"不正なパス構造: {entry.Path}";
                        }
                        else {
                            var archiveRelativePath = string.Join( '/', archiveSegments );
                            if(!TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, archiveRelativePath, out var archivePath )) {
                                failed++;
                                message = $"不正な適用先: {entry.Path}";
                            }
                            else if(!File.Exists( archivePath )) {
                                failed++;
                                message = $"圧縮ファイルが存在しません: {entry.Path}";
                            }
                            else {
                                var entryPathSegments = parts.Skip( archiveIndex + 1 ).ToArray();
                                if(entryPathSegments.Length == 0) {
                                    failed++;
                                    message = $"圧縮ファイル内パスが不正です: {entry.Path}";
                                }
                                else {
                                    var entryPath = string.Join( '/', entryPathSegments );
                                    var addResult = zipService.AddEntry( archivePath, entryPath, sourceFilePath );
                                    if(addResult.IsFailed) {
                                        failed++;
                                        message = $"適用失敗: {entry.Path}";
                                    }
                                    else {
                                        success++;
                                    }
                                }
                            }
                        }
                    }
                }

                if(message is not null) {
                    events.Add( new WorkflowEvent( WorkflowEventKind.Notification, message ) );
                }
            }
            catch(Exception ex) {
                failed++;
                logger.Error( $"適用処理で例外が発生した。Path={entry.Path}", ex );
                events.Add( new WorkflowEvent( WorkflowEventKind.Notification, $"適用失敗: {entry.Path}" ) );
            }

            progressed++;
            var progressEvent = new WorkflowEvent(
                WorkflowEventKind.ApplyProgress,
                Progress: Math.Min( 100, (double)progressed / targetEntries.Count * 100 ) );
            events.Add( progressEvent );
            progress?.Report( progressEvent );
        }

        var completed = new WorkflowEvent( WorkflowEventKind.ApplyProgress, Progress: 100 );
        events.Add( completed );
        progress?.Report( completed );
        events.Add( new WorkflowEvent( WorkflowEventKind.Notification, $"適用完了 成功:{success} 件 失敗:{failed} 件" ) );
        logger.Info( $"適用処理が完了した。成功={success}, 失敗={failed}" );
        return new ApplyWorkflowResult( true, events );
    }

    /// <summary>ルートディレクトリ配下に収まるようにパスを解決する。</summary>
    private static bool TryResolvePathWithinRoot( string rootFullPath, string rootWithSeparator, string relativePath, out string resolvedPath ) {
        resolvedPath = string.Empty;
        if(string.IsNullOrWhiteSpace( relativePath )) {
            return false;
        }

        var candidate = Path.GetFullPath(
            Path.Combine( rootFullPath, relativePath.Replace( '/', Path.DirectorySeparatorChar ) )
        );

        if(!candidate.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    /// <summary>パス先頭のルートセグメントを何個スキップするかを取得する。</summary>
    private static int GetRootSegmentSkipCount( string[] segments ) {
        if(segments.Length == 0) {
            return 0;
        }

        if(string.Equals( segments[0], "DCSWorld", StringComparison.OrdinalIgnoreCase ) &&
           segments.Length >= 3 &&
           string.Equals( segments[1], "Mods", StringComparison.OrdinalIgnoreCase )) {
            return 3;
        }

        if(string.Equals( segments[0], "UserMissions", StringComparison.OrdinalIgnoreCase )) {
            return 1;
        }

        return 0;
    }

    /// <summary>ZIPとして扱う拡張子を含むかを判定する。</summary>
    private static bool IsZipLikeEntrySegment( string segment ) =>
        ZipLikeExtensions.Any( ext => segment.EndsWith( ext, StringComparison.OrdinalIgnoreCase ) );
}