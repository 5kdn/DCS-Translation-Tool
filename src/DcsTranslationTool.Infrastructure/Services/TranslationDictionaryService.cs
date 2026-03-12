using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Interfaces;

using FluentResults;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// アーカイブ内 dictionary を読み込むサービスである。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public sealed partial class TranslationDictionaryService( ILoggingService logger ) : ITranslationDictionaryService {
    private const string DictionaryEntryPath = "l10n/default/dictionary";

    /// <inheritdoc />
    public Result<IReadOnlyList<TranslationDictionaryItem>> LoadDictionary( string archiveFullPath ) {
        if(string.IsNullOrWhiteSpace( archiveFullPath )) {
            logger.Warn( "dictionary 読込でアーカイブパスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( "アーカイブ絶対パスが空です", "DICTIONARY_ARCHIVE_REQUIRED" ) );
        }

        if(!File.Exists( archiveFullPath )) {
            logger.Warn( $"dictionary 読込対象のアーカイブが存在しない。Path={archiveFullPath}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {archiveFullPath}", "DICTIONARY_ARCHIVE_NOT_FOUND" ) );
        }

        try {
            using var archive = ZipFile.OpenRead( archiveFullPath );
            var entry = archive.Entries.FirstOrDefault( value =>
                string.Equals( value.FullName, DictionaryEntryPath, StringComparison.OrdinalIgnoreCase ) );

            if(entry is null) {
                logger.Warn( $"dictionary エントリが存在しない。Archive={archiveFullPath}" );
                return Result.Fail( ResultErrorFactory.NotFound( "dictionary エントリが存在しません", "DICTIONARY_ENTRY_NOT_FOUND" ) );
            }

            using var stream = entry.Open();
            using var reader = new StreamReader( stream, Encoding.UTF8, true );
            var luaText = reader.ReadToEnd();
            var items = ParseDictionaryItems( luaText );
            logger.Info( $"dictionary を読み込んだ。Archive={archiveFullPath}, Count={items.Count}" );
            return Result.Ok<IReadOnlyList<TranslationDictionaryItem>>( items );
        }
        catch(InvalidDataException ex) {
            logger.Error( $"dictionary 読込時に zip 構造が不正だった。Archive={archiveFullPath}", ex );
            return Result.Fail( ResultErrorFactory.External( "アーカイブ構造が不正です", "DICTIONARY_ARCHIVE_INVALID", ex ) );
        }
        catch(Exception ex) {
            logger.Error( $"dictionary 読込に失敗した。Archive={archiveFullPath}", ex );
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "DICTIONARY_LOAD_EXCEPTION" ) );
        }
    }

    private static IReadOnlyList<TranslationDictionaryItem> ParseDictionaryItems( string luaText ) {
        var blockMatch = DictionaryBlockRegex().Match( luaText );
        if(!blockMatch.Success) {
            return [];
        }

        var content = blockMatch.Groups["content"].Value;
        Dictionary<string, TranslationDictionaryItem> itemsByKey = new( StringComparer.Ordinal );

        foreach(Match match in DictionaryEntryRegex().Matches( content )) {
            var key = UnescapeLuaString( match.Groups["key"].Value );
            var value = UnescapeLuaString( match.Groups["value"].Value );
            itemsByKey[key] = new TranslationDictionaryItem( key, value );
        }

        return [.. itemsByKey.Values.OrderBy( item => item.Key, StringComparer.Ordinal )];
    }

    [GeneratedRegex( @"dictionary\s*=\s*\{(?<content>[\s\S]*)\}", RegexOptions.CultureInvariant )]
    private static partial Regex DictionaryBlockRegex();

    [GeneratedRegex( @"\[\s*""(?<key>(?:\\(?:\r\n|\r|\n|.)|[^""\\])*)""\s*\]\s*=\s*""(?<value>(?:\\(?:\r\n|\r|\n|.)|[^""\\])*)""", RegexOptions.CultureInvariant )]
    private static partial Regex DictionaryEntryRegex();

    private static string UnescapeLuaString( string value ) {
        if(string.IsNullOrEmpty( value )) {
            return string.Empty;
        }

        var builder = new StringBuilder( value.Length );
        for(var index = 0; index < value.Length; index++) {
            var current = value[index];
            if(current != '\\') {
                builder.Append( current );
                continue;
            }

            if(index + 1 >= value.Length) {
                builder.Append( current );
                continue;
            }

            var next = value[index + 1];
            switch(next) {
                case '\\':
                    builder.Append( '\\' );
                    index++;
                    break;
                case '"':
                    builder.Append( '"' );
                    index++;
                    break;
                case 'n':
                    builder.Append( '\n' );
                    index++;
                    break;
                case 'r':
                    builder.Append( '\r' );
                    index++;
                    break;
                case 't':
                    builder.Append( '\t' );
                    index++;
                    break;
                case '\n':
                    builder.Append( '\n' );
                    index++;
                    break;
                case '\r':
                    builder.Append( '\r' );
                    index++;
                    if(index + 1 < value.Length && value[index + 1] == '\n') {
                        builder.Append( '\n' );
                        index++;
                    }

                    break;
                default:
                    builder.Append( next );
                    index++;
                    break;
            }
        }

        return builder.ToString();
    }
}