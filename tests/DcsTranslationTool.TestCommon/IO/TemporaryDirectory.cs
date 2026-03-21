using System.Text;

namespace DcsTranslationTool.TestCommon.IO;

/// <summary>
/// テスト用一時ディレクトリの作成と破棄を管理する。
/// </summary>
public sealed class TemporaryDirectory : IDisposable {
    /// <summary>
    /// 一時ディレクトリを初期化する。
    /// </summary>
    /// <param name="prefix">ディレクトリ名プレフィックス。</param>
    public TemporaryDirectory( string? prefix = null ) {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"{prefix ?? nameof( TemporaryDirectory )}_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( Path );
    }

    /// <summary>
    /// 一時ディレクトリの絶対パスを取得する。
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// 一時ディレクトリ配下のパスを結合する。
    /// </summary>
    /// <param name="relativePath">相対パス。</param>
    /// <returns>結合した絶対パスを返す。</returns>
    public string GetPath( string relativePath ) => System.IO.Path.Combine( Path, relativePath );

    /// <summary>
    /// 一時ディレクトリ配下に親ディレクトリを含めてファイルを書き込む。
    /// </summary>
    /// <param name="relativePath">相対パス。</param>
    /// <param name="content">書き込む内容。</param>
    /// <param name="encoding">文字エンコーディング。</param>
    /// <returns>作成したファイルの絶対パスを返す。</returns>
    public string CreateFile( string relativePath, string content, Encoding? encoding = null ) {
        var path = GetPath( relativePath );
        EnsureParentDirectory( path );
        File.WriteAllText( path, content, encoding ?? Encoding.UTF8 );
        return path;
    }

    /// <summary>
    /// 指定パスの親ディレクトリ作成を保証する。
    /// </summary>
    /// <param name="path">対象パス。</param>
    public static void EnsureParentDirectory( string path ) {
        var directory = System.IO.Path.GetDirectoryName( path );
        if(!string.IsNullOrEmpty( directory )) {
            Directory.CreateDirectory( directory );
        }
    }

    /// <summary>
    /// UTF-8 で文字列を書き込む。
    /// </summary>
    /// <param name="path">書き込み先パス。</param>
    /// <param name="content">書き込む内容。</param>
    public static void WriteUtf8Text( string path, string content ) {
        EnsureParentDirectory( path );
        File.WriteAllText( path, content, Encoding.UTF8 );
    }

    /// <summary>
    /// 一時ディレクトリを破棄する。
    /// </summary>
    public void Dispose() {
        if(Directory.Exists( Path )) {
            Directory.Delete( Path, true );
        }

        GC.SuppressFinalize( this );
    }
}