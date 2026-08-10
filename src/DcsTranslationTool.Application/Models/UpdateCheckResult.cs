namespace DcsTranslationTool.Application.Models;

/// <summary>
/// アプリケーション更新確認結果を表す。
/// </summary>
/// <param name="IsUpdateAvailable">更新が利用可能かを示す。</param>
/// <param name="CurrentVersion">現在のバージョンを保持する。</param>
/// <param name="LatestVersion">更新先のバージョンを保持する。</param>
/// <param name="LatestVersionLabel">通知表示用の最新バージョン文字列を保持する。</param>
/// <param name="ReleaseUrl">更新先のリリース URL を保持する。</param>
/// <param name="RuntimeIdentifier">更新対象の RID を保持する。</param>
/// <param name="IsImmutable">Immutable Release かどうかを示す。</param>
/// <param name="FullAsset">完全版アセットを保持する。</param>
/// <param name="DeltaAsset">利用可能な差分アセットを保持する。</param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    Version? CurrentVersion,
    Version? LatestVersion,
    string? LatestVersionLabel,
    string? ReleaseUrl,
    string? RuntimeIdentifier,
    bool IsImmutable,
    ApplicationUpdateAsset? FullAsset,
    ApplicationUpdateAsset? DeltaAsset
) {
    /// <summary>
    /// 通信量が少ない優先アセットを取得する。
    /// </summary>
    public ApplicationUpdateAsset? PreferredAsset =>
        DeltaAsset is not null && FullAsset is not null && DeltaAsset.Size < FullAsset.Size
            ? DeltaAsset
            : FullAsset;

    /// <summary>
    /// 従来の更新通知結果を生成する。
    /// </summary>
    /// <param name="isUpdateAvailable">更新が利用可能かを受け取る。</param>
    /// <param name="latestVersionLabel">表示用バージョンを受け取る。</param>
    /// <param name="releaseUrl">リリース URL を受け取る。</param>
    public UpdateCheckResult( bool isUpdateAvailable, string? latestVersionLabel, string? releaseUrl )
        : this( isUpdateAvailable, null, null, latestVersionLabel, releaseUrl, null, false, null, null ) { }

    /// <summary>
    /// 更新なし結果を返す。
    /// </summary>
    public static UpdateCheckResult NoUpdate { get; } = new( false, null, null, null, null, null, false, null, null );
}