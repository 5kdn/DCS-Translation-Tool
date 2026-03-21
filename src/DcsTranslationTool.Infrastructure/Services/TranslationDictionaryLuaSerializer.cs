using System.Text;

using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// dictionary の Lua テキスト生成を担うヘルパーである。
/// </summary>
internal static class TranslationDictionaryLuaSerializer {
    /// <summary>
    /// dictionary 項目一覧から新規 Lua テキストを生成する。
    /// </summary>
    /// <param name="items">出力対象の dictionary 項目一覧。</param>
    /// <returns>生成した Lua テキスト。</returns>
    internal static string Serialize( IReadOnlyList<TranslationDictionaryItem> items ) {
        ArgumentNullException.ThrowIfNull( items );

        var builder = new StringBuilder();
        builder.Append( "dictionary = {\n" );

        foreach(var item in items) {
            builder.Append( "    [\"" );
            builder.Append( EscapeLuaString( item.Key ) );
            builder.Append( "\"] = \"" );
            builder.Append( EscapeLuaString( item.Translated ) );
            builder.Append( "\",\n" );
        }

        builder.Append( "}\n" );
        return builder.ToString();
    }

    /// <summary>
    /// 元の dictionary 構造を維持した Lua テキストを生成する。
    /// </summary>
    /// <param name="dictionary">元の dictionary 情報。</param>
    /// <param name="translatedByKey">キーごとの翻訳値。</param>
    /// <returns>生成した Lua テキスト。</returns>
    internal static string Serialize( EditableTranslationDictionary dictionary, IReadOnlyDictionary<string, string> translatedByKey ) {
        ArgumentNullException.ThrowIfNull( dictionary );
        ArgumentNullException.ThrowIfNull( translatedByKey );

        var fallbackValues = dictionary.Items.ToDictionary(
            item => item.Key,
            item => item.Original,
            StringComparer.Ordinal );
        var builder = new StringBuilder( dictionary.OriginalText );

        foreach(var valueRange in dictionary.ValueRanges.Values
            .OrderByDescending( item => item.StartIndex )) {
            var replacementValue = translatedByKey.TryGetValue( valueRange.Key, out var translated )
                ? translated
                : fallbackValues[valueRange.Key];
            builder.Remove( valueRange.StartIndex, valueRange.Length );
            builder.Insert( valueRange.StartIndex, EscapeLuaString( replacementValue ) );
        }

        return builder.ToString();
    }

    private static string EscapeLuaString( string value ) {
        if(string.IsNullOrEmpty( value )) {
            return string.Empty;
        }

        var normalizedValue = NormalizeLineEndings( value );
        var lines = normalizedValue.Split( '\n' );
        var builder = new StringBuilder( normalizedValue.Length );

        for(var index = 0; index < lines.Length; index++) {
            AppendEscapedLuaLine( builder, lines[index] );
            if(index < lines.Length - 1) {
                builder.Append( "\\\n" );
            }
        }

        return builder.ToString();
    }

    private static void AppendEscapedLuaLine( StringBuilder builder, string line ) {
        foreach(var current in line) {
            switch(current) {
                case '\\':
                    builder.Append( "\\\\" );
                    break;
                case '"':
                    builder.Append( "\\\"" );
                    break;
                case '\t':
                    builder.Append( "\\t" );
                    break;
                default:
                    builder.Append( current );
                    break;
            }
        }
    }

    private static string NormalizeLineEndings( string value ) => value
        .Replace( "\r\n", "\n", StringComparison.Ordinal )
        .Replace( '\r', '\n' );
}