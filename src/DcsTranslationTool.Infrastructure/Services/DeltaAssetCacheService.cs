using System.Security.Cryptography;
using System.Text.RegularExpressions;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// 検証済み差分アセットの安全なローカルキャッシュを管理する。
/// </summary>
/// <param name="logger">ロギングサービスを受け取る。</param>
/// <param name="updatesDirectory">更新作業ルートを受け取る。</param>
public sealed partial class DeltaAssetCacheService( ILoggingService logger, string updatesDirectory ) {
    private const string CacheDirectoryName = "cache";
    private const long CacheMaximumSizeBytes = 512L * 1024 * 1024;
    private static readonly TimeSpan CacheRetention = TimeSpan.FromDays( 30 );
    private readonly string _updatesDirectory = Path.GetFullPath( updatesDirectory );

    /// <summary>
    /// 検証済みキャッシュから指定した差分アセットを復元する。
    /// </summary>
    /// <param name="asset">復元する差分アセットを受け取る。</param>
    /// <param name="destinationPath">復元先の絶対パスを受け取る。</param>
    /// <returns>キャッシュを検証して復元できた場合はtrueを返す。</returns>
    public async Task<bool> TryRestoreAsync( ApplicationUpdateAsset asset, string destinationPath, CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();
        if(!TryParseDigest( asset.Sha256Digest, out var hash )) {
            return false;
        }

        var cachePath = this.GetCachePath( hash );
        if(!this.IsSafeCachePath( cachePath ) || !File.Exists( cachePath )) {
            return false;
        }

        try {
            cancellationToken.ThrowIfCancellationRequested();
            if(new FileInfo( cachePath ).Length != asset.Size ||
                !string.Equals( await CalculateSha256Async( cachePath, cancellationToken ).ConfigureAwait( false ), hash, StringComparison.OrdinalIgnoreCase )) {
                TryDeleteFile( cachePath );
                return false;
            }

            var parent = Path.GetDirectoryName( destinationPath );
            if(!string.IsNullOrWhiteSpace( parent )) {
                Directory.CreateDirectory( parent );
            }

            await CopyFileAsync( cachePath, destinationPath, overwrite: true, cancellationToken ).ConfigureAwait( false );
            File.SetLastWriteTimeUtc( cachePath, DateTime.UtcNow );
            return true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) {
            logger.Warn( $"差分アセットのキャッシュを再利用できないため再取得する。Asset={asset.Name}", ex );
            return false;
        }
    }

    /// <summary>
    /// 検証済み差分アセットをキャッシュへ保存する。
    /// </summary>
    /// <param name="asset">保存する差分アセットを受け取る。</param>
    /// <param name="sourcePath">検証済みアセットの絶対パスを受け取る。</param>
    public async Task StoreAsync( ApplicationUpdateAsset asset, string sourcePath, CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();
        if(!TryParseDigest( asset.Sha256Digest, out var hash )) {
            return;
        }

        try {
            var cachePath = this.GetCachePath( hash );
            if(new FileInfo( sourcePath ).Length > CacheMaximumSizeBytes || !this.IsSafeCachePath( cachePath )) {
                return;
            }

            Directory.CreateDirectory( Path.GetDirectoryName( cachePath )! );
            if(!this.IsSafeCachePath( cachePath )) {
                return;
            }

            var temporaryPath = $"{cachePath}.{Guid.NewGuid():N}.partial";
            try {
                await CopyFileAsync( sourcePath, temporaryPath, overwrite: false, cancellationToken ).ConfigureAwait( false );
                File.Move( temporaryPath, cachePath, true );
                await Task.Run( this.Prune, cancellationToken ).ConfigureAwait( false );
            }
            finally {
                TryDeleteFile( temporaryPath );
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException) {
            logger.Warn( $"差分アセットのキャッシュを保存できなかった。Asset={asset.Name}", ex );
        }
    }

    /// <summary>
    /// 差分アセットキャッシュを有効期限と容量上限に従って整理する。
    /// </summary>
    public void Prune() {
        var cacheDirectory = this.CacheDirectory;
        if(!Directory.Exists( cacheDirectory ) ||
            !this.IsSafeCachePath( Path.Combine( cacheDirectory, $"{new string( '0', 64 )}.zip" ) )) {
            return;
        }

        try {
            var expiration = DateTime.UtcNow - CacheRetention;
            var retainedFiles = new List<FileInfo>();
            foreach(var path in Directory.GetFiles( cacheDirectory, "*", SearchOption.TopDirectoryOnly )) {
                if(ContainsReparsePoint( path )) {
                    logger.Warn( $"再解析ポイントを含む差分アセットキャッシュ項目を整理しない。Cache={path}" );
                    continue;
                }

                var file = new FileInfo( path );
                if(string.Equals( file.Extension, ".partial", StringComparison.OrdinalIgnoreCase )) {
                    if(file.LastWriteTimeUtc <= expiration) {
                        TryDeleteFile( path );
                    }
                    continue;
                }

                if(!string.Equals( file.Extension, ".zip", StringComparison.OrdinalIgnoreCase ) ||
                    !Sha256Regex().IsMatch( Path.GetFileNameWithoutExtension( path ) )) {
                    continue;
                }

                if(file.LastWriteTimeUtc <= expiration) {
                    TryDeleteFile( path );
                    continue;
                }

                retainedFiles.Add( file );
            }

            var totalSize = retainedFiles.Aggregate<FileInfo, long>( 0, ( total, file ) => checked( total + file.Length ) );
            foreach(var file in retainedFiles.OrderBy( file => file.LastWriteTimeUtc )) {
                if(totalSize <= CacheMaximumSizeBytes) {
                    break;
                }

                try {
                    File.Delete( file.FullName );
                    totalSize -= file.Length;
                }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
                    logger.Warn( $"差分アセットキャッシュ項目を削除できなかった。Cache={file.FullName}", ex );
                }
            }
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or OverflowException) {
            logger.Warn( $"差分アセットキャッシュを整理できなかった。Cache={cacheDirectory}", ex );
        }
    }

