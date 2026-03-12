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

    [Fact]
    public void LoadDictionaryはLua文字列の行継続で改行コードを保持して読み込む() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
dictionary =
{
    ["DictKey_10"] = "Target marked with yellow smoke.\
\
 You are cleared in. Out.",
} -- end of dictionary
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        var item = Assert.Single( result.Value );
        Assert.Equal( "DictKey_10", item.Key );
        Assert.Equal(
            "Target marked with yellow smoke."
            + Environment.NewLine
            + Environment.NewLine
            + " You are cleared in. Out.",
            item.Original );
    }

    [Theory]
    [InlineData( "\\n", "\n" )]
    [InlineData( "\\r", "\r" )]
    [InlineData( "\\t", "\t" )]
    [InlineData( "\\\\", "\\" )]
    [InlineData( "\\\"", "\"" )]
    [InlineData( "\\\n", "\n" )]
    [InlineData( "\\\r", "\r" )]
    [InlineData( "\\\r\n", "\r\n" )]
    public void LoadDictionaryはLua文字列の主要escapeを復元する( string escapedValue, string expectedValue ) {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", $$"""
dictionary = {
    ["key"] = "{{escapedValue}}"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        var item = Assert.Single( result.Value );
        Assert.Equal( expectedValue, item.Original );
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