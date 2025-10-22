namespace DcsTranslationTool.Shared.Models;

/// <summary>
/// ファイルまたはディレクトリのエントリを表現するクラス。
/// </summary>
/// <param name="name">名称</param>
/// <param name="path">翻訳ルートまたはリポジトリルートからのパス</param>
/// <param name="isDirectory">ディレクトリかどうか</param>
/// <param name="localSha">ローカルのSHA1</param>
/// <param name="repoSha">リポジトリのSHA1</param>
public class FileEntry(
    string name,
    string path,
    bool isDirectory,
    string? localSha = null,
    string? repoSha = null
) {
    /// <summary>
    /// ファイルまたはディレクトリの名称を取得する。
    /// </summary>
    public string Name => name;

    /// <summary>
    /// 翻訳ルートまたはリポジトリルートからのパスを取得する。
    /// </summary>
    public string Path => path;

    /// <summary>
    /// ディレクトリかどうかを取得する。<br/>
    /// ディレクトリの場合は <see langword="true"/>
    /// ファイルの場合は <see langword="false"/> とする。
    /// </summary>
    public bool IsDirectory => isDirectory;

    /// <summary>
    /// ローカルのSHA1を取得する。
    /// </summary>
    public string? LocalSha { get; set; } = localSha;

    /// <summary>
    /// リポジトリのSHA1を取得する。
    /// </summary>
    public string? RepoSha { get; set; } = repoSha;

}

/// <summary>
/// リポジトリのファイルまたはディレクトリのエントリを表現するモデル。
/// </summary>
/// <param name="name">名称</param>
/// <param name="path">翻訳ルートまたはリポジトリルートからのパス</param>
/// <param name="isDirectory">ディレクトリかどうか</param>
/// <param name="localSha">ローカルのSHA1</param>
public class LocalFileEntry( string name, string path, bool isDirectory, string? localSha = null ) :
    FileEntry( name, path, isDirectory, localSha, null ) { }

/// <summary>
/// リポジトリのファイルまたはディレクトリのエントリを表現するモデル。
/// </summary>
/// <param name="name">名称</param>
/// <param name="path">翻訳ルートまたはリポジトリルートからのパス</param>
/// <param name="isDirectory">ディレクトリかどうか</param>
/// <param name="repoSha">リポジトリのSHA1</param>
public class RepoFileEntry( string name, string path, bool isDirectory, string? repoSha = null ) :
    FileEntry( name, path, isDirectory, null, repoSha ) { }