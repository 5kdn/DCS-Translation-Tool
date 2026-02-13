using System.Security.Cryptography;
using System.Text;

namespace DcsTranslationTool.Shared.Helpers;

/// <summary>
/// Git の Blob オブジェクトにおける SHA1 を計算するヘルパー。
/// </summary>
public static class GitBlobSha1Helper {
    /// <summary>
    /// 指定したファイルの Blob-SHA1 を非同期に計算する。<br/>
    /// ファイルがロックされている場合は一定回数再試行し、読み取り不可のままの場合は <see langword="null"/> を返す。
    /// </summary>
    /// <param name="filePath">対象ファイルのパス</param>
    /// /// <param name="ct">キャンセル用トークン</param>
    /// <returns>計算された SHA1 読み取り不可の場合は <see langword="null"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="filePath"/> が <see langword="null"/> または空文字列の場合</exception>
    public static async Task<string?> CalculateAsync( string filePath, CancellationToken ct = default ) {
        const int retryCount = 10;
        const int bufferSize = 1024 * 128;
        for(var i = 0; i < retryCount; i++) {
            try {
                await using FileStream stream = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: bufferSize,
                    useAsync: true);
                var header = Encoding.UTF8.GetBytes( $"blob {stream.Length}\0" );
                using var sha1 = SHA1.Create();
                _ = sha1.TransformBlock( header, 0, header.Length, null, 0 );

                var buffer = new byte[bufferSize];
                while(true) {
                    var read = await stream.ReadAsync( buffer.AsMemory( 0, buffer.Length ), ct );
                    if(read == 0) {
                        break;
                    }

                    _ = sha1.TransformBlock( buffer, 0, read, null, 0 );
                }

                _ = sha1.TransformFinalBlock( [], 0, 0 );
                var hash = sha1.Hash;
                if(hash is null) {
                    return null;
                }

                return Convert.ToHexString( hash ).ToLowerInvariant();
            }
            catch(IOException) when(i < retryCount - 1) {
                await Task.Delay( 100, ct );
            }
            catch(IOException) {
                return null;
            }
        }
        return null;
    }
}