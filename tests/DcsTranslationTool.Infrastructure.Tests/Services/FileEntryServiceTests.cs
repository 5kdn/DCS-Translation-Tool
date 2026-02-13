using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

public class FileEntryServiceTests : IDisposable {
    private readonly Mock<ILoggingService> logger = new();
    private readonly Mock<IFileEntryHashCacheService> _hashCacheService = new();
    private readonly string _tempDir;

    public FileEntryServiceTests() {
        _tempDir = Path.Join( Path.GetTempPath(), Guid.NewGuid().ToString() );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) Directory.Delete( _tempDir, true );
        GC.SuppressFinalize( this );
    }

    #region GetChildrenRecursive
    [Fact]
    public async Task GetChildrenRecursiveがファイルを含むディレクトリを指定した場合正しい結果が返る() {
        // Arrange
        var targetDir = Path.Combine(_tempDir, "DirWithFile");
        var targetSubdir = Path.Combine(targetDir, "subdir");
        Directory.CreateDirectory( targetSubdir );

        var filePath = Path.Combine(targetSubdir, "test.txt");
        File.WriteAllText( filePath, "Hello World" );
        var service = CreateService();

        // Act
        var result = await service.GetChildrenRecursiveAsync(targetDir);

        // Assert
        Assert.True( result.IsSuccess );
        var actualFileEntries = result.Value;
        Assert.Single( actualFileEntries );

        var actualEntry = actualFileEntries.First();
        Assert.NotNull( actualEntry );
        Assert.Equal( "test.txt", actualEntry.Name );
        Assert.Equal( "subdir/test.txt", actualEntry.Path );
        Assert.False( actualEntry.IsDirectory );
        Assert.Equal( "5e1c309dae7f45e0f39b1bf3ac3cd9db12e7d689", actualEntry.LocalSha );
        Assert.Null( actualEntry.RepoSha );
    }

    [Fact]
    public async Task GetChildrenRecursiveは空のディレクトリを指定した場合空の結果が返る() {
        // Arrange
        var targetDir = Path.Combine(_tempDir, "EmptyDir_" + Guid.NewGuid());
        Directory.CreateDirectory( targetDir );
        var service = CreateService();

        // Act
        var result = await service.GetChildrenRecursiveAsync(targetDir);

        // Assert
        Assert.True( result.IsSuccess );
        var actualFileEntries = result.Value;
        Assert.Empty( actualFileEntries );
    }

    [Fact]
    public async Task GetChildrenRecursiveは存在しないパスを指定した場合失敗結果を返す() {
        // Arrange
        var invalidPath = Path.Combine(_tempDir, "NotExist_" + Guid.NewGuid());
        var service = CreateService();

        // Act
        var result = await service.GetChildrenRecursiveAsync(invalidPath);

        // Assert
        Assert.True( result.IsFailed );
        var actualMessage = result.Errors[0].Message;
        Assert.StartsWith( "指定されたパスが存在しません: ", actualMessage );
        Assert.Equal( nameof( ResultErrorKind.NotFound ), result.Errors[0].Metadata["kind"] );
    }

    [Fact]
    public async Task GetChildrenRecursiveはWatch未実行でもハッシュキャッシュ初期化後に成功する() {
        // Arrange
        var targetDir = Path.Combine( _tempDir, "DirectRecursiveWithRealCache" );
        Directory.CreateDirectory( targetDir );
        var filePath = Path.Combine( targetDir, "test.txt" );
        await File.WriteAllTextAsync( filePath, "Hello", TestContext.Current.CancellationToken );

        var preferredBaseDir = Path.Combine( _tempDir, "cache-preferred" );
        var fallbackBaseDir = Path.Combine( _tempDir, "cache-fallback" );
        var hashCacheService = new FileEntryHashCacheService( logger.Object, preferredBaseDir, fallbackBaseDir );
        using var service = new FileEntryService( logger.Object, hashCacheService );

        // Act
        var result = await service.GetChildrenRecursiveAsync( targetDir );

        // Assert
        Assert.True( result.IsSuccess );
        var entries = result.Value.ToArray();
        Assert.Single( entries );
        Assert.Equal( "test.txt", entries[0].Path );
        Assert.NotNull( entries[0].LocalSha );
    }

    [Fact]
    public async Task GetChildrenRecursiveはハッシュキャッシュ初期化失敗時に失敗結果を返す() {
        // Arrange
        var targetDir = Path.Combine( _tempDir, "CacheInitFailure" );
        Directory.CreateDirectory( targetDir );
        var filePath = Path.Combine( targetDir, "test.txt" );
        await File.WriteAllTextAsync( filePath, "Hello", TestContext.Current.CancellationToken );
        _hashCacheService
            .Setup( service => service.ConfigureRoot( targetDir ) )
            .Throws( new InvalidOperationException( "cache init failed" ) );
        using var service = CreateService();

        // Act
        var result = await service.GetChildrenRecursiveAsync( targetDir );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Equal( nameof( ResultErrorKind.Unexpected ), result.Errors[0].Metadata["kind"] );
        Assert.Equal( "FILE_ENTRY_CACHE_INIT_EXCEPTION", result.Errors[0].Metadata["code"] );
    }

    #endregion

    #region Watch

    [Fact]
    public async Task Watchはファイルを追加したときEntriesChangedが発火する() {
        // Arrange
        var targetDir = Path.Combine(_tempDir, "WatchDir");
        Directory.CreateDirectory( targetDir );
        using var service = CreateService();
        var tcs = new TaskCompletionSource<IReadOnlyList<FileEntry>>(TaskCreationOptions.RunContinuationsAsynchronously);
        service.EntriesChanged += entries => {
            if(entries is { Count: > 0 } && entries.All( e => e.LocalSha is not null ))
                tcs.TrySetResult( entries );
            return Task.CompletedTask;
        };

        // Act
        service.Watch( targetDir );
        await Task.Delay( 100, TestContext.Current.CancellationToken );    // GitHub Actionsでの実行結果を安定化させる
        var filePath = Path.Combine(targetDir, "new.txt");
        await File.WriteAllTextAsync( filePath, "data", TestContext.Current.CancellationToken );
        var entries = await tcs.Task.WaitAsync( TimeSpan.FromSeconds( 5 ), TestContext.Current.CancellationToken );

        // Assert
        Assert.NotNull( entries );
        Assert.Single( entries );

        var entry = entries[0];
        Assert.Equal( "new.txt", entry.Name );
        Assert.Equal( "6320cd248dd8aeaab759d5871f8781b5c0505172", entry.LocalSha );
    }

    [Fact]
    public async Task Watchはハッシュキャッシュ初期化失敗時でも監視を継続する() {
        // Arrange
        var targetDir = Path.Combine( _tempDir, "WatchWithCacheInitFailure" );
        Directory.CreateDirectory( targetDir );
        _hashCacheService
            .Setup( service => service.ConfigureRoot( targetDir ) )
            .Throws( new InvalidOperationException( "cache init failed" ) );
        using var service = CreateService();

        var tcs = new TaskCompletionSource<IReadOnlyList<FileEntry>>( TaskCreationOptions.RunContinuationsAsynchronously );
        service.EntriesChanged += entries => {
            if(entries is { Count: > 0 } && entries.All( entry => entry.LocalSha is not null )) {
                tcs.TrySetResult( entries );
            }

            return Task.CompletedTask;
        };

        // Act
        service.Watch( targetDir );
        await Task.Delay( 100, TestContext.Current.CancellationToken );
        var filePath = Path.Combine( targetDir, "new.txt" );
        await File.WriteAllTextAsync( filePath, "data", TestContext.Current.CancellationToken );
        var entries = await tcs.Task.WaitAsync( TimeSpan.FromSeconds( 5 ), TestContext.Current.CancellationToken );

        // Assert
        Assert.Single( entries );
        Assert.Equal( "new.txt", entries[0].Name );
        Assert.Equal( "6320cd248dd8aeaab759d5871f8781b5c0505172", entries[0].LocalSha );
        _hashCacheService.Verify( service => service.TryGetSha( It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DateTime>(), out It.Ref<string?>.IsAny ), Times.Never );
        _hashCacheService.Verify( service => service.Upsert( It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<string?>() ), Times.Never );
        _hashCacheService.Verify( service => service.Prune( It.IsAny<IReadOnlySet<string>>() ), Times.Never );
    }

    #endregion

    #region GetEntriesAsync

    [Fact]
    public async Task GetEntriesAsyncはWatch未実行で空のコレクションを返す() {
        // Arrange
        using var service = CreateService();

        // Act
        var result = await service.GetEntriesAsync();

        // Assert
        Assert.True( result.IsSuccess );
        Assert.Empty( result.Value );
    }

    [Fact]
    public async Task Watchは存在しないディレクトリを指定したときGetEntriesAsyncが失敗結果を返す() {
        // Arrange
        var targetDir = Path.Combine(_tempDir, "NotExistDir");
        using var service = CreateService();

        // Act
        service.Watch( targetDir );
        var result = await service.GetEntriesAsync();

        // Assert
        Assert.True( result.IsFailed );
    }

    #endregion

    /// <summary>
    /// テスト対象サービスを生成する。
    /// </summary>
    /// <returns>初期化済みの <see cref="FileEntryService"/> を返す。</returns>
    private FileEntryService CreateService() =>
        new( logger.Object, _hashCacheService.Object );
}