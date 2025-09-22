namespace DcsTranslationTool.Application.Contracts;

/// <summary>複数ファイルダウンロードAPIの結果を表現する</summary>
/// <param name="Paths">ダウンロード対象のパス一覧を示す</param>
/// <param name="Content">取得したZIPのバイト列を保持する</param>
/// <param name="Size">ZIPのバイト数を示す</param>
/// <param name="ContentType">レスポンスのContent-Typeを保持する</param>
/// <param name="FileName">レスポンスのファイル名を保持する</param>
/// <param name="ETag">レスポンスヘッダーのETagを保持する</param>
/// <param name="IsNotModified">304 Not Modified を検出したかを示す</param>
public sealed record ApiDownloadFilesResult(
    IReadOnlyList<string> Paths,
    byte[] Content,
    long Size,
    string? ContentType,
    string? FileName,
    string? ETag,
    bool IsNotModified
);