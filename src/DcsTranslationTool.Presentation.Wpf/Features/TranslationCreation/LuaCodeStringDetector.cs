using System.Text;
using System.Text.RegularExpressions;

using MoonSharp.Interpreter;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// 文字列が Lua 5.2 中心互換コードとして解釈可能かどうかを判定する。
/// </summary>
internal static partial class LuaCodeStringDetector {
    /// <summary>
    /// 指定文字列が Lua 5.2 中心互換の文または式として解釈可能かどうかを返す。
    /// </summary>
    /// <param name="value">判定対象文字列。</param>
    /// <returns>Lua コードとして解釈可能な場合は <see langword="true"/>。</returns>
    internal static bool IsLuaCodeString( string value ) {
        if(string.IsNullOrWhiteSpace( value )) {
            return false;
        }

        var normalized = NormalizeLineEndings( value );
        var uncommented = RemoveLuaComments( normalized );
        if(string.IsNullOrWhiteSpace( uncommented )) {
            return false;
        }

        if(CanCompileAsChunk( uncommented )) {
            return true;
        }

        return !IsSimpleLuaExpression( uncommented )
            && CanCompileAsChunk( $"return {uncommented}" );
    }

    private static bool CanCompileAsChunk( string code ) {
        try {
            var script = new Script();
            _ = script.LoadString( code );
            return true;
        }
        catch(SyntaxErrorException) {
            return false;
        }
        catch(InterpreterException) {
            return false;
        }
    }

    private static bool IsSimpleLuaExpression( string value ) {
        var trimmed = value.Trim();
        if(trimmed.Length == 0) {
            return true;
        }

        if(SimpleIdentifierRegex().IsMatch( trimmed )) {
            return true;
        }

        if(SimpleLiteralRegex().IsMatch( trimmed )) {
            return true;
        }

        return IsLongBracketLiteral( trimmed );
    }

    private static string RemoveLuaComments( string value ) {
        var builder = new StringBuilder( value.Length );

        for(var index = 0; index < value.Length; index++) {
            var current = value[index];
            if(current == '\'' || current == '"') {
                AppendQuotedString( builder, value, ref index, current );
                continue;
            }

            if(TryMatchLongBracketStart( value, index, out var longBracketEqualsCount, out var longBracketStartLength )) {
                AppendLongBracket( builder, value, ref index, longBracketEqualsCount, longBracketStartLength );
                continue;
            }

            if(current == '-' && index + 1 < value.Length && value[index + 1] == '-') {
                index += 2;
                if(TryMatchLongBracketStart( value, index, out var commentEqualsCount, out var commentStartLength )) {
                    SkipLongComment( builder, value, ref index, commentEqualsCount, commentStartLength );
                    continue;
                }

                SkipLineComment( builder, value, ref index );
                continue;
            }

            builder.Append( current );
        }

        return builder.ToString();
    }

    private static void AppendQuotedString( StringBuilder builder, string value, ref int index, char quote ) {
        builder.Append( value[index] );

        for(index++; index < value.Length; index++) {
            builder.Append( value[index] );
            if(value[index] == '\\' && index + 1 < value.Length) {
                index++;
                builder.Append( value[index] );
                continue;
            }

            if(value[index] == quote) {
                return;
            }
        }
    }

    private static void AppendLongBracket( StringBuilder builder, string value, ref int index, int equalsCount, int startLength ) {
        for(var offset = 0; offset < startLength; offset++) {
            builder.Append( value[index + offset] );
        }

        index += startLength;
        while(index < value.Length) {
            if(TryMatchLongBracketEnd( value, index, equalsCount, out var endLength )) {
                for(var offset = 0; offset < endLength; offset++) {
                    builder.Append( value[index + offset] );
                }

                index += endLength - 1;
                return;
            }

            builder.Append( value[index] );
            index++;
        }

        index = value.Length;
    }

    private static void SkipLineComment( StringBuilder builder, string value, ref int index ) {
        while(index < value.Length && value[index] != '\n') {
            index++;
        }

        if(index < value.Length && value[index] == '\n') {
            builder.Append( '\n' );
        }
    }

    private static void SkipLongComment( StringBuilder builder, string value, ref int index, int equalsCount, int startLength ) {
        index += startLength;
        while(index < value.Length) {
            if(TryMatchLongBracketEnd( value, index, equalsCount, out var endLength )) {
                index += endLength - 1;
                return;
            }

            if(value[index] == '\n') {
                builder.Append( '\n' );
            }

            index++;
        }

        index = value.Length;
    }

    private static bool TryMatchLongBracketStart( string value, int startIndex, out int equalsCount, out int tokenLength ) {
        equalsCount = 0;
        tokenLength = 0;
        if(startIndex >= value.Length || value[startIndex] != '[') {
            return false;
        }

        var index = startIndex + 1;
        while(index < value.Length && value[index] == '=') {
            equalsCount++;
            index++;
        }

        if(index >= value.Length || value[index] != '[') {
            return false;
        }

        tokenLength = index - startIndex + 1;
        return true;
    }

    private static bool TryMatchLongBracketEnd( string value, int startIndex, int equalsCount, out int tokenLength ) {
        tokenLength = 0;
        if(startIndex >= value.Length || value[startIndex] != ']') {
            return false;
        }

        var index = startIndex + 1;
        for(var equalsIndex = 0; equalsIndex < equalsCount; equalsIndex++) {
            if(index >= value.Length || value[index] != '=') {
                return false;
            }

            index++;
        }

        if(index >= value.Length || value[index] != ']') {
            return false;
        }

        tokenLength = index - startIndex + 1;
        return true;
    }

    private static bool IsLongBracketLiteral( string value ) {
        if(!TryMatchLongBracketStart( value, 0, out var equalsCount, out var startLength )) {
            return false;
        }

        var endIndex = value.Length - (equalsCount + 2);
        return endIndex >= startLength
            && TryMatchLongBracketEnd( value, endIndex, equalsCount, out var endLength )
            && endIndex + endLength == value.Length;
    }

    private static string NormalizeLineEndings( string value ) => value
        .Replace( "\r\n", "\n", StringComparison.Ordinal )
        .Replace( '\r', '\n' );

    [GeneratedRegex( @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant )]
    private static partial Regex SimpleIdentifierRegex();

    [GeneratedRegex( "^(?:true|false|nil|(?:0[xX][0-9A-Fa-f]+|\\d+(?:\\.\\d+)?(?:[eE][+-]?\\d+)?)|(?:\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'))$", RegexOptions.CultureInvariant )]
    private static partial Regex SimpleLiteralRegex();
}