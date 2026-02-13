namespace DcsTranslationTool.Application.Models;

/// <summary>
/// アプリケーション更新確認結果を表す。
/// </summary>
/// <param name="IsUpdateAvailable">更新が利用可能かを示す。</param>
/// <param name="LatestVersionLabel">通知表示用の最新バージョン文字列を保持する。</param>
/// <param name="ReleaseUrl">更新先のリリース URL を保持する。</param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string? LatestVersionLabel,
    string? ReleaseUrl
) {
    /// <summary>
    /// 更新なし結果を返す。
    /// </summary>
    public static UpdateCheckResult NoUpdate { get; } = new( false, null, null );
}