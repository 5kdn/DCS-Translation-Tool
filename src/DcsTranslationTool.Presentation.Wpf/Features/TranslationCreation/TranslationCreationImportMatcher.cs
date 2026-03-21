namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の import 一致判定を担う。
/// </summary>
internal static class TranslationCreationImportMatcher {
    /// <summary>
    /// context と original の組み合わせで一致判定する。
    /// </summary>
    /// <typeparam name="TSource">取り込み元項目型。</typeparam>
    /// <typeparam name="TMatch">一致結果型。</typeparam>
    /// <param name="rows">画面上の行一覧。</param>
    /// <param name="sources">取り込み元一覧。</param>
    /// <param name="rowKeySelector">行側キー選択関数。</param>
    /// <param name="sourceKeySelector">取り込み元側キー選択関数。</param>
    /// <param name="matchFactory">一致結果生成関数。</param>
    /// <returns>一致判定結果を返す。</returns>
    internal static TranslationCreationImportAnalysis<TMatch> MatchByTranslationPair<TSource, TMatch>(
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TSource> sources,
        Func<TranslationDictionaryItemRowViewModel, (string Context, string Original)> rowKeySelector,
        Func<TSource, (string Context, string Original)> sourceKeySelector,
        Func<TranslationDictionaryItemRowViewModel, TSource, TMatch> matchFactory ) {
        var rowGroups = rows
            .GroupBy( row => NormalizeTranslationPair( rowKeySelector( row ) ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var sourceGroups = sources
            .GroupBy( source => NormalizeTranslationPair( sourceKeySelector( source ) ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        return Match( rows.Count, sources.Count, rowGroups, sourceGroups, matchFactory );
    }

    /// <summary>
    /// 正規化した key で一致判定する。
    /// </summary>
    /// <typeparam name="TSource">取り込み元項目型。</typeparam>
    /// <typeparam name="TMatch">一致結果型。</typeparam>
    /// <param name="rows">画面上の行一覧。</param>
    /// <param name="sources">取り込み元一覧。</param>
    /// <param name="rowKeySelector">行側キー選択関数。</param>
    /// <param name="sourceKeySelector">取り込み元側キー選択関数。</param>
    /// <param name="matchFactory">一致結果生成関数。</param>
    /// <returns>一致判定結果を返す。</returns>
    internal static TranslationCreationImportAnalysis<TMatch> MatchByNormalizedKey<TSource, TMatch>(
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TSource> sources,
        Func<TranslationDictionaryItemRowViewModel, string> rowKeySelector,
        Func<TSource, string> sourceKeySelector,
        Func<TranslationDictionaryItemRowViewModel, TSource, TMatch> matchFactory ) {
        var rowGroups = rows
            .GroupBy( row => NormalizeTranslationPairValue( rowKeySelector( row ) ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var sourceGroups = sources
            .GroupBy( source => NormalizeTranslationPairValue( sourceKeySelector( source ) ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        return Match( rows.Count, sources.Count, rowGroups, sourceGroups, matchFactory );
    }

    private static TranslationCreationImportAnalysis<TMatch> Match<TKey, TSource, TMatch>(
        int rowCount,
        int sourceCount,
        IReadOnlyDictionary<TKey, TranslationDictionaryItemRowViewModel[]> rowGroups,
        IReadOnlyDictionary<TKey, TSource[]> sourceGroups,
        Func<TranslationDictionaryItemRowViewModel, TSource, TMatch> matchFactory ) where TKey : notnull {
        var matches = rowGroups.Keys
            .Intersect( sourceGroups.Keys )
            .Where( key => rowGroups[key].Length == 1 && sourceGroups[key].Length == 1 )
            .Select( key => matchFactory( rowGroups[key][0], sourceGroups[key][0] ) )
            .ToArray();
        var isFullMatch =
            rowCount == sourceCount
            && rowGroups.Count == rowCount
            && sourceGroups.Count == sourceCount
            && matches.Length == rowCount;
        return new TranslationCreationImportAnalysis<TMatch>( isFullMatch, matches );
    }

    private static (string Context, string Original) NormalizeTranslationPair( (string Context, string Original) pair ) => (
        NormalizeTranslationPairValue( pair.Context ),
        NormalizeTranslationPairValue( pair.Original ));

    private static string NormalizeTranslationPairValue( string value ) => value
        .Replace( "\r\n", "\n", StringComparison.Ordinal )
        .Replace( '\r', '\n' );
}

/// <summary>
/// TranslationCreation の import 一致判定結果を表す。
/// </summary>
/// <typeparam name="TMatch">一致結果型。</typeparam>
/// <param name="IsFullMatch">全件一致したかどうか。</param>
/// <param name="Matches">一致した項目一覧。</param>
internal sealed record TranslationCreationImportAnalysis<TMatch>( bool IsFullMatch, IReadOnlyList<TMatch> Matches );