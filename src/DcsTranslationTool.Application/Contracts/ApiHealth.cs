namespace DcsTranslationTool.Application.Contracts;

/// <summary>APIのヘルス情報を表現する</summary>
/// <param name="Status">APIが返却したステータスを示す</param>
/// <param name="Timestamp">APIが返却したタイムスタンプを示す</param>
public sealed record ApiHealth( ApiHealthStatus Status, DateTimeOffset? Timestamp );

/// <summary>APIのヘルスステータスを表現する</summary>
public enum ApiHealthStatus {
    /// <summary>状態を判別できないことを示す</summary>
    Unknown = 0,
    /// <summary>APIが正常な状態であることを示す</summary>
    Ok = 1,
}