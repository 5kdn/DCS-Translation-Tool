namespace DcsTranslationTool.Shared.Helpers;

/// <summary>
/// Base64URL 形式のエンコードとデコードを提供するヘルパー
/// </summary>
public static class Base64Url {
    /// <summary>
    /// バイト列を Base64URL 形式でエンコードする
    /// </summary>
    /// <param name="data">エンコード対象のバイト列</param>
    /// <returns>Base64URL 形式の文字列</returns>
    public static string Encode( byte[] data ) =>
        Convert.ToBase64String( data )
        .TrimEnd( '=' )
        .Replace( '+', '-' )
        .Replace( '/', '_' );

    /// <summary>
    /// Base64URL 形式の文字列を元のバイト列に変換する
    /// </summary>
    /// <param name="input">デコード対象の文字列</param>
    /// <returns>変換したバイト列</returns>
    public static byte[] Decode( string input ) {
        var padded = input
            .Replace( '-', '+' )
            .Replace( '_', '/' );
        switch(padded.Length % 4) {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
        }
        return Convert.FromBase64String( padded );
    }

}