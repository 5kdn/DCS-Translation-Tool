using System.IO.Compression;

using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// TranslationDictionaryService の動作を検証する。
/// </summary>
public sealed class TranslationDictionaryServiceTests : IDisposable {
    private readonly string _tempDirectory = Path.Combine( Path.GetTempPath(), $"TranslationDictionaryServiceTests_{Guid.NewGuid():N}" );

    public TranslationDictionaryServiceTests() {
        Directory.CreateDirectory( _tempDirectory );
    }

    [Fact]
    public void LoadDictionaryはdictionaryエントリから項目一覧を読み込む() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
dictionary = {
    ["key2"] = "value2",
    ["key1"] = "value1"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            item => {
                Assert.Equal( "key1", item.Key );
                Assert.Equal( "value1", item.Original );
                Assert.Equal( string.Empty, item.Translated );
            },
            item => Assert.Equal( "key2", item.Key ) );
    }

    [Fact]
    public void LoadDictionaryはエントリ名の大文字小文字差異を吸収する() {
        var archivePath = CreateArchive( "L10N/default/dictionary", """
dictionary = {
    ["key"] = "value"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Single( result.Value );
    }

    [Fact]
    public void LoadDictionaryはdictionaryエントリが存在しないとき失敗する() {
        var archivePath = CreateArchive( "other.txt", "none" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsFailed );
    }

    [Fact]
    public void LoadDictionaryは不正なLuaのとき空一覧を返す() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", "broken" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Empty( result.Value );
    }

    [Fact]
    public void LoadDictionaryは重複keyのとき後勝ちにする() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
dictionary = {
    ["key"] = "old",
    ["key"] = "new"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        var item = Assert.Single( result.Value );
        Assert.Equal( "new", item.Original );
        Assert.Equal( string.Empty, item.Translated );
    }

    private string CreateArchive( string entryPath, string content ) {
        var archivePath = Path.Combine( _tempDirectory, $"{Guid.NewGuid():N}.miz" );
        using var archive = ZipFile.Open( archivePath, ZipArchiveMode.Create );
        var entry = archive.CreateEntry( entryPath );
        using var stream = entry.Open();
        using var writer = new StreamWriter( stream );
        writer.Write( content );
        return archivePath;
    }

    public void Dispose() {
        if(Directory.Exists( _tempDirectory )) {
            Directory.Delete( _tempDirectory, true );
        }
    }
}