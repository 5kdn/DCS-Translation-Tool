namespace DcsTranslationTool.Application.Models;

/// <summary>
/// アプリケーション更新アセットの種別を表す。
/// </summary>
public enum ApplicationUpdateAssetKind {
    /// <summary>
    /// 完全版アセットを表す。
    /// </summary>
    Full,

    /// <summary>
    /// 差分アセットを表す。
    /// </summary>
    Delta
}

/// <summary>
/// GitHub Release に公開されたアプリケーション更新アセットを表す。
/// </summary>
/// <param name="Name">アセット名を受け取る。</param>
/// <param name="DownloadUrl">ダウンロード URL を受け取る。</param>
/// <param name="Size">アセットサイズを受け取る。</param>
/// <param name="Sha256Digest">GitHub が返す SHA-256 digest を受け取る。</param>
/// <param name="Kind">アセット種別を受け取る。</param>
/// <param name="BaseVersion">差分の基準バージョンを受け取る。</param>
/// <param name="TargetVersion">更新先バージョンを受け取る。</param>
/// <param name="Checksum">SHA-256 sidecar アセットを受け取る。</param>
public sealed record ApplicationUpdateAsset(
    string Name,
    string DownloadUrl,
    long Size,
    string Sha256Digest,
    ApplicationUpdateAssetKind Kind,
    Version? BaseVersion,
    Version TargetVersion,
    ApplicationUpdateChecksumAsset Checksum
);

/// <summary>
/// 更新アセットの SHA-256 sidecar を表す。
/// </summary>
/// <param name="Name">sidecar 名を受け取る。</param>
/// <param name="DownloadUrl">ダウンロード URL を受け取る。</param>
/// <param name="Size">sidecar サイズを受け取る。</param>
/// <param name="Sha256Digest">GitHub が返す sidecar 自身の SHA-256 digest を受け取る。</param>
public sealed record ApplicationUpdateChecksumAsset(
    string Name,
    string DownloadUrl,
    long Size,
    string Sha256Digest
);