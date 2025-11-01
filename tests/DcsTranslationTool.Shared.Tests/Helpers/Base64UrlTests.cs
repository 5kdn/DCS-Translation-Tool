using System.Text;

using DcsTranslationTool.Shared.Helpers;

namespace DcsTranslationTool.Shared.Tests.Helpers;

/// <summary>
/// Base64Urlヘルパーの挙動を検証するテストを提供する。
/// </summary>
public sealed class Base64UrlTests {
    #region Encode

    [Theory]
    [InlineData( "f", "Zg" )]
    [InlineData( "fo", "Zm8" )]
    [InlineData( "foo", "Zm9v" )]
    [InlineData( "foobar", "Zm9vYmFy" )]
    public void Encodeは文字列をエンコードしたときにBase64URL形式の文字列が返る( string plain, string expected ) {
        // Arrange
        var bytes = Encoding.ASCII.GetBytes( plain );

        // Act
        var actual = Base64Url.Encode( bytes );

        // Assert
        Assert.Equal( expected, actual );
    }

    [Fact]
    public void Encodeは空配列を空文字列に変換する() {
        // Arrange

        // Act
        var result = Base64Url.Encode([]);

        // Assert
        Assert.Equal( string.Empty, result );
    }

    [Theory]
    [InlineData( "f" )]
    [InlineData( "fo" )]
    [InlineData( "foo" )]
    [InlineData( "foobar" )]
    [InlineData( "日本語" )]
    public void Encodeの結果にはプラスやスラッシュやパディングが含まれない( string plain ) {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(plain);

        // Act
        var encoded = Base64Url.Encode(bytes);

        // Assert
        Assert.DoesNotContain( "+", encoded );
        Assert.DoesNotContain( "/", encoded );
        Assert.DoesNotContain( "=", encoded );
    }

    #endregion

    #region Decode

    [Fact]
    public void DecodeはURLセーフな文字列をデコードしたときに元のバイト列が得られる() {
        // Arrange
        const string encoded = "--__";
        var expected = new byte[] { 0xFB, 0xEF, 0xFF };

        // Act
        var actual = Base64Url.Decode( encoded );

        // Assert
        Assert.Equal( expected, actual );
    }

    [Fact]
    public void Decodeはパディング無しの入力をデコードしたときに正しいバイト列が得られる() {
        // Arrange
        const string encoded = "Zg";
        var expected = Encoding.ASCII.GetBytes( "f" );

        // Act
        var actual = Base64Url.Decode( encoded );

        // Assert
        Assert.Equal( expected, actual );
    }

    [Fact]
    public void Encodeはnull入力でArgumentNullExceptionを送出する() {
        // Arrange

        // Act & Assert
        Assert.Throws<ArgumentNullException>( () => Base64Url.Encode( null! ) );
    }

    [Fact]
    public void Decodeは空文字列を空配列に変換する() {
        // Arrange

        // Act
        var result = Base64Url.Decode(string.Empty);

        // Assert
        Assert.Empty( result );
    }

    [Fact]
    public void Decodeが不正な文字列を受け取るとFormatExceptionを送出する() {
        // Arrange

        // Act & Assert
        Assert.Throws<FormatException>( () => Base64Url.Decode( "###" ) );
    }

    [Fact]
    public void Decodeは不正な長さの文字列でFormatExceptionを送出する() {
        // Arrange

        // Act & Assert
        Assert.Throws<FormatException>( () => Base64Url.Decode( "Z" ) );
    }

    [Fact]
    public void Decodeは無効文字を含む場合FormatExceptionを送出する() {
        // Arrange

        // Act & Assert
        Assert.Throws<FormatException>( () => Base64Url.Decode( "Zg$" ) );
    }

    #endregion

    #region 双方向
    [Theory]
    [InlineData( "日本語" )]
    [InlineData( "123_+-/" )]
    public void EncodeとDecodeを往復しても元の文字列に戻る( string plain ) {
        // Arrange
        var bytes = Encoding.UTF8.GetBytes(plain);

        // Act
        var encoded = Base64Url.Encode(bytes);
        var decoded = Base64Url.Decode(encoded);

        // Assert
        Assert.Equal( bytes, decoded );
    }

    [Fact]
    public void 長いデータでもEncodeとDecodeが一致する() {
        // Arrange
        var data = Enumerable.Range(0, 10000).Select(i => (byte)(i % 256)).ToArray();

        // Act
        var encoded = Base64Url.Encode(data);
        var decoded = Base64Url.Decode(encoded);

        // Assert
        Assert.Equal( data, decoded );
    }

    #endregion
}