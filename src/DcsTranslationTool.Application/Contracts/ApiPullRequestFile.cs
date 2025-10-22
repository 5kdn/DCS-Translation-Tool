namespace DcsTranslationTool.Application.Contracts;

/// <summary>Pull Requestで操作するファイルを定義する</summary>
/// <param name="Operation">適用する操作種別を示す</param>
/// <param name="Path">対象ファイルのパスを指定する</param>
/// <param name="Content">アップサート時に送信するコンテンツを指定する</param>
public sealed record ApiPullRequestFile(
    ApiPullRequestFileOperation Operation,
    string Path,
    string? Content );

/// <summary>Pull Requestで実行するファイル操作種別を表現する</summary>
public enum ApiPullRequestFileOperation {
    /// <summary>ファイルを作成または更新することを示す</summary>
    Upsert = 0,

    /// <summary>ファイルを削除することを示す</summary>
    Delete = 1,
}