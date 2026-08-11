using System.Security.Cryptography;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.TestCommon.IO;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

/// <summary>
/// <see cref="DeltaAssetCacheService"/> の差分アセットキャッシュを検証する。
/// </summary>
public sealed class DeltaAssetCacheServiceTests {
    /// <summary>
    /// ハッシュとサイズが一致するキャッシュを復元し、利用時刻を更新することを確認する。
    /// </summary>
    [Fact]
    public async Task TryRestoreAsyncは検証済みキャッシュを復元する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( TryRestoreAsyncは検証済みキャッシュを復元する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var sourcePath = temporaryDirectory.GetPath( "source.delta.zip" );
        var destinationPath = temporaryDirectory.GetPath( "workspace/asset.delta.zip" );
        var bytes = "verified cache"u8.ToArray();
        File.WriteAllBytes( sourcePath, bytes );
        var asset = CreateAsset( bytes );
        var service = new DeltaAssetCacheService( Mock.Of<ILoggingService>(), updatesDirectory );
        await service.StoreAsync( asset, sourcePath, TestContext.Current.CancellationToken );
        var cachePath = Path.Combine( updatesDirectory, "cache", $"{CalculateSha256( bytes )}.zip" );
        File.SetLastWriteTimeUtc( cachePath, DateTime.UtcNow.AddDays( -1 ) );
        var lastWriteTime = File.GetLastWriteTimeUtc( cachePath );

        var restored = await service.TryRestoreAsync( asset, destinationPath, TestContext.Current.CancellationToken );

        Assert.True( restored );
        Assert.Equal( bytes, File.ReadAllBytes( destinationPath ) );
        Assert.True( File.GetLastWriteTimeUtc( cachePath ) > lastWriteTime );
    }

    /// <summary>
    /// 改変されたキャッシュを削除して復元しないことを確認する。
    /// </summary>
    [Fact]
    public async Task TryRestoreAsyncは改変されたキャッシュを削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( TryRestoreAsyncは改変されたキャッシュを削除する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var expectedBytes = "expected cache"u8.ToArray();
        var asset = CreateAsset( expectedBytes );
        var cachePath = Path.Combine( updatesDirectory, "cache", $"{CalculateSha256( expectedBytes )}.zip" );
        Directory.CreateDirectory( Path.GetDirectoryName( cachePath )! );
        File.WriteAllBytes( cachePath, "tampered cache"u8.ToArray() );
        var service = new DeltaAssetCacheService( Mock.Of<ILoggingService>(), updatesDirectory );

        var restored = await service.TryRestoreAsync(
            asset,
            temporaryDirectory.GetPath( "workspace/asset.delta.zip" ),
            TestContext.Current.CancellationToken );

        Assert.False( restored );
        Assert.False( File.Exists( cachePath ) );
    }

    /// <summary>
    /// キャンセル済みの処理では復元先を作成しないことを確認する。
    /// </summary>
    [Fact]
    public async Task TryRestoreAsyncはキャンセル済みトークンで復元を中止する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( TryRestoreAsyncはキャンセル済みトークンで復元を中止する ) );
        var updatesDirectory = temporaryDirectory.GetPath( "updates" );
        var bytes = "verified cache"u8.ToArray();
        var asset = CreateAsset( bytes );
        var cachePath = Path.Combine( updatesDirectory, "cache", $"{CalculateSha256( bytes )}.zip" );
        Directory.CreateDirectory( Path.GetDirectoryName( cachePath )! );
        File.WriteAllBytes( cachePath, bytes );
        var destinationPath = temporaryDirectory.GetPath( "workspace/asset.delta.zip" );
        var service = new DeltaAssetCacheService( Mock.Of<ILoggingService>(), updatesDirectory );
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.TryRestoreAsync( asset, destinationPath, cancellationTokenSource.Token ) );

        Assert.False( File.Exists( destinationPath ) );
    }

    /// <summary>
    /// 期限切れの差分アセットと未完了一時ファイルを削除することを確認する。
    /// </summary>
    [Fact]
    public void Pruneは期限切れの差分キャッシュを削除する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( Pruneは期限切れの差分キャッシュを削除する ) );
        var cacheDirectory = temporaryDirectory.GetPath( "updates/cache" );
        var expiredPath = Path.Combine( cacheDirectory, $"{new string( 'a', 64 )}.zip" );
        var retainedPath = Path.Combine( cacheDirectory, $"{new string( 'b', 64 )}.zip" );
        var partialPath = Path.Combine( cacheDirectory, "asset.partial" );
        Directory.CreateDirectory( cacheDirectory );
        File.WriteAllText( expiredPath, "expired" );
        File.WriteAllText( retainedPath, "retained" );
        File.WriteAllText( partialPath, "partial" );
        File.SetLastWriteTimeUtc( expiredPath, DateTime.UtcNow.AddDays( -31 ) );
        File.SetLastWriteTimeUtc( partialPath, DateTime.UtcNow.AddDays( -31 ) );
        var service = new DeltaAssetCacheService( Mock.Of<ILoggingService>(), temporaryDirectory.GetPath( "updates" ) );

        service.Prune();

        Assert.False( File.Exists( expiredPath ) );
        Assert.False( File.Exists( partialPath ) );
        Assert.True( File.Exists( retainedPath ) );
    }

    private static ApplicationUpdateAsset CreateAsset( byte[] bytes ) => new(
        "asset.delta.zip",
        "https://example.test/asset.delta.zip",
        bytes.Length,
        $"sha256:{CalculateSha256( bytes )}",
        ApplicationUpdateAssetKind.Delta,
        new Version( 1, 0, 0 ),
        new Version( 1, 1, 0 ),
        new ApplicationUpdateChecksumAsset( "asset.delta.zip.sha256", "https://example.test/asset.delta.zip.sha256", 0, "sha256:" + new string( '0', 64 ) ) );

    private static string CalculateSha256( byte[] bytes ) => Convert.ToHexString( SHA256.HashData( bytes ) ).ToLowerInvariant();
}