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
/// アーカイブ内 dictionary を読み込むサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public sealed partial class TranslationDictionaryService( ILoggingService logger ) : ITranslationDictionaryService {
    private const string DictionaryEntryPath = "l10n/default/dictionary";
    private const string CsvHeaderLine = "Enabled,Key,Original,Translated";
    private const string LegacyCsvHeaderLine = "Key,Original,Translated";
    private const string ObsoletePoPrefix = "#~ ";

    /// <inheritdoc />
    public Result<IReadOnlyList<TranslationDictionaryItem>> LoadDictionary( string archiveFullPath ) {
        var editableResult = LoadEditableDictionary( archiveFullPath );
        return editableResult.IsFailed
            ? Result.Fail( editableResult.Errors )
            : Result.Ok<IReadOnlyList<TranslationDictionaryItem>>( editableResult.Value.Items );
    }

    /// <inheritdoc />
    public Result<EditableTranslationDictionary> LoadEditableDictionary( string archiveFullPath ) {
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
            var luaText = NormalizeLineEndings( reader.ReadToEnd() );
            var editableDictionary = ParseEditableDictionary( luaText );
            logger.Info( $"dictionary を読み込んだ。Archive={archiveFullPath}, Count={editableDictionary.Items.Count}" );
            return Result.Ok( editableDictionary );
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

    /// <inheritdoc />
    public Result<IReadOnlyList<TranslationDictionaryItem>> LoadDictionaryFile( string path ) {
        if(string.IsNullOrWhiteSpace( path )) {
            logger.Warn( "dictionary ファイル読込でファイルパスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( "dictionary ファイルパスが空です", "DICTIONARY_PATH_REQUIRED" ) );
        }

        if(!File.Exists( path )) {
            logger.Warn( $"dictionary ファイル読込対象のファイルが存在しない。Path={path}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {path}", "DICTIONARY_FILE_NOT_FOUND" ) );
        }

        try {
            var luaText = NormalizeLineEndings( File.ReadAllText( path, Encoding.UTF8 ) );
            var editableDictionary = ParseEditableDictionary( luaText );
            var items = editableDictionary.Items
                .Select( item => new TranslationDictionaryItem( item.Key, item.Original )
                {
                    Translated = item.Original
                } )
                .ToArray();
            logger.Info( $"dictionary ファイルを読み込んだ。Path={path}, Count={items.Length}" );
            return Result.Ok<IReadOnlyList<TranslationDictionaryItem>>( items );
        }
        catch(Exception ex) {
            logger.Error( $"dictionary ファイル読込に失敗した。Path={path}", ex );
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "DICTIONARY_FILE_LOAD_EXCEPTION" ) );
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<TranslationPoEntry>> LoadPo( string path ) {
        if(string.IsNullOrWhiteSpace( path )) {
            logger.Warn( "PO 読込でファイルパスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( "PO ファイルパスが空です", "PO_PATH_REQUIRED" ) );
        }

        if(!File.Exists( path )) {
            logger.Warn( $"PO 読込対象のファイルが存在しない。Path={path}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {path}", "PO_FILE_NOT_FOUND" ) );
        }

        try {
            var poText = NormalizeLineEndings( File.ReadAllText( path, Encoding.UTF8 ) );
            var entries = ParsePo( poText );
            logger.Info( $"PO を読み込んだ。Path={path}, Count={entries.Count}" );
            return Result.Ok<IReadOnlyList<TranslationPoEntry>>( entries );
        }
        catch(FormatException ex) {
            logger.Error( $"PO 読込時に形式エラーが発生した。Path={path}", ex );
            return Result.Fail( ResultErrorFactory.Validation( "PO ファイル形式が不正です", "PO_FILE_INVALID" ) );
        }
        catch(Exception ex) {
            logger.Error( $"PO 読込に失敗した。Path={path}", ex );
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "PO_LOAD_EXCEPTION" ) );
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<TranslationCsvEntry>> LoadCsv( string path ) {
        if(string.IsNullOrWhiteSpace( path )) {
            logger.Warn( "CSV 読込でファイルパスが指定されなかった。" );
            return Result.Fail( ResultErrorFactory.Validation( "CSV ファイルパスが空です", "CSV_PATH_REQUIRED" ) );
        }

        if(!File.Exists( path )) {
            logger.Warn( $"CSV 読込対象のファイルが存在しない。Path={path}" );
            return Result.Fail( ResultErrorFactory.NotFound( $"ファイルが存在しません: {path}", "CSV_FILE_NOT_FOUND" ) );
        }

        try {
            var csvText = NormalizeLineEndings( File.ReadAllText( path, Encoding.UTF8 ) );
            var entries = ParseCsv( csvText );
            logger.Info( $"CSV を読み込んだ。Path={path}, Count={entries.Count}" );
            return Result.Ok<IReadOnlyList<TranslationCsvEntry>>( entries );
        }
        catch(FormatException ex) {
            logger.Error( $"CSV 読込時に形式エラーが発生した。Path={path}", ex );
            return Result.Fail( ResultErrorFactory.Validation( "CSV ファイル形式が不正です", "CSV_FILE_INVALID" ) );
        }
        catch(Exception ex) {
            logger.Error( $"CSV 読込に失敗した。Path={path}", ex );
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "CSV_LOAD_EXCEPTION" ) );
        }
    }

    /// <inheritdoc />
    public async Task SaveDictionaryAsync( string path, IReadOnlyList<TranslationDictionaryItem> items, CancellationToken cancellationToken = default ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( path );
        ArgumentNullException.ThrowIfNull( items );

        try {
            var directoryPath = Path.GetDirectoryName( path );
            if(!string.IsNullOrWhiteSpace( directoryPath )) {
                Directory.CreateDirectory( directoryPath );
            }

            var builder = new StringBuilder();
            builder.Append( "dictionary = {\n" );

            foreach(var item in items) {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append( "    [\"" );
                builder.Append( EscapeLuaString( item.Key ) );
                builder.Append( "\"] = \"" );
                builder.Append( EscapeLuaString( item.Translated ) );
                builder.Append( "\",\n" );
            }

            builder.Append( "}\n" );
            await File.WriteAllTextAsync( path, builder.ToString(), Encoding.UTF8, cancellationToken );
            logger.Info( $"dictionary を保存した。Path={path}, Count={items.Count}" );
        }
        catch(OperationCanceledException) {
            logger.Warn( $"dictionary 保存がキャンセルされた。Path={path}" );
            throw;
        }
        catch(Exception ex) {
            logger.Error( $"dictionary 保存に失敗した。Path={path}", ex );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SavePoAsync(
        string path,
        IReadOnlyList<TranslationDictionaryItem> items,
        string projectIdVersion,
        string potCreationDate,
        string poRevisionDate,
        string xGenerator,
        CancellationToken cancellationToken = default ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( path );
        ArgumentNullException.ThrowIfNull( items );
        ArgumentException.ThrowIfNullOrWhiteSpace( projectIdVersion );
        ArgumentException.ThrowIfNullOrWhiteSpace( potCreationDate );
        ArgumentException.ThrowIfNullOrWhiteSpace( poRevisionDate );
        ArgumentException.ThrowIfNullOrWhiteSpace( xGenerator );

        try {
            var directoryPath = Path.GetDirectoryName( path );
            if(!string.IsNullOrWhiteSpace( directoryPath )) {
                Directory.CreateDirectory( directoryPath );
            }

            var builder = new StringBuilder();
            builder.Append( "msgid \"\"\n" );
            builder.Append( "msgstr \"\"\n" );
            builder.Append( $"\"Project-Id-Version: {EscapePoHeaderValue( projectIdVersion )}\\n\"\n" );
            builder.Append( $"\"POT-Creation-Date: {EscapePoHeaderValue( potCreationDate )}\\n\"\n" );
            builder.Append( $"\"PO-Revision-Date: {EscapePoHeaderValue( poRevisionDate )}\\n\"\n" );
            builder.Append( "\"Language: ja_JP\\n\"\n" );
            builder.Append( "\"MIME-Version: 1.0\\n\"\n" );
            builder.Append( "\"Content-Type: text/plain; charset=UTF-8\\n\"\n" );
            builder.Append( "\"Content-Transfer-Encoding: 8bit\\n\"\n" );
            builder.Append( $"\"X-Generator: {EscapePoHeaderValue( xGenerator )}\\n\"\n\n" );

            foreach(var item in items) {
                cancellationToken.ThrowIfCancellationRequested();
                AppendPoEntry( builder, item );
            }

            await File.WriteAllTextAsync( path, builder.ToString(), Encoding.UTF8, cancellationToken );
            logger.Info( $"PO を保存した。Path={path}, Count={items.Count}" );
        }
        catch(OperationCanceledException) {
            logger.Warn( $"PO 保存がキャンセルされた。Path={path}" );
            throw;
        }
        catch(Exception ex) {
            logger.Error( $"PO 保存に失敗した。Path={path}", ex );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveCsvAsync( string path, IReadOnlyList<TranslationDictionaryItem> items, CancellationToken cancellationToken = default ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( path );
        ArgumentNullException.ThrowIfNull( items );

        try {
            var directoryPath = Path.GetDirectoryName( path );
            if(!string.IsNullOrWhiteSpace( directoryPath )) {
                Directory.CreateDirectory( directoryPath );
            }

            var builder = new StringBuilder();
            builder.Append( CsvHeaderLine );
            builder.Append( '\n' );

            foreach(var item in items) {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append( EscapeCsvField( item.IsEnabled ? "true" : "false" ) );
                builder.Append( ',' );
                builder.Append( EscapeCsvField( item.Key ) );
                builder.Append( ',' );
                builder.Append( EscapeCsvField( item.Original ) );
                builder.Append( ',' );
                builder.Append( EscapeCsvField( item.Translated ) );
                builder.Append( '\n' );
            }

            await File.WriteAllTextAsync( path, builder.ToString(), Encoding.UTF8, cancellationToken );
            logger.Info( $"CSV を保存した。Path={path}, Count={items.Count}" );
        }
        catch(OperationCanceledException) {
            logger.Warn( $"CSV 保存がキャンセルされた。Path={path}" );
            throw;
        }
        catch(Exception ex) {
            logger.Error( $"CSV 保存に失敗した。Path={path}", ex );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SaveDictionaryAsync(
        string path,
        EditableTranslationDictionary dictionary,
        IReadOnlyDictionary<string, string> translatedByKey,
        CancellationToken cancellationToken = default ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( path );
        ArgumentNullException.ThrowIfNull( dictionary );
        ArgumentNullException.ThrowIfNull( translatedByKey );

        try {
            var directoryPath = Path.GetDirectoryName( path );
            if(!string.IsNullOrWhiteSpace( directoryPath )) {
                Directory.CreateDirectory( directoryPath );
            }

            var fallbackValues = dictionary.Items.ToDictionary(
                item => item.Key,
                item => item.Original,
                StringComparer.Ordinal );
            var builder = new StringBuilder( dictionary.OriginalText );

            foreach(var valueRange in dictionary.ValueRanges.Values
                .OrderByDescending( item => item.StartIndex )) {
                cancellationToken.ThrowIfCancellationRequested();
                var replacementValue = translatedByKey.TryGetValue( valueRange.Key, out var translated )
                    ? translated
                    : fallbackValues[valueRange.Key];
                builder.Remove( valueRange.StartIndex, valueRange.Length );
                builder.Insert( valueRange.StartIndex, EscapeLuaString( replacementValue ) );
            }

            await File.WriteAllTextAsync( path, builder.ToString(), Encoding.UTF8, cancellationToken );
            logger.Info( $"dictionary を保存した。Path={path}, Count={dictionary.Items.Count}" );
        }
        catch(OperationCanceledException) {
            logger.Warn( $"dictionary 保存がキャンセルされた。Path={path}" );
            throw;
        }
        catch(Exception ex) {
            logger.Error( $"dictionary 保存に失敗した。Path={path}", ex );
            throw;
        }
    }

    private static EditableTranslationDictionary ParseEditableDictionary( string luaText ) {
        var blockMatch = DictionaryBlockRegex().Match( luaText );
        if(!blockMatch.Success) {
            return new EditableTranslationDictionary( luaText, [], new Dictionary<string, TranslationDictionaryValueRange>( StringComparer.Ordinal ) );
        }

        var content = blockMatch.Groups["content"].Value;
        var contentStartIndex = blockMatch.Groups["content"].Index;
        Dictionary<string, TranslationDictionaryItem> itemsByKey = new( StringComparer.Ordinal );
        Dictionary<string, TranslationDictionaryValueRange> valueRangesByKey = new( StringComparer.Ordinal );

        foreach(Match match in DictionaryEntryRegex().Matches( content )) {
            if(IsCommentedOutLine( content, match.Index )) {
                continue;
            }

            var key = UnescapeLuaString( match.Groups["key"].Value );
            var value = UnescapeLuaString( match.Groups["value"].Value );
            itemsByKey[key] = new TranslationDictionaryItem( key, value );
            valueRangesByKey[key] = new TranslationDictionaryValueRange(
                key,
                contentStartIndex + match.Groups["value"].Index,
                match.Groups["value"].Length );
        }

        return new EditableTranslationDictionary(
            luaText,
            [.. itemsByKey.Values.OrderBy( item => item.Key, StringComparer.Ordinal )],
            valueRangesByKey );
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

        return NormalizeLineEndings( builder.ToString() );
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

    private static string EscapePoString( string value ) {
        if(string.IsNullOrEmpty( value )) {
            return "\"\"";
        }

        var normalizedValue = NormalizeLineEndings( value );
        if(!normalizedValue.Contains( '\n' )) {
            return $"\"{EscapePoSegment( normalizedValue )}\"";
        }

        var builder = new StringBuilder();
        builder.Append( "\"\"\n" );

        var segmentStartIndex = 0;
        for(var index = 0; index < normalizedValue.Length; index++) {
            if(normalizedValue[index] != '\n') {
                continue;
            }

            var lineWithNewline = normalizedValue[segmentStartIndex..(index + 1)];
            builder.Append( '"' );
            builder.Append( EscapePoSegment( lineWithNewline ) );
            builder.Append( "\"\n" );
            segmentStartIndex = index + 1;
        }

        if(segmentStartIndex < normalizedValue.Length) {
            builder.Append( '"' );
            builder.Append( EscapePoSegment( normalizedValue[segmentStartIndex..] ) );
            builder.Append( "\"\n" );
        }

        return builder.ToString().TrimEnd( '\n' );
    }

    private static string EscapePoSegment( string value ) {
        var builder = new StringBuilder( value.Length );

        foreach(var current in value) {
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
                case '\n':
                    builder.Append( "\\n" );
                    break;
                default:
                    builder.Append( current );
                    break;
            }
        }

        return builder.ToString();
    }

    private static string EscapePoHeaderValue( string value ) => value
        .Replace( "\\", "\\\\", StringComparison.Ordinal )
        .Replace( "\"", "\\\"", StringComparison.Ordinal );

    private static List<TranslationCsvEntry> ParseCsv( string csvText ) {
        var records = ParseCsvRecords( csvText );
        if(records.Count == 0) {
            throw new FormatException( "CSV ヘッダーが存在しない。" );
        }

        var hasEnabledColumn = records[0].Count == 4
            && records[0][0] == "Enabled"
            && records[0][1] == "Key"
            && records[0][2] == "Original"
            && records[0][3] == "Translated";
        var isLegacyFormat = records[0].Count == 3
            && records[0][0] == "Key"
            && records[0][1] == "Original"
            && records[0][2] == "Translated";

        if(!hasEnabledColumn && !isLegacyFormat) {
            throw new FormatException( "CSV ヘッダーが不正である。" );
        }

        List<TranslationCsvEntry> entries = [];
        foreach(var record in records.Skip( 1 )) {
            if(record.Count == 1 && record[0].Length == 0) {
                continue;
            }

            if(hasEnabledColumn) {
                if(record.Count != 4) {
                    throw new FormatException( "CSV 列数が不正である。" );
                }

                entries.Add( new TranslationCsvEntry( record[1], record[2], record[3], ParseEnabledValue( record[0] ) ) );
                continue;
            }

            if(record.Count != 3) {
                throw new FormatException( "CSV 列数が不正である。" );
            }

            entries.Add( new TranslationCsvEntry( record[0], record[1], record[2] ) );
        }

        return entries;
    }

    private static List<List<string>> ParseCsvRecords( string csvText ) {
        List<List<string>> records = [];
        List<string> currentRecord = [];
        var fieldBuilder = new StringBuilder();
        var inQuotes = false;

        for(var index = 0; index < csvText.Length; index++) {
            var current = csvText[index];
            if(inQuotes) {
                if(current == '"') {
                    if(index + 1 < csvText.Length && csvText[index + 1] == '"') {
                        fieldBuilder.Append( '"' );
                        index++;
                        continue;
                    }

                    inQuotes = false;
                    continue;
                }

                fieldBuilder.Append( current );
                continue;
            }

            switch(current) {
                case '"':
                    if(fieldBuilder.Length != 0) {
                        throw new FormatException( "CSV クオートの位置が不正である。" );
                    }

                    inQuotes = true;
                    break;
                case ',':
                    currentRecord.Add( fieldBuilder.ToString() );
                    fieldBuilder.Clear();
                    break;
                case '\n':
                    currentRecord.Add( fieldBuilder.ToString() );
                    fieldBuilder.Clear();
                    records.Add( currentRecord );
                    currentRecord = [];
                    break;
                default:
                    fieldBuilder.Append( current );
                    break;
            }
        }

        if(inQuotes) {
            throw new FormatException( "CSV クオートが閉じていない。" );
        }

        if(fieldBuilder.Length > 0 || currentRecord.Count > 0) {
            currentRecord.Add( fieldBuilder.ToString() );
            records.Add( currentRecord );
        }

        return records;
    }

    private static List<TranslationPoEntry> ParsePo( string poText ) {
        var lines = NormalizeLineEndings( poText ).Split( '\n' );
        List<TranslationPoEntry> entries = [];

        for(var index = 0; index < lines.Length;) {
            SkipIgnorablePoLines( lines, ref index );
            if(index >= lines.Length) {
                break;
            }

            var isEnabled = !IsObsoletePoLine( lines[index] );
            string? context = null;
            if(IsPoDirectiveLine( lines[index], "msgctxt", isEnabled )) {
                context = ParsePoDirective( lines, ref index, "msgctxt", isEnabled );
            }

            SkipIgnorablePoLines( lines, ref index );
            if(index >= lines.Length || !IsPoDirectiveLine( lines[index], "msgid", isEnabled )) {
                throw new FormatException( "msgid が見つからなかった。" );
            }

            var original = ParsePoDirective( lines, ref index, "msgid", isEnabled );

            SkipIgnorablePoLines( lines, ref index );
            if(index >= lines.Length || !IsPoDirectiveLine( lines[index], "msgstr", isEnabled )) {
                throw new FormatException( "msgstr が見つからなかった。" );
            }

            var translated = ParsePoDirective( lines, ref index, "msgstr", isEnabled );

            if(context is null) {
                if(original.Length == 0) {
                    continue;
                }

                throw new FormatException( "msgctxt が見つからなかった。" );
            }

            entries.Add( new TranslationPoEntry( context, original, translated, isEnabled ) );
        }

        return entries;
    }

    private static void SkipIgnorablePoLines( string[] lines, ref int index ) {
        while(index < lines.Length) {
            var trimmedLine = lines[index].Trim();
            if(trimmedLine.Length == 0) {
                index++;
                continue;
            }

            if(trimmedLine.StartsWith( ObsoletePoPrefix, StringComparison.Ordinal )) {
                break;
            }

            if(trimmedLine.StartsWith( '#' )) {
                index++;
                continue;
            }

            break;
        }
    }

    private static bool IsPoDirectiveLine( string line, string directive, bool isEnabled ) {
        var trimmedLine = GetPoDirectiveCandidate( line, isEnabled );
        return trimmedLine.StartsWith( $"{directive} ", StringComparison.Ordinal );
    }

    private static string ParsePoDirective( string[] lines, ref int index, string directive, bool isEnabled ) {
        var trimmedLine = GetPoDirectiveCandidate( lines[index], isEnabled );
        if(!trimmedLine.StartsWith( $"{directive} ", StringComparison.Ordinal )) {
            throw new FormatException( $"{directive} が見つからなかった。" );
        }

        var builder = new StringBuilder();
        builder.Append( UnescapePoQuotedText( trimmedLine[(directive.Length + 1)..] ) );
        index++;

        while(index < lines.Length) {
            var continuationLine = GetPoContinuationCandidate( lines[index], isEnabled );
            if(!continuationLine.StartsWith( '"' )) {
                break;
            }

            builder.Append( UnescapePoQuotedText( continuationLine ) );
            index++;
        }

        return NormalizeLineEndings( builder.ToString() );
    }

    private static string UnescapePoQuotedText( string value ) {
        if(value.Length < 2 || value[0] != '"' || value[^1] != '"') {
            throw new FormatException( "PO 文字列が引用符で囲まれていない。" );
        }

        return UnescapePoSegment( value[1..^1] );
    }

    private static string UnescapePoSegment( string value ) {
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
                default:
                    builder.Append( next );
                    index++;
                    break;
            }
        }

        return NormalizeLineEndings( builder.ToString() );
    }

    private static string EscapeCsvField( string value ) {
        var normalizedValue = NormalizeLineEndings( value );
        if(!normalizedValue.Contains( ',' ) && !normalizedValue.Contains( '"' ) && !normalizedValue.Contains( '\n' )) {
            return normalizedValue;
        }

        return $"\"{normalizedValue.Replace( "\"", "\"\"", StringComparison.Ordinal )}\"";
    }

    private static string NormalizeLineEndings( string value ) => value
        .Replace( "\r\n", "\n", StringComparison.Ordinal )
        .Replace( '\r', '\n' );

    private static bool IsCommentedOutLine( string content, int matchIndex ) {
        var lineStartIndex = content.LastIndexOf( '\n', Math.Max( 0, matchIndex - 1 ) );
        lineStartIndex = lineStartIndex < 0 ? 0 : lineStartIndex + 1;
        var linePrefix = content[lineStartIndex..matchIndex];
        return linePrefix.TrimStart().StartsWith( "--", StringComparison.Ordinal );
    }

    private static void AppendPoEntry( StringBuilder builder, TranslationDictionaryItem item ) {
        if(item.IsEnabled) {
            builder.Append( "#, no-wrap\n" );
            builder.Append( "msgctxt " );
            builder.Append( EscapePoString( item.Key ) );
            builder.Append( '\n' );
            builder.Append( "msgid " );
            builder.Append( EscapePoString( item.Original ) );
            builder.Append( '\n' );
            builder.Append( "msgstr " );
            builder.Append( EscapePoString( item.Translated ) );
            builder.Append( "\n\n" );
            return;
        }

        builder.Append( "#~ msgctxt " );
        builder.Append( PrefixPoObsoleteContinuation( EscapePoString( item.Key ) ) );
        builder.Append( '\n' );
        builder.Append( "#~ msgid " );
        builder.Append( PrefixPoObsoleteContinuation( EscapePoString( item.Original ) ) );
        builder.Append( '\n' );
        builder.Append( "#~ msgstr " );
        builder.Append( PrefixPoObsoleteContinuation( EscapePoString( item.Translated ) ) );
        builder.Append( "\n\n" );
    }

    private static string PrefixPoObsoleteContinuation( string value ) =>
        value.Replace( "\n", "\n#~ ", StringComparison.Ordinal );

    private static bool ParseEnabledValue( string value ) =>
        value switch
        {
            "true" => true,
            "false" => false,
            _ => throw new FormatException( "Enabled 列の値が不正である。" )
        };

    private static bool IsObsoletePoLine( string line ) =>
        line.TrimStart().StartsWith( ObsoletePoPrefix, StringComparison.Ordinal );

    private static string GetPoDirectiveCandidate( string line, bool isEnabled ) {
        var trimmedLine = line.TrimStart();
        if(isEnabled) {
            return trimmedLine;
        }

        if(!trimmedLine.StartsWith( ObsoletePoPrefix, StringComparison.Ordinal )) {
            throw new FormatException( "PO コメントアウト行の形式が不正である。" );
        }

        return trimmedLine[ObsoletePoPrefix.Length..];
    }

    private static string GetPoContinuationCandidate( string line, bool isEnabled ) {
        var trimmedLine = line.TrimStart();
        if(isEnabled) {
            return trimmedLine;
        }

        return trimmedLine.StartsWith( ObsoletePoPrefix, StringComparison.Ordinal )
            ? trimmedLine[ObsoletePoPrefix.Length..]
            : trimmedLine;
    }
}