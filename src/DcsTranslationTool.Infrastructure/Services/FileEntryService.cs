using System.Collections.Concurrent;
using System.Diagnostics;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Shared.Helpers;
using DcsTranslationTool.Shared.Models;

using FluentResults;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// ローカルディレクトリのファイル列挙と監視を行うサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
/// <param name="fileEntryHashCacheService">ファイルハッシュキャッシュサービス。</param>
public class FileEntryService(
    ILoggingService logger,
    IFileEntryHashCacheService fileEntryHashCacheService
) : IFileEntryService {
    #region Fields

    private const int FileSystemDebounceMilliseconds = 200;
    private const int HashParallelismMin = 2;
    private const int HashParallelismMax = 8;

    private FileSystemWatcher? watcher;
    private string _path = string.Empty;
    private bool _isHashCacheEnabled = true;
    private readonly SemaphoreSlim _notifySemaphore = new( 1, 1 );
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _debounceCts;

    #endregion

    /// <inheritdoc />
    public void Dispose() {
        logger.Debug( "ファイル監視サービスを破棄する。" );
        watcher?.Dispose();
        if(fileEntryHashCacheService is IDisposable disposable) {
            disposable.Dispose();
        }

        lock(_debounceLock) {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = null;
        }
        GC.SuppressFinalize( this );
        logger.Debug( "ファイル監視サービスの破棄が完了した。" );
    }

    /// <inheritdoc />
    public event Func<IReadOnlyList<FileEntry>, Task>? EntriesChanged;

    /// <inheritdoc />
    public async Task<Result<IEnumerable<FileEntry>>> GetChildrenRecursiveAsync( string path ) {
        if(!Path.Exists( path )) {
            logger.Warn( $"ファイル列挙の対象パスが存在しない。Path={path}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"指定されたパスが存在しません: {path}", "FILE_ENTRY_PATH_NOT_FOUND" ) );
        }

        logger.Debug( $"ファイル階層を再帰的に列挙する。Path={path}" );
        var stopwatch = Stopwatch.StartNew();

        try {
            var useHashCache = true;
            try {
                fileEntryHashCacheService.ConfigureRoot( path );
                _isHashCacheEnabled = true;
            }
            catch(Exception ex) {
                var isWatchPath = !string.IsNullOrEmpty( _path ) && string.Equals( _path, path, StringComparison.OrdinalIgnoreCase );
                if(!isWatchPath) {
                    logger.Error( $"ハッシュキャッシュ初期化に失敗した。Path={path}", ex );
                    return Result.Fail( ResultErrorFactory.Unexpected( ex, "FILE_ENTRY_CACHE_INIT_EXCEPTION" ) );
                }

                _isHashCacheEnabled = false;
                useHashCache = false;
                logger.Warn( $"ハッシュキャッシュ初期化に失敗したためキャッシュなしで列挙を継続する。Path={path}", ex );
            }

            var allFilePaths = Directory
                .EnumerateFiles( path, "*", SearchOption.AllDirectories )
                .ToArray();
            var existingRelativePaths = new ConcurrentDictionary<string, byte>( StringComparer.OrdinalIgnoreCase );
            var entries = new ConcurrentBag<FileEntry>();
            var parallelism = Math.Clamp( Environment.ProcessorCount, HashParallelismMin, HashParallelismMax );
            var cacheHitCount = 0;
            var cacheMissCount = 0;

            await Parallel.ForEachAsync(
                allFilePaths,
                new ParallelOptions
                {
                    MaxDegreeOfParallelism = parallelism
                },
                async ( entryPath, cancellationToken ) => {
                    var relativePath = Path.GetRelativePath( path, entryPath ).Replace( "\\", "/", StringComparison.Ordinal );
                    existingRelativePaths.TryAdd( relativePath, 0 );

                    var fileInfo = new FileInfo( entryPath );
                    var lastWriteUtc = fileInfo.LastWriteTimeUtc;
                    var fileSize = fileInfo.Length;

                    if(!useHashCache || !fileEntryHashCacheService.TryGetSha( relativePath, fileSize, lastWriteUtc, out var sha )) {
                        Interlocked.Increment( ref cacheMissCount );
                        sha = await GitBlobSha1Helper.CalculateAsync( entryPath, cancellationToken );
                        if(useHashCache) {
                            fileEntryHashCacheService.Upsert( relativePath, fileSize, lastWriteUtc, sha );
                        }
                    }
                    else {
                        Interlocked.Increment( ref cacheHitCount );
                    }

                    var name = Path.GetFileName( entryPath );
                    entries.Add( new LocalFileEntry( name, relativePath, false, sha ) );
                }
            );

            if(useHashCache) {
                fileEntryHashCacheService.Prune( existingRelativePaths.Keys.ToHashSet( StringComparer.OrdinalIgnoreCase ) );
            }
            var result = entries.OrderBy( entry => entry.Path, StringComparer.Ordinal ).ToArray();
            logger.Info( $"ファイル階層の列挙が完了した。Count={result.Length}, CacheHit={cacheHitCount}, CacheMiss={cacheMissCount}, ElapsedMs={stopwatch.ElapsedMilliseconds}" );
            return Result.Ok<IEnumerable<FileEntry>>( result );
        }
        catch(Exception ex) {
            logger.Error( $"ファイル階層の列挙に失敗した。Path={path}", ex );
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "FILE_ENTRY_ENUMERATION_EXCEPTION" ) );
        }
    }

    /// <inheritdoc />
    public void Watch( string path ) {
        _path = path;
        watcher?.Dispose();
        if(!Directory.Exists( path )) {
            logger.Warn( $"監視対象ディレクトリが存在しないため監視を開始しない。Path={path}" );
            return;
        }

        try {
            fileEntryHashCacheService.ConfigureRoot( path );
            _isHashCacheEnabled = true;
        }
        catch(Exception ex) {
            _isHashCacheEnabled = false;
            logger.Warn( $"ハッシュキャッシュ初期化に失敗したためキャッシュなしで監視を継続する。Path={path}", ex );
        }

        logger.Info( $"ファイル監視を開始する。Path={path}" );

        watcher = new FileSystemWatcher( path ) { IncludeSubdirectories = true };
        watcher.Changed += OnFileSystemChanged;
        watcher.Created += OnFileSystemChanged;
        watcher.Deleted += OnFileSystemChanged;
        watcher.Renamed += OnFileSystemChanged;
        watcher.EnableRaisingEvents = true;

        logger.Debug( "ファイル監視が有効化された。" );
        ScheduleNotify();
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FileEntry>>> GetEntriesAsync() {
        if(string.IsNullOrEmpty( _path )) {
            logger.Debug( "監視対象が未設定のため空のエントリを返す。" );
            return Result.Ok<IReadOnlyList<FileEntry>>( [] );
        }

        logger.Debug( $"監視ディレクトリのエントリを取得する。Path={_path}" );
        var result = await GetChildrenRecursiveAsync( _path );
        if(result.IsFailed) {
            logger.Error( $"監視ディレクトリのエントリ取得に失敗した。Path={_path}. Errors={string.Join( ", ", result.Errors.Select( e => e.Message ) )}" );
            return Result.Fail<IReadOnlyList<FileEntry>>( result.Errors );
        }

        var entries = result.Value.ToArray();
        logger.Info( $"監視ディレクトリのエントリ取得が完了した。Count={entries.Length}" );
        return Result.Ok<IReadOnlyList<FileEntry>>( entries );
    }

    /// <summary>
    /// ファイルシステムの変更イベントを処理する。
    /// </summary>
    /// <param name="sender">イベント発生元</param>
    /// <param name="e">イベント引数</param>
    private void OnFileSystemChanged( object sender, FileSystemEventArgs e ) {
        if(e.ChangeType is WatcherChangeTypes.Deleted && !string.IsNullOrWhiteSpace( e.FullPath ) && !string.IsNullOrEmpty( _path )) {
            var relativePath = Path.GetRelativePath( _path, e.FullPath ).Replace( "\\", "/", StringComparison.Ordinal );
            if(!relativePath.StartsWith( "..", StringComparison.Ordinal )) {
                if(_isHashCacheEnabled) {
                    fileEntryHashCacheService.Remove( relativePath );
                }
            }
        }

        ScheduleNotify();
    }

    /// <summary>
    /// ファイルシステムイベントをデバウンスして通知する。
    /// </summary>
    private void ScheduleNotify() {
        lock(_debounceLock) {
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            _ = Task.Run( async () => {
                try {
                    await Task.Delay( FileSystemDebounceMilliseconds, token );
                    if(token.IsCancellationRequested) return;
                    await NotifyAsync();
                }
                catch(OperationCanceledException) {
                    // デバウンスのキャンセルは想定内
                }
            }, token );
        }
    }

    /// <summary>
    /// 変更通知イベントを発火する。
    /// </summary>
    private async Task NotifyAsync() {
        if(EntriesChanged is null) return;
        await _notifySemaphore.WaitAsync();
        try {
            logger.Trace( "ファイル変更イベントを通知する。" );
            var entries = await GetEntriesAsync();
            if(entries.IsFailed) {
                logger.Warn( "ファイル変更イベントの通知前にエントリ取得が失敗した。" );
                return;
            }
            logger.Debug( $"ファイル変更イベントを発行する。Count={entries.Value.Count}" );
            await EntriesChanged.Invoke( entries.Value );
        }
        finally {
            _notifySemaphore.Release();
        }
    }
}