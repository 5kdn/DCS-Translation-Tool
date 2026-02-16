namespace DcsTranslationTool.Infrastructure.Interfaces;

/// <summary>
/// Tree API のレース呼び出しで利用する HTTP クライアントを供給する。
/// </summary>
public interface ITreeHttpClientProvider {
    /// <summary>既定経路に使う HTTP クライアントを返す。</summary>
    HttpClient DefaultClient { get; }

    /// <summary>IPv4 優先経路に使う HTTP クライアントを返す。</summary>
    HttpClient Ipv4PreferredClient { get; }

    /// <summary>IPv6 優先経路に使う HTTP クライアントを返す。</summary>
    HttpClient Ipv6PreferredClient { get; }

    /// <summary>IPv4 優先経路が専用クライアントかどうかを返す。</summary>
    bool IsIpv4PreferredDedicated { get; }

    /// <summary>IPv6 優先経路が専用クライアントかどうかを返す。</summary>
    bool IsIpv6PreferredDedicated { get; }
}