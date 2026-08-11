namespace DcsTranslationTool.Application.Models;

/// <summary>
/// アプリケーション更新方式の選択理由を表す。
/// </summary>
public enum ApplicationUpdateSelectionReason {
    /// <summary>
    /// 差分チェーンがすべての選択条件を満たす。
    /// </summary>
    DeltaChainSelected,

    /// <summary>
    /// majorバージョンが異なるため完全版を必要とする。
    /// </summary>
    MajorVersionChanged,

    /// <summary>
    /// 利用できる差分チェーンがないため完全版を必要とする。
    /// </summary>
    DeltaChainUnavailable,

    /// <summary>
    /// 差分チェーンが本数上限を超えるため完全版を必要とする。
    /// </summary>
    DeltaChainTooLong,

    /// <summary>
    /// 差分チェーンが連続性などの要件を満たさないため完全版を必要とする。
    /// </summary>
    DeltaChainInvalid,

    /// <summary>
    /// 差分の総転送量が上限を超えるため完全版を必要とする。
    /// </summary>
    DeltaTransferExceedsLimit
}

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
/// <param name="DeltaAssets">利用可能な差分アセットチェーンを保持する。</param>
public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    Version? CurrentVersion,
    Version? LatestVersion,
    string? LatestVersionLabel,
    string? ReleaseUrl,
    string? RuntimeIdentifier,
    bool IsImmutable,
    ApplicationUpdateAsset? FullAsset,
    IReadOnlyList<ApplicationUpdateAsset> DeltaAssets
) {
    /// <summary>
    /// 利用する差分チェーンの最大要素数を表す。
    /// </summary>
    public const int MaximumDeltaChainLength = 5;

    /// <summary>
    /// 差分チェーンを利用できる完全版比率の上限を百分率で表す。
    /// </summary>
    public const int MaximumDeltaDownloadPercentage = 70;

    /// <summary>
    /// 完全版の総転送量を取得する。
    /// </summary>
    public long FullDownloadSize => GetDownloadSize( FullAsset );

    /// <summary>
    /// 差分チェーンの総転送量を取得する。
    /// </summary>
    public long DeltaDownloadSize => GetDownloadSize( DeltaAssets );

    /// <summary>
    /// 差分チェーンを優先するかどうかを取得する。
    /// </summary>
    public bool IsDeltaPreferred => SelectionReason == ApplicationUpdateSelectionReason.DeltaChainSelected;

    /// <summary>
    /// 更新方式の選択理由を取得する。
    /// </summary>
    public ApplicationUpdateSelectionReason SelectionReason => GetSelectionReason();

    /// <summary>
    /// 優先する更新アセット種別を取得する。
    /// </summary>
    public ApplicationUpdateAssetKind? PreferredAssetKind => IsDeltaPreferred
        ? ApplicationUpdateAssetKind.Delta
        : FullAsset?.Kind;

    /// <summary>
    /// 優先する更新アセット一覧を取得する。
    /// </summary>
    public IReadOnlyList<ApplicationUpdateAsset> PreferredAssets => IsDeltaPreferred
        ? DeltaAssets
        : FullAsset is null
            ? []
            : [FullAsset];

    /// <summary>
    /// 優先する更新方式の総転送量を取得する。
    /// </summary>
    public long PreferredDownloadSize => IsDeltaPreferred ? DeltaDownloadSize : FullDownloadSize;

    /// <summary>
    /// 従来の更新通知結果を生成する。
    /// </summary>
    /// <param name="isUpdateAvailable">更新が利用可能かを受け取る。</param>
    /// <param name="latestVersionLabel">表示用バージョンを受け取る。</param>
    /// <param name="releaseUrl">リリース URL を受け取る。</param>
    public UpdateCheckResult( bool isUpdateAvailable, string? latestVersionLabel, string? releaseUrl )
        : this( isUpdateAvailable, null, null, latestVersionLabel, releaseUrl, null, false, null, [] ) { }

    /// <summary>
    /// 更新なし結果を返す。
    /// </summary>
    public static UpdateCheckResult NoUpdate { get; } = new( false, null, null, null, null, null, false, null, [] );

    /// <summary>
    /// アセット本体とチェックサムsidecarの総転送量を取得する。
    /// </summary>
    private static long GetDownloadSize( ApplicationUpdateAsset? asset ) => asset is null
        ? 0
        : AddSaturated( asset.Size, asset.Checksum.Size );

    /// <summary>
    /// 複数アセットの本体とチェックサムsidecarの総転送量を取得する。
    /// </summary>
    private static long GetDownloadSize( IReadOnlyList<ApplicationUpdateAsset> assets ) {
        long total = 0;
        foreach(var asset in assets) {
            total = AddSaturated( total, GetDownloadSize( asset ) );
        }

        return total;
    }

    /// <summary>
    /// オーバーフロー時に最大値へ飽和させて転送量を加算する。
    /// </summary>
    private static long AddSaturated( long left, long right ) =>
        left < 0 || right < 0 || left > long.MaxValue - right
            ? long.MaxValue
            : left + right;

    /// <summary>
    /// 差分チェーンと完全版の検証結果から更新方式の選択理由を取得する。
    /// </summary>
    private ApplicationUpdateSelectionReason GetSelectionReason() {
        if(CurrentVersion is not null && LatestVersion is not null && CurrentVersion.Major != LatestVersion.Major) {
            return ApplicationUpdateSelectionReason.MajorVersionChanged;
        }

        if(DeltaAssets.Count == 0) {
            return ApplicationUpdateSelectionReason.DeltaChainUnavailable;
        }

        if(DeltaAssets.Count > MaximumDeltaChainLength) {
            return ApplicationUpdateSelectionReason.DeltaChainTooLong;
        }

        if(FullAsset is null || CurrentVersion is null || LatestVersion is null ||
            FullAsset.Kind != ApplicationUpdateAssetKind.Full ||
            !VersionsEqual( FullAsset.TargetVersion, LatestVersion ) ||
            FullAsset.Size <= 0 || FullAsset.Checksum.Size <= 0 ||
            !IsContinuousDeltaChain()) {
            return ApplicationUpdateSelectionReason.DeltaChainInvalid;
        }

        return (decimal)DeltaDownloadSize * 100 <= (decimal)FullDownloadSize * MaximumDeltaDownloadPercentage
            ? ApplicationUpdateSelectionReason.DeltaChainSelected
            : ApplicationUpdateSelectionReason.DeltaTransferExceedsLimit;
    }

    /// <summary>
    /// 差分アセットが現在版から最新版まで連続しているかを判定する。
    /// </summary>
    private bool IsContinuousDeltaChain() {
        if(CurrentVersion is null || LatestVersion is null) {
            return false;
        }

        var expectedBaseVersion = CurrentVersion;
        foreach(var asset in DeltaAssets) {
            if(asset.Kind != ApplicationUpdateAssetKind.Delta || asset.BaseVersion is null ||
                asset.Size <= 0 || asset.Checksum.Size <= 0 ||
                !VersionsEqual( asset.BaseVersion, expectedBaseVersion ) ||
                asset.TargetVersion.Major != CurrentVersion.Major ||
                CompareVersions( asset.TargetVersion, expectedBaseVersion ) <= 0) {
                return false;
            }

            expectedBaseVersion = asset.TargetVersion;
        }

        return VersionsEqual( expectedBaseVersion, LatestVersion );
    }

    /// <summary>
    /// 更新判定で使用する3要素のバージョンが等しいかを判定する。
    /// </summary>
    private static bool VersionsEqual( Version left, Version right ) => CompareVersions( left, right ) == 0;

    /// <summary>
    /// major、minor、buildの順にバージョンを比較する。
    /// </summary>
    private static int CompareVersions( Version left, Version right ) {
        var majorComparison = left.Major.CompareTo( right.Major );
        if(majorComparison != 0) {
            return majorComparison;
        }

        var minorComparison = left.Minor.CompareTo( right.Minor );
        return minorComparison != 0
            ? minorComparison
            : NormalizeBuild( left ).CompareTo( NormalizeBuild( right ) );
    }

    /// <summary>
    /// 未指定のbuild番号を0として取得する。
    /// </summary>
    private static int NormalizeBuild( Version version ) => version.Build < 0 ? 0 : version.Build;
}