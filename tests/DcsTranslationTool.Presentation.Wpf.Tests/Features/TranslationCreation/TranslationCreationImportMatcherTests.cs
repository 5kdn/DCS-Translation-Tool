using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// <see cref="TranslationCreationImportMatcher"/> の動作を検証する。
/// </summary>
public sealed class TranslationCreationImportMatcherTests {
    [Fact]
    public void MatchByTranslationPairはCRLF差異を無視して全件一致を判定する() {
        var rows = new[]
        {
            new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "ctx", "line1\r\nline2" ) )
        };
        var entries = new[]
        {
            new TranslationPoEntry( "ctx", "line1\nline2", "translated", true )
        };

        var result = TranslationCreationImportMatcher.MatchByTranslationPair(
            rows,
            entries,
            static row => (row.Key, row.Original),
            static entry => (entry.Context, entry.Original),
            static ( row, entry ) => (row, entry.Translated, entry.IsEnabled) );

        Assert.True( result.IsFullMatch );
        Assert.Single( result.Matches );
        Assert.Equal( "translated", result.Matches[0].Translated );
        Assert.True( result.Matches[0].IsEnabled );
    }

    [Fact]
    public void MatchByNormalizedKeyは重複キーを部分一致として扱う() {
        var rows = new[]
        {
            new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "same", "o1" ) ),
            new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "same", "o2" ) )
        };
        var items = new[]
        {
            new TranslationDictionaryItem( "same", "x" ) { Translated = "translated" }
        };

        var result = TranslationCreationImportMatcher.MatchByNormalizedKey(
            rows,
            items,
            static row => row.Key,
            static item => item.Key,
            static ( row, item ) => (row, item.Translated) );

        Assert.False( result.IsFullMatch );
        Assert.Empty( result.Matches );
    }
}