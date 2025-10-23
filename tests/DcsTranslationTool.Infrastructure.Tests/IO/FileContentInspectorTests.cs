using System.Text;

using DcsTranslationTool.Infrastructure.IO;

namespace DcsTranslationTool.Infrastructure.Tests.IO;

/// <summary>
/// FileContentInspectorの解析結果を検証するテストを提供する。
/// </summary>
public sealed class FileContentInspectorTests {
    private readonly FileContentInspector sut = new();
    [Fact]
    public void Inspectは空配列をUTF8空文字として扱う() {
        // Arrange
        var content = Array.Empty<byte>();

        // Act
        var result = sut.Inspect( content );

        // Assert
        Assert.False( result.IsBinary );
        Assert.Equal( Encoding.UTF8.WebName, result.Encoding?.WebName );
        Assert.Equal( 1.0, result.DetectionConfidence );
        Assert.Equal( string.Empty, result.Text );
        Assert.Equal( 0, result.ByteCount );
    }

    [Fact]
    public void InspectはUTF8BOMを優先してデコードする() {
        // Arrange
        const string text = "テスト";
        var encoding = new UTF8Encoding( encoderShouldEmitUTF8Identifier: true );
        var content = encoding.GetPreamble().Concat( encoding.GetBytes( text ) ).ToArray();

        // Act
        var result = sut.Inspect( content );

        // Assert
        Assert.False( result.IsBinary );
        Assert.Equal( Encoding.UTF8.WebName, result.Encoding?.WebName );
        Assert.NotNull( result.Text );
        Assert.Equal( text, result.Text!.TrimStart( '\uFEFF' ) );
        Assert.Equal( content.Length, result.ByteCount );
        Assert.Equal( 1.0, result.DetectionConfidence );
    }

    [Fact]
    public void InspectはASCIIテキストをテキストと判定する() {
        // Arrange
        const string text = "Hello, world!";
        var content = Encoding.UTF8.GetBytes( text );

        // Act
        var result = sut.Inspect( content );

        // Assert
        Assert.False( result.IsBinary );
        Assert.NotNull( result.Encoding );
        Assert.Equal( text, result.Text );
        Assert.Equal( content.Length, result.ByteCount );
        Assert.True( result.DetectionConfidence >= 0.35 );
    }

    [Fact]
    public void Inspectはヌルバイトを含むデータをバイナリと判定する() {
        // Arrange
        var content = Enumerable.Repeat( (byte)0x00, 32 )
            .Concat( new byte[] { 0xFF, 0xFE, 0x00, 0x01 } )
            .Concat( Enumerable.Repeat( (byte)0x00, 32 ) )
            .ToArray();

        // Act
        var result = sut.Inspect( content );

        // Assert
        Assert.True( result.IsBinary );
        Assert.Null( result.Encoding );
        Assert.Null( result.Text );
        Assert.Equal( content.Length, result.ByteCount );
    }

    [Fact]
    public void InspectはUtf16LEのBOMを優先してデコードする() {
        // Arrange
        const string text = "abc漢";
        var enc = new UnicodeEncoding(bigEndian:false, byteOrderMark:true);
        //content = [255, 254, 97, 0, 98, 0, 99, 0, 34, 111];
        var content = enc.GetPreamble().Concat(enc.GetBytes(text)).ToArray();

        // Act
        var result = sut.Inspect(content);

        // Assert
        Assert.False( result.IsBinary );
        Assert.Equal( Encoding.Unicode.WebName, result.Encoding?.WebName );
        Assert.Equal( text, result.Text?.TrimStart( '\uFEFF' ) );   // BOMが残る
        Assert.Equal( content.Length, result.ByteCount );
        Assert.Equal( 1.0, result.DetectionConfidence );
    }

    [Fact]
    public void InspectはUtf16BEのBOMを優先してデコードする() {
        // Arrange
        const string text = "def漢";
        var enc = new UnicodeEncoding(bigEndian:true, byteOrderMark:true);
        var content = enc.GetPreamble().Concat(enc.GetBytes(text)).ToArray();

        // Act
        var result = sut.Inspect(content);

        // Assert
        Assert.False( result.IsBinary );
        Assert.Equal( Encoding.BigEndianUnicode.WebName, result.Encoding?.WebName );
        Assert.Equal( text, result.Text?.TrimStart( '\uFEFF' ) );   // BOMが残る
        Assert.Equal( content.Length, result.ByteCount );
        Assert.Equal( 1.0, result.DetectionConfidence );
    }

