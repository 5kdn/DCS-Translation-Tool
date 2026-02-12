using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

public class FileEntryServiceTests : IDisposable {
    private readonly Mock<ILoggingService> logger = new();
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
        var service = new FileEntryService(logger.Object);

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
        var service = new FileEntryService(logger.Object);

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
        var service = new FileEntryService(logger.Object);

        // Act
        var result = await service.GetChildrenRecursiveAsync(invalidPath);

        // Assert
        Assert.True( result.IsFailed );
        var actualMessage = result.Errors[0].Message;
        Assert.StartsWith( "指定されたパスが存在しません: ", actualMessage );
        Assert.Equal( ResultErrorKind.NotFound, result.GetFirstErrorKind() );
    }

    #endregion

    #region Watch

    [Fact]
    public async Task Watchはファイルを追加したときEntriesChangedが発火する() {
        // Arrange
        var targetDir = Path.Combine(_tempDir, "WatchDir");
        Directory.CreateDirectory( targetDir );
        using var service = new FileEntryService(logger.Object);
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

    #endregion

    #region GetEntriesAsync

    [Fact]
    public async Task GetEntriesAsyncはWatch未実行で空のコレクションを返す() {
        // Arrange
        using var service = new FileEntryService(logger.Object);

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
        using var service = new FileEntryService(logger.Object);

        // Act
        service.Watch( targetDir );
        var result = await service.GetEntriesAsync();

        // Assert
        Assert.True( result.IsFailed );
    }

    #endregion
}
