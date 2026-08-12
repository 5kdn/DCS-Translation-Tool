using DcsTranslationTool.Application.Interfaces;

using Microsoft.Data.Sqlite;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// SQLite を利用してファイル SHA1 キャッシュを永続化する。
/// </summary>
public sealed class FileEntryHashCacheService(
    ILoggingService logger,
    string? preferredBaseDirectory = null,
    string? fallbackBaseDirectory = null
) : IFileEntryHashCacheService, IDisposable {
    private const string LegacyCacheDirectoryName = ".dcs-translation-cache";
    private const string CacheDirectoryName = "cache";
    private const string CacheFileName = "hashcache.db";

    private readonly object _gate = new();
    private readonly Dictionary<string, CacheEntry> _index = new( StringComparer.OrdinalIgnoreCase );
    private readonly string _preferredBaseDirectory = ResolvePreferredBaseDirectory( preferredBaseDirectory );
    private readonly string _fallbackBaseDirectory = ResolveFallbackBaseDirectory( fallbackBaseDirectory );

    private SqliteConnection? _connection;
    private string _currentRootPath = string.Empty;

    /// <inheritdoc />
    public void ConfigureRoot( string rootPath ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( rootPath );
        var normalizedRoot = Path.GetFullPath( rootPath );

        lock(_gate) {
            if(string.Equals( _currentRootPath, normalizedRoot, StringComparison.OrdinalIgnoreCase ) && _connection is not null) {
                return;
            }

            DisposeConnection();
            _index.Clear();

            var legacyDbPath = Path.Combine( _preferredBaseDirectory, LegacyCacheDirectoryName, CacheFileName );
            var cacheDbPath = Path.Combine( _fallbackBaseDirectory, CacheDirectoryName, CacheFileName );
            var migrationCopied = TryCopyLegacyCache( legacyDbPath, cacheDbPath, out var migrationException );
            if(migrationException is not null) {
                logger.Warn( $"旧ハッシュキャッシュの移行準備に失敗したため新保存先で再構築する。LegacyPath={legacyDbPath}", migrationException );
            }

            if(!TryInitializeCache( cacheDbPath, normalizedRoot, out var connection, out var exception )) {
                if(migrationCopied) {
                    DeleteCacheFamily( cacheDbPath );
                    if(!TryInitializeCache( cacheDbPath, normalizedRoot, out connection, out var rebuildException )) {
                        throw new InvalidOperationException( $"キャッシュDBの再構築に失敗した。Path={cacheDbPath}", rebuildException );
                    }
                    logger.Warn( $"移行済みキャッシュを初期化できなかったため新保存先で再構築した。Path={cacheDbPath}", exception );
                    migrationCopied = false;
                }
                else {
                    throw new InvalidOperationException( $"キャッシュDBの初期化に失敗した。Path={cacheDbPath}", exception );
                }
            }

            _connection = connection!;
            if(migrationCopied) {
                DeleteCacheFamily( legacyDbPath );
                TryDeleteEmptyDirectory( Path.GetDirectoryName( legacyDbPath ) );
                logger.Info( $"旧ハッシュキャッシュを新保存先へ移行した。Path={cacheDbPath}" );
            }
            else {
                logger.Info( $"ハッシュキャッシュ保存先をローカルアプリデータに設定した。Path={cacheDbPath}" );
            }

            _currentRootPath = normalizedRoot;
            logger.Info( $"ハッシュキャッシュを初期化した。Root={_currentRootPath}, Count={_index.Count}" );
        }
    }

    /// <inheritdoc />
    public bool TryGetSha( string relativePath, long fileSize, DateTime lastWriteUtc, out string? sha ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( relativePath );
        var normalizedPath = NormalizeRelativePath( relativePath );
        var ticks = lastWriteUtc.ToUniversalTime().Ticks;

        lock(_gate) {
            if(_index.TryGetValue( normalizedPath, out var entry )
                && entry.FileSize == fileSize
                && entry.LastWriteUtcTicks == ticks
                && !string.IsNullOrWhiteSpace( entry.Sha )) {
                sha = entry.Sha;
                return true;
            }
        }

        sha = null;
        return false;
    }

    /// <inheritdoc />
    public void Upsert( string relativePath, long fileSize, DateTime lastWriteUtc, string? sha ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( relativePath );
        var normalizedPath = NormalizeRelativePath( relativePath );
        var ticks = lastWriteUtc.ToUniversalTime().Ticks;
        var updatedAtTicks = DateTime.UtcNow.Ticks;

        lock(_gate) {
            EnsureConnection();

            using var command = _connection!.CreateCommand();
            command.CommandText = """
                                  INSERT INTO file_hash_cache(root_path, path, file_size, last_write_utc_ticks, sha1, updated_at_utc_ticks)
                                  VALUES($rootPath, $path, $fileSize, $lastWriteUtcTicks, $sha1, $updatedAtUtcTicks)
                                  ON CONFLICT(root_path, path) DO UPDATE SET
                                      file_size = excluded.file_size,
                                      last_write_utc_ticks = excluded.last_write_utc_ticks,
                                      sha1 = excluded.sha1,
                                      updated_at_utc_ticks = excluded.updated_at_utc_ticks;
                                  """;
            command.Parameters.AddWithValue( "$rootPath", _currentRootPath );
            command.Parameters.AddWithValue( "$path", normalizedPath );
            command.Parameters.AddWithValue( "$fileSize", fileSize );
            command.Parameters.AddWithValue( "$lastWriteUtcTicks", ticks );
            command.Parameters.AddWithValue( "$sha1", (object?)sha ?? DBNull.Value );
            command.Parameters.AddWithValue( "$updatedAtUtcTicks", updatedAtTicks );
            command.ExecuteNonQuery();

            _index[normalizedPath] = new CacheEntry( fileSize, ticks, sha );
        }
    }

    /// <inheritdoc />
    public void Remove( string relativePath ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( relativePath );
        var normalizedPath = NormalizeRelativePath( relativePath );

        lock(_gate) {
            if(_connection is null) {
                _index.Remove( normalizedPath );
                return;
            }

            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM file_hash_cache WHERE root_path = $rootPath AND path = $path;";
            command.Parameters.AddWithValue( "$rootPath", _currentRootPath );
            command.Parameters.AddWithValue( "$path", normalizedPath );
            command.ExecuteNonQuery();
            _index.Remove( normalizedPath );
        }
    }

    /// <inheritdoc />
    public void Prune( IReadOnlySet<string> existingRelativePaths ) {
        ArgumentNullException.ThrowIfNull( existingRelativePaths );

        lock(_gate) {
            if(_connection is null || _index.Count == 0) {
                return;
            }

            var normalizedExistingPaths = new HashSet<string>(
                existingRelativePaths.Select( NormalizeRelativePath ),
                StringComparer.OrdinalIgnoreCase
            );
            var stalePaths = _index.Keys
                .Where( path => !normalizedExistingPaths.Contains( path ) )
                .ToArray();
            if(stalePaths.Length == 0) {
                return;
            }

            using var transaction = _connection.BeginTransaction();
            foreach(var stalePath in stalePaths) {
                using var command = _connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM file_hash_cache WHERE root_path = $rootPath AND path = $path;";
                command.Parameters.AddWithValue( "$rootPath", _currentRootPath );
                command.Parameters.AddWithValue( "$path", stalePath );
                command.ExecuteNonQuery();
                _index.Remove( stalePath );
            }
            transaction.Commit();
            logger.Info( $"未使用のハッシュキャッシュを削除した。Count={stalePaths.Length}" );
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        lock(_gate) {
            DisposeConnection();
            _index.Clear();
            _currentRootPath = string.Empty;
        }
    }

    /// <summary>
    /// 接続が初期化済みであることを保証する。
    /// </summary>
    private void EnsureConnection() {
        if(_connection is null) {
            throw new InvalidOperationException( "キャッシュ接続が初期化されていない。" );
        }
    }

    /// <summary>
    /// 相対パスを正規化する。
    /// </summary>
    /// <param name="relativePath">正規化対象パス。</param>
    /// <returns>正規化した相対パス。</returns>
    private static string NormalizeRelativePath( string relativePath ) =>
        relativePath.Replace( "\\", "/", StringComparison.Ordinal ).TrimStart( '/' );

    /// <summary>
    /// DB 接続を安全に破棄する。
    /// </summary>
    private void DisposeConnection() {
        if(_connection is null) {
            return;
        }

        try {
            _connection.Dispose();
        }
        catch(Exception ex) {
            logger.Warn( "ハッシュキャッシュ接続の破棄時に例外が発生した。", ex );
        }
        finally {
            _connection = null;
        }
    }

    /// <summary>
    /// 接続確立を試行する。
    /// </summary>
    /// <param name="dbPath">DB ファイルパス。</param>
    /// <param name="connection">確立済み接続。</param>
    /// <param name="exception">失敗時の例外。</param>
    /// <returns>接続できた場合は <see langword="true"/> を返す。</returns>
    private static bool TryOpenConnection( string dbPath, out SqliteConnection? connection, out Exception? exception ) {
        connection = null;
        exception = null;
        try {
            var directory = Path.GetDirectoryName( dbPath ) ?? throw new InvalidOperationException( "DBディレクトリを解決できない。" );
            Directory.CreateDirectory( directory );
            connection = new SqliteConnection( $"Data Source={dbPath};Pooling=False" );
            connection.Open();
            return true;
        }
        catch(Exception ex) {
            connection?.Dispose();
            connection = null;
            exception = ex;
            return false;
        }
    }

    /// <summary>
    /// DBの接続、スキーマ初期化、インデックス読込を一体として試行する。
    /// </summary>
    /// <param name="dbPath">DBファイルパス。</param>
    /// <param name="rootPath">読み込むルートパス。</param>
    /// <param name="connection">初期化済み接続。</param>
    /// <param name="exception">失敗時の例外。</param>
    /// <returns>利用可能な状態まで初期化できた場合は <see langword="true"/> を返す。</returns>
    private bool TryInitializeCache( string dbPath, string rootPath, out SqliteConnection? connection, out Exception? exception ) {
        if(!TryOpenConnection( dbPath, out connection, out exception )) {
            return false;
        }

        try {
            InitializeSchema( connection! );
            LoadIndex( connection!, rootPath );
            return true;
        }
        catch(Exception ex) when(ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or InvalidCastException or FormatException or OverflowException) {
            connection?.Dispose();
            connection = null;
            exception = ex;
            _index.Clear();
            return false;
        }
    }

    /// <summary>
    /// テーブルとインデックスを初期化する。
    /// </summary>
    /// <param name="connection">初期化対象接続。</param>
    private static void InitializeSchema( SqliteConnection connection ) {
        using(var pragma = connection.CreateCommand()) {
            pragma.CommandText = """
                                PRAGMA journal_mode = WAL;
                                PRAGMA synchronous = NORMAL;
                                """;
            pragma.ExecuteNonQuery();
        }

        EnsureSchemaV2( connection );

        using var create = connection.CreateCommand();
        create.CommandText = """
                             CREATE TABLE IF NOT EXISTS file_hash_cache (
                                 root_path TEXT NOT NULL,
                                 path TEXT NOT NULL,
                                 file_size INTEGER NOT NULL,
                                 last_write_utc_ticks INTEGER NOT NULL,
                                 sha1 TEXT NULL,
                                 updated_at_utc_ticks INTEGER NOT NULL,
                                 PRIMARY KEY(root_path, path)
                             );
                             CREATE INDEX IF NOT EXISTS ix_file_hash_cache_updated_at
                             ON file_hash_cache(root_path, updated_at_utc_ticks);
                             """;
        create.ExecuteNonQuery();
    }

    /// <summary>
    /// DB からインデックスを読み込む。
    /// </summary>
    /// <param name="connection">読込対象接続。</param>
    /// <param name="rootPath">読み込むルートパス。</param>
    private void LoadIndex( SqliteConnection connection, string rootPath ) {
        using var select = connection.CreateCommand();
        select.CommandText = """
                             SELECT path, file_size, last_write_utc_ticks, sha1
                             FROM file_hash_cache
                             WHERE root_path = $rootPath;
                             """;
        select.Parameters.AddWithValue( "$rootPath", rootPath );
        using var reader = select.ExecuteReader();
        while(reader.Read()) {
            var relativePath = reader.GetString( 0 );
            var fileSize = reader.GetInt64( 1 );
            var lastWriteTicks = reader.GetInt64( 2 );
            var sha = reader.IsDBNull( 3 ) ? null : reader.GetString( 3 );
            _index[relativePath] = new CacheEntry( fileSize, lastWriteTicks, sha );
        }
    }

    /// <summary>
    /// 旧スキーマを検出した場合に v2 スキーマへ移行する。
    /// </summary>
    /// <param name="connection">対象接続。</param>
    private static void EnsureSchemaV2( SqliteConnection connection ) {
        using var check = connection.CreateCommand();
        check.CommandText = """
                             SELECT COUNT(*)
                             FROM sqlite_master
                             WHERE type='table' AND name='file_hash_cache';
                             """;
        var exists = Convert.ToInt64( check.ExecuteScalar() ) > 0;
        if(!exists) {
            return;
        }

        using var tableInfo = connection.CreateCommand();
        tableInfo.CommandText = "PRAGMA table_info(file_hash_cache);";
        using var reader = tableInfo.ExecuteReader();
        var hasRootPath = false;
        while(reader.Read()) {
            if(string.Equals( reader.GetString( 1 ), "root_path", StringComparison.OrdinalIgnoreCase )) {
                hasRootPath = true;
                break;
            }
        }

        if(hasRootPath) {
            return;
        }

        using var migrate = connection.CreateCommand();
        migrate.CommandText = """
                              ALTER TABLE file_hash_cache RENAME TO file_hash_cache_v1;
                              CREATE TABLE file_hash_cache (
                                  root_path TEXT NOT NULL,
                                  path TEXT NOT NULL,
                                  file_size INTEGER NOT NULL,
                                  last_write_utc_ticks INTEGER NOT NULL,
                                  sha1 TEXT NULL,
                                  updated_at_utc_ticks INTEGER NOT NULL,
                                  PRIMARY KEY(root_path, path)
                              );
                              INSERT INTO file_hash_cache(root_path, path, file_size, last_write_utc_ticks, sha1, updated_at_utc_ticks)
                              SELECT '' AS root_path, path, file_size, last_write_utc_ticks, sha1, updated_at_utc_ticks
                              FROM file_hash_cache_v1;
                              DROP TABLE file_hash_cache_v1;
                              """;
        migrate.ExecuteNonQuery();
    }

    /// <summary>
    /// 優先保存先ベースディレクトリを解決する。
    /// </summary>
    /// <param name="preferredBaseDirectory">明示指定の優先パス。</param>
    /// <returns>優先保存先ベースディレクトリ。</returns>
    private static string ResolvePreferredBaseDirectory( string? preferredBaseDirectory ) {
        if(!string.IsNullOrWhiteSpace( preferredBaseDirectory )) {
            return Path.GetFullPath( preferredBaseDirectory );
        }

        var processPath = Environment.ProcessPath;
        var baseDirectory = !string.IsNullOrWhiteSpace( processPath )
            ? Path.GetDirectoryName( processPath )
            : null;
        baseDirectory ??= AppContext.BaseDirectory;
        return Path.GetFullPath( baseDirectory );
    }

    /// <summary>
    /// フォールバック保存先ベースディレクトリを解決する。
    /// </summary>
    /// <param name="fallbackBaseDirectory">明示指定のフォールバックパス。</param>
    /// <returns>フォールバック保存先ベースディレクトリ。</returns>
    private static string ResolveFallbackBaseDirectory( string? fallbackBaseDirectory ) {
        if(!string.IsNullOrWhiteSpace( fallbackBaseDirectory )) {
            return Path.GetFullPath( fallbackBaseDirectory );
        }

#if E2E_TESTING
        return ApplicationUpdateE2eConfiguration.Load().CacheDirectory;
#else
        var localAppData = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
        return Path.Combine( localAppData, "DcsTranslationTool" );
#endif
    }

    /// <summary>
    /// 旧キャッシュ一式を新保存先へ複製する。
    /// </summary>
    private static bool TryCopyLegacyCache( string legacyDbPath, string cacheDbPath, out Exception? exception ) {
        exception = null;
        if(File.Exists( cacheDbPath ) || !File.Exists( legacyDbPath )) {
            return false;
        }

        try {
            Directory.CreateDirectory( Path.GetDirectoryName( cacheDbPath )! );
            foreach(var suffix in new[] { string.Empty, "-wal", "-shm" }) {
                var source = legacyDbPath + suffix;
                if(File.Exists( source )) {
                    File.Copy( source, cacheDbPath + suffix, false );
                }
            }
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            DeleteCacheFamily( cacheDbPath );
            exception = ex;
            return false;
        }
    }

    /// <summary>
    /// DB、WAL、SHM ファイルを削除する。
    /// </summary>
    private static void DeleteCacheFamily( string dbPath ) {
        foreach(var suffix in new[] { string.Empty, "-wal", "-shm" }) {
            try {
                var path = dbPath + suffix;
                if(File.Exists( path )) {
                    File.Delete( path );
                }
            }
            catch(IOException) { }
            catch(UnauthorizedAccessException) { }
        }
    }

    /// <summary>
    /// 空の旧キャッシュフォルダーを削除する。
    /// </summary>
    private static void TryDeleteEmptyDirectory( string? directory ) {
        try {
            if(!string.IsNullOrWhiteSpace( directory ) && Directory.Exists( directory ) && !Directory.EnumerateFileSystemEntries( directory ).Any()) {
                Directory.Delete( directory );
            }
        }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }

    /// <summary>
    /// キャッシュ要素を表す。
    /// </summary>
    /// <param name="FileSize">ファイルサイズ。</param>
    /// <param name="LastWriteUtcTicks">更新日時（Ticks）。</param>
    /// <param name="Sha">SHA1。</param>
    private sealed record CacheEntry( long FileSize, long LastWriteUtcTicks, string? Sha );
}