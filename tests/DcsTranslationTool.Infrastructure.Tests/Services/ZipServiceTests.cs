using System.IO.Compression;

using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// ZipService の機能を検証するテスト。
/// </summary>
public class ZipServiceTests : IDisposable {
    private readonly Mock<ILoggingService> logger = new();
    private readonly string _tempDir;

    public ZipServiceTests() {
        _tempDir = Path.Join(
            Path.GetTempPath(),
            Guid.NewGuid().ToString()
        );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        Directory.Delete( _tempDir, true );
        GC.SuppressFinalize( this );
    }

    #region GetEntries

    [Fact]
    public void GetEntriesは全エントリー名を取得する() {
        // Arrange
        var zipPath = Path.Join(_tempDir, "test.zip");
        using(var archive = ZipFile.Open( zipPath, ZipArchiveMode.Create )) {
            archive.CreateEntry( "foo.txt" );
            archive.CreateEntry( "bar/baz.txt" );
        }
        var sut = new ZipService(logger.Object);

        // Act
        var result = sut.GetEntries(zipPath);

        // Assert
        Assert.True( result.IsSuccess );
        var entries = result.Value;
        Assert.Equal( 2, entries.Count );
        Assert.Contains( "foo.txt", entries );
        Assert.Contains( "bar/baz.txt", entries );
    }

