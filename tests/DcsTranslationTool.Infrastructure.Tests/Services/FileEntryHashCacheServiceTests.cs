using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// <see cref="FileEntryHashCacheService"/> の永続化挙動を検証する。
/// </summary>
public sealed class FileEntryHashCacheServiceTests : IDisposable {
    private readonly Mock<ILoggingService> _logger = new();
    private readonly string _tempDir;
    private readonly string _preferredBaseDir;
    private readonly string _fallbackBaseDir;

    public FileEntryHashCacheServiceTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"FileEntryHashCacheServiceTests_{Guid.NewGuid():N}" );
        _preferredBaseDir = Path.Combine( _tempDir, "preferred" );
        _fallbackBaseDir = Path.Combine( _tempDir, "fallback" );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
        GC.SuppressFinalize( this );
    }

    [Fact]
    public void Upsertした値は再初期化後も取得できる() {
        var lastWriteUtc = DateTime.UtcNow.AddMinutes( -1 );
        const string relativePath = "DCSWorld/Mods/aircraft/A10C/L10N/Test.lua";
        const string sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        using(var service = CreateService()) {
            service.ConfigureRoot( _tempDir );
            service.Upsert( relativePath, 123, lastWriteUtc, sha );
        }

        using var verifyService = CreateService();
        verifyService.ConfigureRoot( _tempDir );
        var hit = verifyService.TryGetSha( relativePath, 123, lastWriteUtc, out var actualSha );

        Assert.True( hit );
        Assert.Equal( sha, actualSha );
        var expectedDbPath = Path.Combine( _preferredBaseDir, ".dcs-translation-cache", "hashcache.db" );
        Assert.True( File.Exists( expectedDbPath ) );
    }

    [Fact]
    public void Pruneは存在しないキーを削除する() {
        var lastWriteUtc = DateTime.UtcNow.AddMinutes( -1 );
        const string keepPath = "DCSWorld/Mods/aircraft/A10C/L10N/Keep.lua";
        const string removePath = "DCSWorld/Mods/aircraft/A10C/L10N/Remove.lua";

        using var service = CreateService();
        service.ConfigureRoot( _tempDir );
        service.Upsert( keepPath, 1, lastWriteUtc, "1111111111111111111111111111111111111111" );
        service.Upsert( removePath, 2, lastWriteUtc, "2222222222222222222222222222222222222222" );

        service.Prune( new HashSet<string>( [keepPath], StringComparer.OrdinalIgnoreCase ) );

        var keepHit = service.TryGetSha( keepPath, 1, lastWriteUtc, out _ );
        var removeHit = service.TryGetSha( removePath, 2, lastWriteUtc, out _ );

        Assert.True( keepHit );
        Assert.False( removeHit );
    }

    [Fact]
    public void 優先保存先に作成できない場合はフォールバック先へ保存する() {
        var blockedPath = Path.Combine( _tempDir, "blocked" );
        File.WriteAllText( blockedPath, "blocked" );
        var root = Path.Combine( _tempDir, "root" );
        Directory.CreateDirectory( root );

        var lastWriteUtc = DateTime.UtcNow.AddMinutes( -1 );
        const string relativePath = "DCSWorld/Mods/aircraft/A10C/L10N/Fallback.lua";

        using var service = new FileEntryHashCacheService( _logger.Object, blockedPath, _fallbackBaseDir );
        service.ConfigureRoot( root );
        service.Upsert( relativePath, 1, lastWriteUtc, "3333333333333333333333333333333333333333" );

        var fallbackDbPath = Path.Combine( _fallbackBaseDir, "cache", "hashcache.db" );
        Assert.True( File.Exists( fallbackDbPath ) );
    }

    [Fact]
    public void ルートを切り替えても他ルートのSHAを誤って再利用しない() {
        var rootA = Path.Combine( _tempDir, "rootA" );
        var rootB = Path.Combine( _tempDir, "rootB" );
        Directory.CreateDirectory( rootA );
        Directory.CreateDirectory( rootB );

        var lastWriteUtc = new DateTime( 2026, 1, 1, 0, 0, 0, DateTimeKind.Utc );
        const string relativePath = "DCSWorld/Mods/aircraft/A10C/L10N/Common.lua";
        const string shaA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        using var service = CreateService();
        service.ConfigureRoot( rootA );
        service.Upsert( relativePath, 100, lastWriteUtc, shaA );

        service.ConfigureRoot( rootB );
        var hitOnRootB = service.TryGetSha( relativePath, 100, lastWriteUtc, out var shaOnRootB );

        service.ConfigureRoot( rootA );
        var hitOnRootA = service.TryGetSha( relativePath, 100, lastWriteUtc, out var shaOnRootA );

        Assert.False( hitOnRootB );
        Assert.Null( shaOnRootB );
        Assert.True( hitOnRootA );
        Assert.Equal( shaA, shaOnRootA );
    }

    /// <summary>
    /// テスト対象サービスを生成する。
    /// </summary>
    /// <returns>生成したキャッシュサービス。</returns>
    private FileEntryHashCacheService CreateService() =>
        new( _logger.Object, _preferredBaseDir, _fallbackBaseDir );
}