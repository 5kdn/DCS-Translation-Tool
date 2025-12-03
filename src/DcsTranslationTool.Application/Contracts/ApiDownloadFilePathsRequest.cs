namespace DcsTranslationTool.Application.Contracts;

/// <summary>複数ファイルダウンロードAPIの入力を表現する</summary>
/// <param name="Paths">ダウンロード対象のパス一覧を表現する</param>
/// <param name="ETag">条件付きリクエストで利用するETagを保持する</param>
public sealed record ApiDownloadFilePathsRequest( IReadOnlyList<string> Paths, string? ETag );