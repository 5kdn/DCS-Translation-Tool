using System.Threading;
using System.Threading.Tasks;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Shared.Helpers;
using DcsTranslationTool.Shared.Models;

using FluentResults;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// ローカルディレクトリのファイル列挙と監視を行うサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public class FileEntryService( ILoggingService logger ) : IFileEntryService {
    #region Fields

    private FileSystemWatcher? watcher;
    private string _path = string.Empty;
    private readonly SemaphoreSlim _notifySemaphore = new( 1, 1 );
    private readonly object _debounceLock = new();
    private CancellationTokenSource? _debounceCts;

    private const int FileSystemDebounceMilliseconds = 200;

    #endregion

    /// <inheritdoc />
    public void Dispose() {
        logger.Debug( "ファイル監視サービスを破棄する。" );
        watcher?.Dispose();
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
            return Result.Fail( $"指定されたパスが存在しません: {path}" );
        }

        logger.Debug( $"ファイル階層を再帰的に列挙する。Path={path}" );

        List<FileEntry> result = [];
        try {
            foreach(var entryPath in Directory.GetFiles( path, "*", SearchOption.AllDirectories )) {
                var isDir = Directory.Exists( entryPath );
                var relative = Path.GetRelativePath( path, entryPath ).Replace( "\\", "/" );
                var name = Path.GetFileName( entryPath );
                string? sha = isDir ? null : await GitBlobSha1Helper.CalculateAsync( entryPath );
                result.Add( new LocalFileEntry( name, relative, isDir, sha ) );
            }
            logger.Info( $"ファイル階層の列挙が完了した。Count={result.Count}" );
            return Result.Ok<IEnumerable<FileEntry>>( result );
        }
        catch(Exception ex) {
            logger.Error( $"ファイル階層の列挙に失敗した。Path={path}", ex );
            return Result.Fail( ex.Message );
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

        logger.Info( $"監視ディレクトリのエントリ取得が完了した。Count={result.Value.Count()}" );
        return Result.Ok<IReadOnlyList<FileEntry>>( [.. result.Value] );
    }

    /// <summary>
    /// ファイルシステムの変更イベントを処理する。
    /// </summary>
    /// <param name="sender">イベント発生元</param>
    /// <param name="e">イベント引数</param>
    private void OnFileSystemChanged( object sender, FileSystemEventArgs e ) => ScheduleNotify();

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