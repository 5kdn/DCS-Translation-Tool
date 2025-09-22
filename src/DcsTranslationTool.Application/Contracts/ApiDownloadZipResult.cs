namespace DcsTranslationTool.Application.Contracts;

/// <summary>ダウンロード用のZIP取得結果を表現する</summary>
/// <param name="Path">ダウンロードしたパスを示す</param>
/// <param name="Content">ZIPのバイト列を保持する</param>
/// <param name="Size">ZIPのバイト数を示す</param>
/// <param name="Message">APIが返却したメッセージを保持する</param>
public sealed record ApiDownloadZipResult( string Path, byte[] Content, long Size, string? Message );