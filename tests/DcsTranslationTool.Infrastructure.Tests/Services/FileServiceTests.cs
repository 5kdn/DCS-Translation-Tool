using System.Text;

using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

internal sealed class TestData {
    public int Id { get; set; }
    public string? Name { get; set; }
}

/// <summary>
/// <see cref="FileService"/> の単体テスト。
/// </summary>
public class FileServiceTests : IDisposable {
    private readonly Mock<ILoggingService> logger = new();
    private readonly string _tempDir;

    public FileServiceTests() {
        _tempDir = Path.Join( Path.GetTempPath(), Guid.NewGuid().ToString() );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) Directory.Delete( _tempDir, true );
        GC.SuppressFinalize( this );
    }

    #region Read

    [Fact]
    public void Readは有効なJsonを正しくデシリアライズできる() {
        // Arrange
        var sut = new FileService(logger.Object);
        var path = Path.Combine(_tempDir, "data.json");
        var expected = new TestData { Id = 1, Name = "Test" };
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(expected);
        File.WriteAllText( path, json, Encoding.UTF8 );

        // Act
        var result = sut.Read<TestData>(_tempDir, "data.json");

        // Assert
        Assert.NotNull( result );
        Assert.Equal( expected.Id, result!.Id );
        Assert.Equal( expected.Name, result.Name );
    }

    [Fact]
    public void Readはファイルが存在しない場合にnullを返す() {
        // Arrange & Act
        var sut = new FileService(logger.Object);

        // Assert
        var result = sut.Read<TestData>(_tempDir, "notfound.json");
        Assert.Null( result );
    }

    [Fact]
    public void Readはファイルが空の場合はnullを返す() {
        // Arrange & Act
        var path = Path.Combine(_tempDir, "empty.json");
        File.WriteAllText( path, string.Empty, Encoding.UTF8 );

        var sut = new FileService(logger.Object);
        var result = sut.Read<TestData>(_tempDir, "empty.json");

        // Assert
        Assert.Null( result );
    }

    [Theory]
    [InlineData( "", "file.json" )]
    [InlineData( "   ", "file.json" )]
    public void Readはフォルダパスが不正な場合は例外を投げる( string folderPath, string fileName ) {
        // Arrange
        var sut = new FileService(logger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>( () => sut.Read<TestData>( folderPath!, fileName ) );
    }

    [Theory]
    [InlineData( "folder", "" )]
    [InlineData( "folder", "   " )]
    public void Readはファイル名が不正な場合は例外を投げる( string folderPath, string fileName ) {
        // Arrange
        var sut = new FileService(logger.Object);

        // Act & Assert
        Assert.Throws<ArgumentException>( () => sut.Read<TestData>( folderPath, fileName! ) );
    }

    #endregion

    #region Save

    [Fact]
    public void Saveはオブジェクトを正しく保存できる() {
        // Arrange
        var sut = new FileService(logger.Object);
        const string fileName = "saved.json";
        var data = new TestData { Id = 42, Name = "SavedData" };

        // Act
        sut.Save( _tempDir, fileName, data );

        // Assert
        var path = Path.Combine(_tempDir, fileName);
        Assert.True( File.Exists( path ) );

        var content = File.ReadAllText(path, Encoding.UTF8);
        var deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject<TestData>(content);

        Assert.NotNull( deserialized );
        Assert.Equal( data.Id, deserialized!.Id );
        Assert.Equal( data.Name, deserialized.Name );
    }

    #endregion

    #region Delete

    [Fact]
    public void Deleteは存在するファイルを削除できる() {
        // Arrange
        var sut = new FileService(logger.Object);
        const string fileName = "delete.json";
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText( path, "dummy", Encoding.UTF8 );

        // Act
        sut.Delete( _tempDir, fileName );

        // Assert
        Assert.False( File.Exists( path ) );
    }

    [Fact]
    public void Deleteは存在しないファイルでは何もしない() {
        // Arrange
        var sut = new FileService(logger.Object);
        const string fileName = "nonexistent.json";
        var path = Path.Combine(_tempDir, fileName);

        // Act
        sut.Delete( _tempDir, fileName );

        // Assert
        Assert.False( File.Exists( path ) );
    }

    #endregion
}