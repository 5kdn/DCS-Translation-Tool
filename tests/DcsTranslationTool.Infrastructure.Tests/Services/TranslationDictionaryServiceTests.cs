using System.IO.Compression;

using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// TranslationDictionaryService の動作を検証する。
/// </summary>
public sealed class TranslationDictionaryServiceTests : IDisposable {
    private readonly string _tempDirectory = Path.Combine( Path.GetTempPath(), $"TranslationDictionaryServiceTests_{Guid.NewGuid():N}" );
    private const string ProjectIdVersion = "DCS Translation Japanese 1.4.0.0";
    private const string PotCreationDate = "2026-03-13 14:25+09:00";
    private const string PoRevisionDate = "2026-03-13 14:25+09:00";
    private const string XGenerator = "DCS Translation Tool 1.4.0.0";

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
    public void HasArchiveEntryは指定エントリが存在するときtrueを返す() {
        var archivePath = CreateArchive( "l10n/JP/dictionary", "dictionary = {}" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.HasArchiveEntry( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value );
    }

    [Fact]
    public void HasArchiveEntryは大文字小文字差異を吸収する() {
        var archivePath = CreateArchive( "L10N/jp/dictionary", "dictionary = {}" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.HasArchiveEntry( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value );
    }

    [Fact]
    public void HasArchiveEntryはエントリが存在しないときfalseを返す() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", "dictionary = {}" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.HasArchiveEntry( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.False( result.Value );
    }

    [Fact]
    public void LoadDictionaryは指定エントリから項目一覧を読み込む() {
        var archivePath = CreateArchive( "l10n/JP/dictionary", """
dictionary = {
    ["key2"] = "value2",
    ["key1"] = "value1"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            item => Assert.Equal( "key1", item.Key ),
            item => Assert.Equal( "key2", item.Key ) );
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
    public void LoadDictionaryはコメントアウトされた行を無視する() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
dictionary = {
    -- ["ignored"] = "old",
    ["key"] = "value"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        var item = Assert.Single( result.Value );
        Assert.Equal( "key", item.Key );
        Assert.Equal( "value", item.Original );
    }

    [Fact]
    public void LoadDictionaryは先頭コメントにdictionaryを含んでも実体を読み込む() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
-- dictionary generated by tool
dictionary = {
    ["key"] = "value"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionary( archivePath );

        var item = Assert.Single( result.Value );
        Assert.Equal( "key", item.Key );
        Assert.Equal( "value", item.Original );
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
            "Target marked with yellow smoke.\n\n You are cleared in. Out.",
            item.Original );
    }

    [Theory]
    [InlineData( "\\n", "\n" )]
    [InlineData( "\\r", "\n" )]
    [InlineData( "\\t", "\t" )]
    [InlineData( "\\\\", "\\" )]
    [InlineData( "\\\"", "\"" )]
    [InlineData( "\\\n", "\n" )]
    [InlineData( "\\\r", "\n" )]
    [InlineData( "\\\r\n", "\n" )]
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

    [Fact]
    public async Task LoadDictionaryFileはローカルdictionaryファイルから項目一覧を読み込む() {
        var path = Path.Combine( _tempDirectory, "dictionary" );
        await File.WriteAllTextAsync(
            path,
            """
dictionary = {
    ["key2"] = "value2",
    ["key1"] = "value1"
}
""",
            TestContext.Current.CancellationToken );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionaryFile( path );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            item => {
                Assert.Equal( "key1", item.Key );
                Assert.Equal( "value1", item.Translated );
            },
            item => {
                Assert.Equal( "key2", item.Key );
                Assert.Equal( "value2", item.Translated );
            } );
    }

    [Fact]
    public async Task LoadDictionaryFileは先頭コメントにdictionaryを含んでも実体を読み込む() {
        var path = Path.Combine( _tempDirectory, "dictionary-with-comment" );
        await File.WriteAllTextAsync(
            path,
            """
-- dictionary generated by tool
dictionary = {
    ["key"] = "value"
}
""",
            TestContext.Current.CancellationToken );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionaryFile( path );

        var item = Assert.Single( result.Value );
        Assert.Equal( "key", item.Key );
        Assert.Equal( "value", item.Translated );
    }

    [Fact]
    public async Task LoadDictionaryFileは不正なLuaのとき空一覧を返す() {
        var path = Path.Combine( _tempDirectory, "broken-dictionary" );
        await File.WriteAllTextAsync( path, "broken", TestContext.Current.CancellationToken );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionaryFile( path );

        Assert.True( result.IsSuccess );
        Assert.Empty( result.Value );
    }

    [Fact]
    public void LoadDictionaryFileはファイルが存在しないとき失敗する() {
        var path = Path.Combine( _tempDirectory, "missing-dictionary" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadDictionaryFile( path );

        Assert.True( result.IsFailed );
    }

    [Fact]
    public async Task SaveDictionaryAsyncはLua形式で入力順を維持して保存する() {
        var path = Path.Combine( _tempDirectory, "dictionary.lua" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SaveDictionaryAsync(
            path,
            [
                new TranslationDictionaryItem( "key2", "o2" ) { Translated = "translated2" },
                new TranslationDictionaryItem( "key1", "o1" ) { Translated = "translated1" }
            ],
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Equal(
            """
dictionary = {
    ["key2"] = "translated2",
    ["key1"] = "translated1",
}
""".ReplaceLineEndings( "\n" ) + "\n",
            content );
        Assert.DoesNotContain( "\r\n", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SaveDictionaryAsyncは主要escapeをLua文字列へ変換する() {
        var path = Path.Combine( _tempDirectory, "escaped.lua" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SaveDictionaryAsync(
            path,
            [
                new TranslationDictionaryItem( "key\\\"x", "o1" ) {
                    Translated = "line1\nline2\t\"quoted\"\\"
                }
            ],
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Contains( "[\"key\\\\\\\"x\"] = \"line1\\\nline2\\t\\\"quoted\\\"\\\\\"", content, StringComparison.Ordinal );
        Assert.DoesNotContain( "\r\n", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SaveDictionaryAsyncは連続改行を各改行直前の行継続へ変換する() {
        var path = Path.Combine( _tempDirectory, "blank-lines.lua" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SaveDictionaryAsync(
            path,
            [
                new TranslationDictionaryItem( "key", "o1" ) {
                    Translated = "asdf\n\nzxcv"
                }
            ],
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Contains( "[\"key\"] = \"asdf\\\n\\\nzxcv\"", content, StringComparison.Ordinal );
        Assert.DoesNotContain( "asdf\n\\\n", content, StringComparison.Ordinal );
        Assert.DoesNotContain( "\r", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SaveDictionaryAsyncはCRLFをLF正規化した上で行継続へ変換する() {
        var path = Path.Combine( _tempDirectory, "crlf.lua" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SaveDictionaryAsync(
            path,
            [
                new TranslationDictionaryItem( "key", "o1" ) {
                    Translated = "asdf\r\n\r\nzxcv"
                }
            ],
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Contains( "[\"key\"] = \"asdf\\\n\\\nzxcv\"", content, StringComparison.Ordinal );
        Assert.DoesNotContain( "\r", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SaveDictionaryAsyncは複数行valueを再読込すると内部でLFに戻る() {
        var path = Path.Combine( _tempDirectory, "roundtrip.lua" );
        var archivePath = Path.Combine( _tempDirectory, "roundtrip.miz" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );
        var sourceItems = new[] {
            new TranslationDictionaryItem( "key", "original" ) { Translated = "line1\nline2\nline3" }
        };

        await sut.SaveDictionaryAsync( path, sourceItems, TestContext.Current.CancellationToken );

        using(var archive = ZipFile.Open( archivePath, ZipArchiveMode.Create )) {
            var entry = archive.CreateEntry( "l10n/default/dictionary" );
            await using var stream = entry.Open();
            await using var writer = new StreamWriter( stream, leaveOpen: false );
            var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
            await writer.WriteAsync( content );
        }

        var result = sut.LoadDictionary( archivePath );

        var item = Assert.Single( result.Value );
        Assert.Equal( "line1\nline2\nline3", item.Original );
    }

    [Fact]
    public void LoadEditableDictionaryはコメントアウト行を除外し最後の有効value範囲を保持する() {
        var archivePath = CreateArchive( "l10n/default/dictionary", """
dictionary = {
    -- ["key1"] = "commented",
    ["key1"] = "value1",
    ["key2"] = "value2",
    ["key1"] = "value3"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadEditableDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Equal( ["key1", "key2"], [.. result.Value.Items.Select( item => item.Key )] );
        Assert.Equal( "value3", result.Value.Items.Single( item => item.Key == "key1" ).Original );
        Assert.Equal( "value3".Length, result.Value.ValueRanges["key1"].Length );
    }

    [Fact]
    public void LoadEditableDictionaryは先頭コメントにdictionaryを含んでもItemsとValueRangesを返す() {
        var archivePath = CreateArchive( "l10n/default/dictionary", """
-- dictionary generated by tool
dictionary = {
    ["key1"] = "value1",
    ["key2"] = "value2"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadEditableDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Equal( ["key1", "key2"], [.. result.Value.Items.Select( item => item.Key )] );
        Assert.Equal( "value1", result.Value.Items.Single( item => item.Key == "key1" ).Original );
        Assert.Equal( "value2", result.Value.Items.Single( item => item.Key == "key2" ).Original );
        Assert.Equal( "value1".Length, result.Value.ValueRanges["key1"].Length );
        Assert.Equal( "value2".Length, result.Value.ValueRanges["key2"].Length );
    }

    [Fact]
    public async Task SaveDictionaryAsyncは元の構造を維持してvalueのみを書き換える() {
        var path = Path.Combine( _tempDirectory, "preserve.lua" );
        var archivePath = CreateArchive( "l10n/default/dictionary", """
dictionary = {
    -- ["key1"] = "commented",

    ["key1"] = "old1", -- inline
    ["key2"] = "old2",
    ["key1"] = "old3"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );
        var editableResult = sut.LoadEditableDictionary( archivePath );

        await sut.SaveDictionaryAsync(
            path,
            editableResult.Value,
            new Dictionary<string, string>( StringComparer.Ordinal )
            {
                ["key1"] = "line1\nline2",
                ["key2"] = "updated2"
            },
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Equal(
            """
dictionary = {
    -- ["key1"] = "commented",

    ["key1"] = "old1", -- inline
    ["key2"] = "updated2",
    ["key1"] = "line1\
line2"
}
""".ReplaceLineEndings( "\n" ),
            content );
        Assert.DoesNotContain( "\r\n", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SaveDictionaryAsyncは元の構造を維持しつつ連続改行を正しい位置へ書き出す() {
        var path = Path.Combine( _tempDirectory, "preserve-blank-lines.lua" );
        var archivePath = CreateArchive( "l10n/default/dictionary", """
dictionary = {
    ["key"] = "old"
}
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );
        var editableResult = sut.LoadEditableDictionary( archivePath );

        await sut.SaveDictionaryAsync(
            path,
            editableResult.Value,
            new Dictionary<string, string>( StringComparer.Ordinal )
            {
                ["key"] = "asdf\n\nzxcv"
            },
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Equal(
            """
dictionary = {
    ["key"] = "asdf\
\
zxcv"
}
""".ReplaceLineEndings( "\n" ),
            content );
    }

    [Fact]
    public async Task SaveDictionaryAsyncは元の構造を維持した書き出し結果を再読込できる() {
        var path = Path.Combine( _tempDirectory, "preserve-roundtrip.lua" );
        var sourceArchivePath = CreateArchive( "l10n/default/dictionary", """
dictionary = {
    -- ["key1"] = "commented",
    ["key1"] = "old1",
    ["key2"] = "old2"
}
""" );
        var roundtripArchivePath = Path.Combine( _tempDirectory, "preserve-roundtrip.miz" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );
        var editableResult = sut.LoadEditableDictionary( sourceArchivePath );

        await sut.SaveDictionaryAsync(
            path,
            editableResult.Value,
            new Dictionary<string, string>( StringComparer.Ordinal )
            {
                ["key1"] = "line1\nline2",
                ["key2"] = "updated2"
            },
            TestContext.Current.CancellationToken );

        using(var archive = ZipFile.Open( roundtripArchivePath, ZipArchiveMode.Create )) {
            var entry = archive.CreateEntry( "l10n/default/dictionary" );
            await using var stream = entry.Open();
            await using var writer = new StreamWriter( stream, leaveOpen: false );
            var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
            await writer.WriteAsync( content );
        }

        var roundtripResult = sut.LoadDictionary( roundtripArchivePath );

        Assert.True( roundtripResult.IsSuccess );
        Assert.Collection(
            roundtripResult.Value,
            item => {
                Assert.Equal( "key1", item.Key );
                Assert.Equal( "line1\nline2", item.Original );
            },
            item => {
                Assert.Equal( "key2", item.Key );
                Assert.Equal( "updated2", item.Original );
            } );
    }

    [Fact]
    public async Task SaveDictionaryAsyncはLuaコンパイル検証に失敗したとき保存しない() {
        var path = Path.Combine( _tempDirectory, "invalid-preserve.lua" );
        var sourceArchivePath = CreateArchive( "l10n/default/dictionary", """
dictionary = {
    ["key"] = "old"
}
]]
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );
        var editableResult = sut.LoadEditableDictionary( sourceArchivePath );

        await Assert.ThrowsAsync<InvalidOperationException>( async () =>
            await sut.SaveDictionaryAsync(
                path,
                editableResult.Value,
                new Dictionary<string, string>( StringComparer.Ordinal )
                {
                    ["key"] = "updated"
                },
                TestContext.Current.CancellationToken ) );
        Assert.False( File.Exists( path ) );
    }

    [Fact]
    public void LoadCsvはヘッダーとquotedFieldを復元する() {
        var path = CreateCsvFile(
            "Enabled,Key,Original,Translated\ntrue,\"key,1\",\"line1\nline2\",\"translated \"\"quoted\"\"\"" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadCsv( path );

        var entry = Assert.Single( result.Value );
        Assert.True( result.IsSuccess );
        Assert.Equal( "key,1", entry.Key );
        Assert.Equal( "line1\nline2", entry.Original );
        Assert.Equal( "translated \"quoted\"", entry.Translated );
        Assert.True( entry.IsEnabled );
    }

    [Fact]
    public void LoadCsvは旧形式を有効行として復元する() {
        var path = CreateCsvFile(
            "Key,Original,Translated\nkey1,original,translated" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadCsv( path );

        var entry = Assert.Single( result.Value );
        Assert.Equal( "key1", entry.Key );
        Assert.True( entry.IsEnabled );
    }

    [Fact]
    public void LoadCsvはヘッダー不一致のとき失敗する() {
        var path = CreateCsvFile(
            """
Context,Original,Translated
key,original,translated
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadCsv( path );

        Assert.True( result.IsFailed );
    }

    [Fact]
    public async Task SaveCsvAsyncはヘッダーとLF改行で保存する() {
        var path = Path.Combine( _tempDirectory, "dictionary.csv" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SaveCsvAsync(
            path,
            [
                new TranslationDictionaryItem( "key1", "o1" ) { Translated = "t1" }
            ],
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );

        Assert.Equal(
            """
Enabled,Key,Original,Translated
true,key1,o1,t1
""".ReplaceLineEndings( "\n" ) + "\n",
            content );
        Assert.DoesNotContain( "\r", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SaveCsvAsyncはカンマ引用符改行をエスケープする() {
        var path = Path.Combine( _tempDirectory, "escaped.csv" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SaveCsvAsync(
            path,
            [
                new TranslationDictionaryItem( "key,1", "line1\nline2" ) {
                    Translated = "\"quoted\"",
                    IsEnabled = false
                }
            ],
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );

        Assert.Contains( "false,\"key,1\",\"line1\nline2\",\"\"\"quoted\"\"\"", content, StringComparison.Ordinal );
        Assert.DoesNotContain( "\r", content, StringComparison.Ordinal );
    }

    [Fact]
    public void LoadPoはヘッダーを無視してエントリーを読み込む() {
        var path = CreatePoFile(
            """
msgid ""
msgstr ""
"Project-Id-Version: test\n"

#, no-wrap
msgctxt "DictKey_sortie_1"
msgid "original"
msgstr "translated"
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadPo( path );

        var entry = Assert.Single( result.Value );
        Assert.True( result.IsSuccess );
        Assert.Equal( "DictKey_sortie_1", entry.Context );
        Assert.Equal( "original", entry.Original );
        Assert.Equal( "translated", entry.Translated );
        Assert.True( entry.IsEnabled );
    }

    [Fact]
    public void LoadPoはコメントと複数行文字列を復元する() {
        var path = CreatePoFile(
            """
# translator comment
#, no-wrap
msgctxt "key"
msgid ""
"line1\n"
"line2\t\"quoted\"\\"
msgstr ""
"translated1\n"
"translated2"
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadPo( path );

        var entry = Assert.Single( result.Value );
        Assert.Equal( "line1\nline2\t\"quoted\"\\", entry.Original );
        Assert.Equal( "translated1\ntranslated2", entry.Translated );
        Assert.True( entry.IsEnabled );
    }

    [Fact]
    public void LoadPoはコメントアウト済みエントリーを無効行として復元する() {
        var path = CreatePoFile(
            """
#~ msgctxt "key"
#~ msgid ""
#~ "line1\n"
#~ "line2"
#~ msgstr "translated"
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadPo( path );

        var entry = Assert.Single( result.Value );
        Assert.Equal( "key", entry.Context );
        Assert.Equal( "line1\nline2", entry.Original );
        Assert.Equal( "translated", entry.Translated );
        Assert.False( entry.IsEnabled );
    }

    [Fact]
    public void LoadPoは重複したmsgctxtとmsgidの組み合わせをそのまま返す() {
        var path = CreatePoFile(
            """
#, no-wrap
msgctxt "key"
msgid "original"
msgstr "translated1"

#, no-wrap
msgctxt "key"
msgid "original"
msgstr "translated2"
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadPo( path );

        Assert.True( result.IsSuccess );
        Assert.Equal( 2, result.Value.Count );
        Assert.Equal( "translated1", result.Value[0].Translated );
        Assert.Equal( "translated2", result.Value[1].Translated );
    }

    [Fact]
    public void LoadPoは不正形式のとき失敗する() {
        var path = CreatePoFile(
            """
#, no-wrap
msgctxt "key"
msgid "original"
""" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        var result = sut.LoadPo( path );

        Assert.True( result.IsFailed );
    }

    [Fact]
    public async Task SavePoAsyncは指定ヘッダーとnoWrap属性を出力する() {
        var path = Path.Combine( _tempDirectory, "dictionary.po" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SavePoAsync(
            path,
            [
                new TranslationDictionaryItem( "DictKey_sortie_1", "original" ) { Translated = "translated" }
            ],
            ProjectIdVersion,
            PotCreationDate,
            PoRevisionDate,
            XGenerator,
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.StartsWith(
            """
msgid ""
msgstr ""
"Project-Id-Version: DCS Translation Japanese 1.4.0.0\n"
"POT-Creation-Date: 2026-03-13 14:25+09:00\n"
"PO-Revision-Date: 2026-03-13 14:25+09:00\n"
"Language: ja_JP\n"
"MIME-Version: 1.0\n"
"Content-Type: text/plain; charset=UTF-8\n"
"Content-Transfer-Encoding: 8bit\n"
"X-Generator: DCS Translation Tool 1.4.0.0\n"

#, no-wrap
msgctxt "DictKey_sortie_1"
msgid "original"
msgstr "translated"

""".ReplaceLineEndings( "\n" ),
            content,
            StringComparison.Ordinal );
        Assert.DoesNotContain( "\r", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SavePoAsyncは無効行をコメントアウトして出力する() {
        var path = Path.Combine( _tempDirectory, "disabled.po" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SavePoAsync(
            path,
            [
                new TranslationDictionaryItem( "key", "original" ) {
                    Translated = "translated",
                    IsEnabled = false
                }
            ],
            ProjectIdVersion,
            PotCreationDate,
            PoRevisionDate,
            XGenerator,
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Contains( "#~ msgctxt \"key\"", content, StringComparison.Ordinal );
        Assert.Contains( "#~ msgid \"original\"", content, StringComparison.Ordinal );
        Assert.Contains( "#~ msgstr \"translated\"", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SavePoAsyncは複数行文字列をPO形式でエスケープする() {
        var path = Path.Combine( _tempDirectory, "multiline.po" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SavePoAsync(
            path,
            [
                new TranslationDictionaryItem( "key\\\"x", "line1\nline2\t\"quoted\"\\" ) {
                    Translated = "translated1\ntranslated2"
                }
            ],
            ProjectIdVersion,
            PotCreationDate,
            PoRevisionDate,
            XGenerator,
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Contains(
            """
msgctxt "key\\\"x"
msgid ""
"line1\n"
"line2\t\"quoted\"\\"
msgstr ""
"translated1\n"
"translated2"
""".ReplaceLineEndings( "\n" ),
            content,
            StringComparison.Ordinal );
    }

    [Fact]
    public async Task SavePoAsyncは未翻訳行を空msgstrで出力する() {
        var path = Path.Combine( _tempDirectory, "empty.po" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SavePoAsync(
            path,
            [
                new TranslationDictionaryItem( "key", "original" )
            ],
            ProjectIdVersion,
            PotCreationDate,
            PoRevisionDate,
            XGenerator,
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        Assert.Contains( "msgstr \"\"", content, StringComparison.Ordinal );
    }

    [Fact]
    public async Task SavePoAsyncは入力順を維持して保存する() {
        var path = Path.Combine( _tempDirectory, "ordered.po" );
        var sut = new TranslationDictionaryService( new Mock<ILoggingService>().Object );

        await sut.SavePoAsync(
            path,
            [
                new TranslationDictionaryItem( "key2", "o2" ) { Translated = "t2" },
                new TranslationDictionaryItem( "key1", "o1" ) { Translated = "t1" }
            ],
            ProjectIdVersion,
            PotCreationDate,
            PoRevisionDate,
            XGenerator,
            TestContext.Current.CancellationToken );

        var content = await File.ReadAllTextAsync( path, TestContext.Current.CancellationToken );
        var key2Index = content.IndexOf( "msgctxt \"key2\"", StringComparison.Ordinal );
        var key1Index = content.IndexOf( "msgctxt \"key1\"", StringComparison.Ordinal );

        Assert.True( key2Index >= 0 );
        Assert.True( key1Index > key2Index );
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

    private string CreatePoFile( string content ) {
        var path = Path.Combine( _tempDirectory, $"{Guid.NewGuid():N}.po" );
        File.WriteAllText( path, content.ReplaceLineEndings( "\n" ) );
        return path;
    }

    private string CreateCsvFile( string content ) {
        var path = Path.Combine( _tempDirectory, $"{Guid.NewGuid():N}.csv" );
        File.WriteAllText( path, content.ReplaceLineEndings( "\n" ) );
        return path;
    }

    public void Dispose() {
        if(Directory.Exists( _tempDirectory )) {
            Directory.Delete( _tempDirectory, true );
        }
    }
}