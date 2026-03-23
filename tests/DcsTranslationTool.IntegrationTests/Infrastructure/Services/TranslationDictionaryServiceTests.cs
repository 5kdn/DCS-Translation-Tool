using System.IO.Compression;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

/// <summary>
/// <see cref="TranslationDictionaryService"/> の archive 解決を検証する。
/// </summary>
public sealed class TranslationDictionaryServiceTests : IDisposable {
    private readonly string _tempDirectory = Path.Combine( Path.GetTempPath(), $"TranslationDictionaryServiceTests_{Guid.NewGuid():N}" );

    /// <summary>
    /// テスト用ディレクトリを初期化する。
    /// </summary>
    public TranslationDictionaryServiceTests() {
        Directory.CreateDirectory( _tempDirectory );
    }

    /// <summary>
    /// default dictionary を archive から読み込むことを検証する。
    /// </summary>
    [Fact]
    public void LoadDictionaryはdictionaryエントリから項目一覧を読み込む() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
dictionary = {
    ["key2"] = "value2",
    ["key1"] = "value1"
}
""" );
        var sut = CreateSut();

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            item => {
                Assert.Equal( "key1", item.Key );
                Assert.Equal( "value1", item.Original );
            },
            item => Assert.Equal( "key2", item.Key ) );
    }

    /// <summary>
    /// dictionary エントリ名の大文字小文字差異を吸収することを検証する。
    /// </summary>
    [Fact]
    public void LoadDictionaryはエントリ名の大文字小文字差異を吸収する() {
        var archivePath = CreateArchive( "L10N/default/dictionary", """
dictionary = {
    ["key"] = "value"
}
""" );
        var sut = CreateSut();

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Single( result.Value );
    }

    /// <summary>
    /// default dictionary が存在しない場合に失敗することを検証する。
    /// </summary>
    [Fact]
    public void LoadDictionaryはdictionaryエントリが存在しないとき失敗する() {
        var archivePath = CreateArchive( "other.txt", "none" );
        var sut = CreateSut();

        var result = sut.LoadDictionary( archivePath );

        Assert.True( result.IsFailed );
    }

    /// <summary>
    /// 指定エントリ存在判定が成功することを検証する。
    /// </summary>
    [Fact]
    public void HasArchiveEntryは指定エントリが存在するときtrueを返す() {
        var archivePath = CreateArchive( "l10n/JP/dictionary", "dictionary = {}" );
        var sut = CreateSut();

        var result = sut.HasArchiveEntry( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value );
    }

    /// <summary>
    /// エントリ存在判定が大文字小文字差異を吸収することを検証する。
    /// </summary>
    [Fact]
    public void HasArchiveEntryは大文字小文字差異を吸収する() {
        var archivePath = CreateArchive( "L10N/jp/dictionary", "dictionary = {}" );
        var sut = CreateSut();

        var result = sut.HasArchiveEntry( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value );
    }

    /// <summary>
    /// エントリが存在しない場合に false を返すことを検証する。
    /// </summary>
    [Fact]
    public void HasArchiveEntryはエントリが存在しないときfalseを返す() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", "dictionary = {}" );
        var sut = CreateSut();

        var result = sut.HasArchiveEntry( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.False( result.Value );
    }

    /// <summary>
    /// default のみ存在する archive を読み込めることを検証する。
    /// </summary>
    [Fact]
    public void LoadArchiveDictionariesはdefaultのみ存在するときJPなしで返す() {
        var archivePath = CreateArchive( "l10n/DEFAULT/dictionary", """
dictionary = {
    ["key1"] = "value1"
}
""" );
        var sut = CreateSut();

        var result = sut.LoadArchiveDictionaries( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Single( result.Value.DefaultDictionaryItems );
        Assert.False( result.Value.HasJapaneseDictionary );
        Assert.Empty( result.Value.JapaneseDictionaryItems );
    }

    /// <summary>
    /// default と JP dictionary をまとめて読み込めることを検証する。
    /// </summary>
    [Fact]
    public void LoadArchiveDictionariesはdefaultとJPをまとめて返す() {
        var archivePath = CreateArchive(
            ("l10n/DEFAULT/dictionary", """
dictionary = {
    ["key1"] = "value1"
}
"""),
            ("l10n/JP/dictionary", """
dictionary = {
    ["key1"] = "jp1"
}
""") );
        var sut = CreateSut();

        var result = sut.LoadArchiveDictionaries( archivePath );

        Assert.True( result.IsSuccess );
        Assert.Single( result.Value.DefaultDictionaryItems );
        Assert.True( result.Value.HasJapaneseDictionary );
        Assert.Single( result.Value.JapaneseDictionaryItems );
        Assert.Equal( "jp1", result.Value.JapaneseDictionaryItems[0].Original );
    }

    /// <summary>
    /// default dictionary が欠落すると失敗することを検証する。
    /// </summary>
    [Fact]
    public void LoadArchiveDictionariesはdefaultが欠落するとき失敗する() {
        var archivePath = CreateArchive( "l10n/JP/dictionary", "dictionary = {}" );
        var sut = CreateSut();

        var result = sut.LoadArchiveDictionaries( archivePath );

        Assert.True( result.IsFailed );
    }

    /// <summary>
    /// 指定 entryPath の dictionary を archive から読み込めることを検証する。
    /// </summary>
    [Fact]
    public void LoadDictionaryは指定エントリから項目一覧を読み込む() {
        var archivePath = CreateArchive( "l10n/JP/dictionary", """
dictionary = {
    ["key2"] = "value2",
    ["key1"] = "value1"
}
""" );
        var sut = CreateSut();

        var result = sut.LoadDictionary( archivePath, "l10n/JP/dictionary" );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            item => Assert.Equal( "key1", item.Key ),
            item => Assert.Equal( "key2", item.Key ) );
    }

    /// <summary>
    /// テスト対象を生成する。
    /// </summary>
    /// <returns>生成したサービスを返す。</returns>
    private static TranslationDictionaryService CreateSut() =>
        new( new Mock<ILoggingService>().Object );

    /// <summary>
    /// 単一 entry の archive を生成する。
    /// </summary>
    /// <param name="entryPath">entryPath。</param>
    /// <param name="content">entry 内容。</param>
    /// <returns>生成した archive パスを返す。</returns>
    private string CreateArchive( string entryPath, string content ) {
        return CreateArchive( (entryPath, content) );
    }

    /// <summary>
    /// 複数 entry の archive を生成する。
    /// </summary>
    /// <param name="entries">entry 一覧。</param>
    /// <returns>生成した archive パスを返す。</returns>
    private string CreateArchive( params (string EntryPath, string Content)[] entries ) {
        var archivePath = Path.Combine( _tempDirectory, $"{Guid.NewGuid():N}.miz" );
        using var archive = ZipFile.Open( archivePath, ZipArchiveMode.Create );
        foreach(var (entryPath, content) in entries) {
            var entry = archive.CreateEntry( entryPath );
            using var stream = entry.Open();
            using var writer = new StreamWriter( stream );
            writer.Write( content );
        }

        return archivePath;
    }

    /// <summary>
    /// 使用した一時ディレクトリを破棄する。
    /// </summary>
    public void Dispose() {
        if(Directory.Exists( _tempDirectory )) {
            Directory.Delete( _tempDirectory, true );
        }
    }
}