    [Theory]
    [InlineData( "" )]
    [InlineData( " " )]
    public void GetEntriesはZip空のファイルパスを指定したとき失敗結果を返す( string targetPath ) {
        // Arrange
        var sut = new ZipService(logger.Object);

        // Act
        var result = sut.GetEntries(targetPath);

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "zip ファイルパスが null または空です", result.Errors[0].Message );
        Assert.Equal( ResultErrorKind.Validation, result.GetFirstErrorKind() );
    }

    [Fact]
    public void GetEntriesは存在しないパスを指定したとき失敗結果を返す() {
        // Arrange
        var zipPath = Path.Join(_tempDir, "notfound.zip");
        var sut = new ZipService(logger.Object);

        // Act
        var result = sut.GetEntries( zipPath );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "ファイルが存在しません", result.Errors[0].Message );
    }

    [Fact]
    public void GetEntriesは壊れたzipファイルを指定したとき失敗結果を返す() {
        // Arrange
        var zipPath = Path.Join(_tempDir, "broken.zip");
        File.WriteAllText( zipPath, "not a zip archive!" );
        var sut = new ZipService(logger.Object);

        // Act
        var result = sut.GetEntries( zipPath );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "End of Central Directory record could not be found.", result.Errors[0].Message );
    }

    [Fact]
    public void GetEntriesはアクセス不可なzipファイルを指定したとき失敗結果を返す() {
        // Arrange
        var zipPath = Path.Join(_tempDir, "locked.zip");
        using(var archive = ZipFile.Open( zipPath, ZipArchiveMode.Create )) {
            archive.CreateEntry( "foo.txt" );
        }
        using var fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.None);
        var sut = new ZipService(logger.Object);

        // Act
        var result = sut.GetEntries(zipPath);

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "The process cannot access the file", result.Errors[0].Message );
    }

    #endregion

    #region AddEntry(byte[])

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryはバイト列をzipに追加できる() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "dummy.zip");
        byte[] data = [ 1, 2, 3, 4, 5 ];
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }

        // Act
        var result = service.AddEntry( zipPath, "data.bin", data );

        // Assert
        Assert.True( result.IsSuccess );

        using ZipArchive actual = ZipFile.OpenRead( zipPath );
        var actualEntry = actual.GetEntry("data.bin");
        Assert.NotNull( actualEntry );
        using var stream = actualEntry.Open();
        var resultBytes = new byte[data.Length];
        var bytesRead = stream.Read(resultBytes, 0, resultBytes.Length);
        Assert.Equal( data.Length, bytesRead );
        Assert.Equal( data, resultBytes );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryは空のZipファイルパスを指定したとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        byte[] data = [1, 2, 3];

        // Act
        var result = service.AddEntry( "", "foo.txt", data );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "zip ファイルパスが null または空です", result.Errors[0].Message );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryは存在しないZipファイルを指定したとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        byte[] data = [1, 2, 3];

        // Act
        var result = service.AddEntry( "notfound.zip", "foo.txt", data );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "ファイルが存在しません", result.Errors[0].Message );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryは空のEntryPathのとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "dummy.zip");
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }
        byte[] data = [1, 2, 3];

        // Act
        var result = service.AddEntry( zipPath, "", data );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "エントリーが null または空です", result.Errors[0].Message );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryはDataが空配列のとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "dummy.zip");
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }

        // Act
        var result = service.AddEntry( zipPath, "foo.txt", [] );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "追加するデータが null または空です", result.Errors[0].Message );
    }

    #endregion

    #region AddEntry(file)

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryはzipにファイルパスから正常に追加できる() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "file.zip");
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }
        var filePath = Path.Join(_tempDir, "data.txt");
        File.WriteAllText( filePath, "data" );

        // Act
        var result = service.AddEntry( zipPath, "data.txt", filePath );

        // Assert
        Assert.True( result.IsSuccess );
        using var archive = ZipFile.OpenRead( zipPath );
        Assert.NotNull( archive.GetEntry( "data.txt" ) );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryは存在しないZipファイルにファイルパスを追加しようとしたとき失敗する() {
        // Arrange
        var service = new ZipService(logger.Object);
        var filePath = Path.Join(_tempDir, "data.txt");
        File.WriteAllText( filePath, "data" );

        // Act
        var result = service.AddEntry( Path.Join(_tempDir, "no.zip" ), "data.txt", filePath );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "ファイルが存在しません", result.Errors[0].Message );
    }


    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryは存在しないファイルパスを追加しようとしたとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "file.zip");
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }

        // Act
        var result = service.AddEntry( zipPath, "data.txt", Path.Join(_tempDir, "nofile.txt") );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "ファイルが存在しません", result.Errors[0].Message );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void AddEntryはEntryPathが空のとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "file.zip");
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }
        var filePath = Path.Join(_tempDir, "data.txt");
        File.WriteAllText( filePath, "data" );

        // Act
        var result = service.AddEntry( zipPath, "", filePath );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "値が null または空です", result.Errors[0].Message );
    }

    #endregion

    #region DeleteEntry

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void DeleteEntryは正常にエントリを削除できる() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "dummy.zip");
        using(var archive = ZipFile.Open( zipPath, ZipArchiveMode.Create )) {
            archive.CreateEntry( "a.txt" );
            archive.CreateEntry( "b.txt" );
            archive.CreateEntry( "dir/c.txt" );
        }

        // Act
        var result = service.DeleteEntry( zipPath, "a.txt" );

        // Assert
        Assert.True( result.IsSuccess );
        using var actual = ZipFile.OpenRead( zipPath );
        Assert.Null( actual.GetEntry( "a.txt" ) );
        Assert.NotNull( actual.GetEntry( "b.txt" ) );
        Assert.NotNull( actual.GetEntry( "dir/c.txt" ) );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void DeleteEntryはディレクトリエントリを指定したとき配下全てを削除できる() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "dummy.zip");
        using(var archive = ZipFile.Open( zipPath, ZipArchiveMode.Create )) {
            archive.CreateEntry( "dir/a.txt" );
            archive.CreateEntry( "dir/b.txt" );
            archive.CreateEntry( "other.txt" );
        }

        // Act
        var result = service.DeleteEntry( zipPath, "dir" );

        // Assert
        Assert.True( result.IsSuccess );
        using var actual = ZipFile.OpenRead( zipPath );
        Assert.Null( actual.GetEntry( "dir/a.txt" ) );
        Assert.Null( actual.GetEntry( "dir/b.txt" ) );
        Assert.NotNull( actual.GetEntry( "other.txt" ) );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void DeleteEntryはZipファイルパスが空のとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);

        // Act
        var result = service.DeleteEntry( "", "foo.txt" );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "zip ファイルパスが空です", result.Errors[0].Message );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void DeleteEntryはZipファイルが存在しないとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);

        // Act
        var result = service.DeleteEntry( "notfound.zip", "foo.txt" );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "ファイルが存在しません", result.Errors[0].Message );
    }

    [Fact]
    [Trait( "Category", "WindowsOnly" )]
    public void DeleteEntryはEntryPathが空のとき失敗結果を返す() {
        // Arrange
        var service = new ZipService(logger.Object);
        var zipPath = Path.Join(_tempDir, "dummy.zip");
        using(ZipFile.Open( zipPath, ZipArchiveMode.Create )) { }

        // Act
        var result = service.DeleteEntry( zipPath, "" );

        // Assert
        Assert.True( result.IsFailed );
        Assert.Contains( "zip ファイルパスが空です", result.Errors[0].Message );
    }

    #endregion
}