    private string CacheDirectory => Path.Combine( this._updatesDirectory, CacheDirectoryName );

    private string GetCachePath( string hash ) => Path.Combine( this.CacheDirectory, $"{hash.ToLowerInvariant()}.zip" );

    private bool IsSafeCachePath( string cachePath ) {
        var cacheDirectory = Path.GetDirectoryName( cachePath );
        return !string.IsNullOrWhiteSpace( cacheDirectory ) &&
            PathsEqual( cacheDirectory, this.CacheDirectory ) &&
            !ContainsReparsePoint( this._updatesDirectory ) &&
            !ContainsReparsePoint( cacheDirectory ) &&
            !ContainsReparsePoint( cachePath );
    }

    private static bool TryParseDigest( string digest, out string hash ) {
        const string prefix = "sha256:";
        hash = digest.StartsWith( prefix, StringComparison.OrdinalIgnoreCase ) ? digest[prefix.Length..] : string.Empty;
        return Sha256Regex().IsMatch( hash );
    }

    private static string CalculateSha256( string path ) {
        using var stream = File.OpenRead( path );
        return Convert.ToHexString( SHA256.HashData( stream ) ).ToLowerInvariant();
    }

    private static async Task<string> CalculateSha256Async( string path, CancellationToken cancellationToken ) {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan );
        return Convert.ToHexString( await SHA256.HashDataAsync( stream, cancellationToken ).ConfigureAwait( false ) ).ToLowerInvariant();
    }

    private static async Task CopyFileAsync( string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken ) {
        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan );
        await using var destination = new FileStream(
            destinationPath,
            overwrite ? FileMode.Create : FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan );
        await source.CopyToAsync( destination, 81920, cancellationToken ).ConfigureAwait( false );
        await destination.FlushAsync( cancellationToken ).ConfigureAwait( false );
    }

    private static bool ContainsReparsePoint( string path ) {
        var fullPath = Path.GetFullPath( path );
        return (Directory.Exists( fullPath ) || File.Exists( fullPath )) &&
            File.GetAttributes( fullPath ).HasFlag( FileAttributes.ReparsePoint );
    }

    private static bool PathsEqual( string left, string right ) => string.Equals(
        Path.GetFullPath( left ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ),
        Path.GetFullPath( right ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ),
        StringComparison.OrdinalIgnoreCase );

    private static void TryDeleteFile( string path ) {
        try {
            if(File.Exists( path )) {
                File.Delete( path );
            }
        }
        catch(IOException) {
        }
        catch(UnauthorizedAccessException) {
        }
    }

    /// <summary>
    /// SHA-256文字列を検証する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex( "^[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant )]
    private static partial Regex Sha256Regex();
}