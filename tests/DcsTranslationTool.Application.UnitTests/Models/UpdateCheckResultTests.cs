using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Application.UnitTests.Models;

/// <summary>
/// <see cref="UpdateCheckResult"/> の更新アセット選択規則を検証する。
/// </summary>
public sealed class UpdateCheckResultTests {
    /// <summary>
    /// 差分5本の総転送量が完全版の70%の場合に差分を選択することを確認する。
    /// </summary>
    [Fact]
    public void PreferredAssetsは差分5本かつ総転送量70パーセント以下の場合に差分を返す() {
        var fullAsset = CreateAsset( ApplicationUpdateAssetKind.Full, null, new Version( 2, 5, 0 ), 950, 50 );
        var deltaAssets = Enumerable.Range( 0, 5 )
            .Select( index => CreateAsset(
                ApplicationUpdateAssetKind.Delta,
                new Version( 2, index, 0 ),
                new Version( 2, index + 1, 0 ),
                130,
                10 ) )
            .ToArray();
        var sut = CreateResult( new Version( 2, 0, 0 ), new Version( 2, 5, 0 ), fullAsset, deltaAssets );

        Assert.True( sut.IsDeltaPreferred );
        Assert.Equal( 700, sut.DeltaDownloadSize );
        Assert.Equal( 1000, sut.FullDownloadSize );
        Assert.Equal( 700, sut.PreferredDownloadSize );
        Assert.Equal( deltaAssets, sut.PreferredAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Delta, sut.PreferredAssetKind );
        Assert.Equal( ApplicationUpdateSelectionReason.DeltaChainSelected, sut.SelectionReason );
    }

    /// <summary>
    /// 差分が6本の場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public void PreferredAssetsは差分が6本の場合に完全版を返す() {
        var fullAsset = CreateAsset( ApplicationUpdateAssetKind.Full, null, new Version( 2, 6, 0 ), 950, 50 );
        var deltaAssets = Enumerable.Range( 0, 6 )
            .Select( index => CreateAsset(
                ApplicationUpdateAssetKind.Delta,
                new Version( 2, index, 0 ),
                new Version( 2, index + 1, 0 ),
                50,
                10 ) )
            .ToArray();
        var sut = CreateResult( new Version( 2, 0, 0 ), new Version( 2, 6, 0 ), fullAsset, deltaAssets );

        Assert.False( sut.IsDeltaPreferred );
        Assert.Equal( [fullAsset], sut.PreferredAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, sut.PreferredAssetKind );
        Assert.Equal( ApplicationUpdateSelectionReason.DeltaChainTooLong, sut.SelectionReason );
    }

    /// <summary>
    /// sidecar込みの差分総転送量が70%を超える場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public void PreferredAssetsはsidecar込みの差分総転送量が70パーセントを超える場合に完全版を返す() {
        var fullAsset = CreateAsset( ApplicationUpdateAssetKind.Full, null, new Version( 2, 1, 0 ), 950, 50 );
        var deltaAsset = CreateAsset(
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 0, 0 ),
            new Version( 2, 1, 0 ),
            690,
            11 );
        var sut = CreateResult( new Version( 2, 0, 0 ), new Version( 2, 1, 0 ), fullAsset, [deltaAsset] );

        Assert.False( sut.IsDeltaPreferred );
        Assert.Equal( 701, sut.DeltaDownloadSize );
        Assert.Equal( [fullAsset], sut.PreferredAssets );
        Assert.Equal( ApplicationUpdateSelectionReason.DeltaTransferExceedsLimit, sut.SelectionReason );
    }

    /// <summary>
    /// majorが異なる場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public void PreferredAssetsはmajorが異なる場合に完全版を返す() {
        var fullAsset = CreateAsset( ApplicationUpdateAssetKind.Full, null, new Version( 3, 0, 0 ), 950, 50 );
        var deltaAsset = CreateAsset(
            ApplicationUpdateAssetKind.Delta,
            new Version( 2, 9, 0 ),
            new Version( 3, 0, 0 ),
            100,
            10 );
        var sut = CreateResult( new Version( 2, 9, 0 ), new Version( 3, 0, 0 ), fullAsset, [deltaAsset] );

        Assert.False( sut.IsDeltaPreferred );
        Assert.Equal( [fullAsset], sut.PreferredAssets );
        Assert.Equal( ApplicationUpdateSelectionReason.MajorVersionChanged, sut.SelectionReason );
    }

    /// <summary>
    /// 差分チェーンが不連続な場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public void PreferredAssetsは差分チェーンが不連続な場合に完全版を返す() {
        var fullAsset = CreateAsset( ApplicationUpdateAssetKind.Full, null, new Version( 2, 2, 0 ), 950, 50 );
        var deltaAssets = new[]
        {
            CreateAsset( ApplicationUpdateAssetKind.Delta, new Version( 2, 0, 0 ), new Version( 2, 1, 0 ), 100, 10 ),
            CreateAsset( ApplicationUpdateAssetKind.Delta, new Version( 2, 1, 1 ), new Version( 2, 2, 0 ), 100, 10 )
        };
        var sut = CreateResult( new Version( 2, 0, 0 ), new Version( 2, 2, 0 ), fullAsset, deltaAssets );

        Assert.False( sut.IsDeltaPreferred );
        Assert.Equal( [fullAsset], sut.PreferredAssets );
        Assert.Equal( ApplicationUpdateSelectionReason.DeltaChainInvalid, sut.SelectionReason );
    }

    private static UpdateCheckResult CreateResult(
        Version currentVersion,
        Version latestVersion,
        ApplicationUpdateAsset fullAsset,
        IReadOnlyList<ApplicationUpdateAsset> deltaAssets
    ) => new(
        true,
        currentVersion,
        latestVersion,
        $"v{latestVersion.ToString( 3 )}",
        $"https://github.com/dcs-translation-japanese/DCS-Translation-Tool/releases/tag/v{latestVersion.ToString( 3 )}",
        "win-x64",
        true,
        fullAsset,
        deltaAssets );

    private static ApplicationUpdateAsset CreateAsset(
        ApplicationUpdateAssetKind kind,
        Version? baseVersion,
        Version targetVersion,
        long size,
        long checksumSize
    ) {
        var name = kind == ApplicationUpdateAssetKind.Full
            ? $"DCS-Translation-Tool-{targetVersion.ToString( 3 )}-win-x64.zip"
            : $"DCS-Translation-Tool-{baseVersion!.ToString( 3 )}-to-{targetVersion.ToString( 3 )}-win-x64.delta.zip";
        return new ApplicationUpdateAsset(
            name,
            $"https://github.com/dcs-translation-japanese/DCS-Translation-Tool/releases/download/v{targetVersion.ToString( 3 )}/{name}",
            size,
            $"sha256:{new string( 'a', 64 )}",
            kind,
            baseVersion,
            targetVersion,
            new ApplicationUpdateChecksumAsset(
                $"{name}.sha256",
                $"https://github.com/dcs-translation-japanese/DCS-Translation-Tool/releases/download/v{targetVersion.ToString( 3 )}/{name}.sha256",
                checksumSize,
                $"sha256:{new string( 'b', 64 )}" ) );
    }
}