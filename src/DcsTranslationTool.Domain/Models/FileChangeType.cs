namespace DcsTranslationTool.Domain.Models;

/// <summary>
/// ファイルの変更種別。
/// </summary>
public enum FileChangeType {
    /// <summary>
    /// ローカル・リポジトリ両方に存在し、変更がない状態。
    /// </summary>
    Unchanged,

    /// <summary>
    /// リポジトリにのみ存在する状態。
    /// </summary>
    RepoOnly,

    /// <summary>
    /// ローカルにのみ存在する状態。
    /// </summary>
    LocalOnly,

    /// <summary>
    /// ローカル・リポジトリ両方に存在し、内容に差分が有る状態。
    /// </summary>
    Modified,
}