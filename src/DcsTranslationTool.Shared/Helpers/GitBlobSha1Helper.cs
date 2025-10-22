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
        for(var i = 0; i < retryCount; i++) {
            try {
                await using FileStream stream = new(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);
                var content = new byte[stream.Length];
                await stream.ReadExactlyAsync( content, ct );

                var header = $"blob {content.Length}\0";
                byte[] data = [.. Encoding.UTF8.GetBytes( header ), .. content];

                var hash = SHA1.HashData( data );
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