    [Fact]
    public void InspectはUtf32LEのBOMを含む配列をバイナリと判定する() {
        // Arrange
        const string text = "XYZ漢";
        var enc = new UTF32Encoding(bigEndian:false, byteOrderMark:true);
        var content = enc.GetPreamble().Concat(enc.GetBytes(text)).ToArray();

        // Act
        var result = sut.Inspect(content);

        // Assert
        Assert.True( result.IsBinary );
        Assert.Null( result.Encoding );
        Assert.Null( result.Text );
        Assert.Equal( content.Length, result.ByteCount );
        Assert.Equal( 1.0, result.DetectionConfidence );
    }

    [Fact]
    public void InspectはUtf32BEのBOMを含む配列をバイナリと判定する() {
        // Arrange
        const string text = "JKL漢";
        var enc = new UTF32Encoding(bigEndian:true, byteOrderMark:true);
        var content = enc.GetPreamble().Concat(enc.GetBytes(text)).ToArray();

        // Act
        var result = sut.Inspect(content);

        // Assert
        Assert.True( result.IsBinary );
        Assert.Null( result.Encoding );
        Assert.Null( result.Text );
        Assert.Equal( content.Length, result.ByteCount );
        Assert.Equal( 1.0, result.DetectionConfidence );
    }

    [Fact]
    public void InspectはBOMなしでもUtf16LEらしい配列をUtf16LEとして解釈する() {
        // Arrange: a\0b\0c\0d\0e\0f\0
        var bytes = new byte[] { 0x61,0x00, 0x62,0x00, 0x63,0x00, 0x64,0x00, 0x65,0x00, 0x66,0x00 };
        const string expected = "abcdef";

        // Act
        var result = sut.Inspect(bytes);

        // Assert
        Assert.False( result.IsBinary );
        Assert.Equal( Encoding.Unicode.WebName, result.Encoding?.WebName );
        Assert.Equal( expected, result.Text );
        Assert.Equal( bytes.Length, result.ByteCount );
        Assert.True( result.DetectionConfidence >= 0.35 );
    }

    [Fact]
    public void InspectはBOMなしでもUtf16BEらしい配列をUtf16BEとして解釈する() {
        // Arrange: \0a\0b\0c\0d\0e\0f
        var bytes = new byte[] { 0x00,0x61, 0x00,0x62, 0x00,0x63, 0x00,0x64, 0x00,0x65, 0x00,0x66 };
        const string expected = "abcdef";

        // Act
        var result = sut.Inspect(bytes);

        // Assert
        Assert.False( result.IsBinary );
        Assert.Equal( Encoding.BigEndianUnicode.WebName, result.Encoding?.WebName );
        Assert.Equal( expected, result.Text );
        Assert.Equal( bytes.Length, result.ByteCount );
        Assert.True( result.DetectionConfidence >= 0.35 );
    }

    [Fact]
    public void Inspectは明らかなテキストUTF8をテキストと判定する() {
        // Arrange
        const string text = "日本語テキスト_123";
        var bytes = Encoding.UTF8.GetBytes(text);

        // Act
        var result = sut.Inspect(bytes);

        // Assert
        Assert.False( result.IsBinary );
        Assert.NotNull( result.Text );
        Assert.Equal( text, result.Text );
        Assert.Equal( bytes.Length, result.ByteCount );
    }

    [Fact]
    public void Inspectは強いバイナリらしさを持つ配列をバイナリと判定する() {
        // Arrange: 全て NUL で scoreが 1.0 に十分近い
        var bytes = Enumerable.Repeat((byte)0x00, 256).ToArray();

        // Act
        var result = sut.Inspect(bytes);

        // Assert
        Assert.True( result.IsBinary );
        Assert.Null( result.Encoding );
        Assert.Null( result.Text );
        Assert.Equal( bytes.Length, result.ByteCount );
    }
}