using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationSession の動作を検証する。
/// </summary>
public sealed class TranslationCreationSessionTests {
    [Fact]
    public void LoadはRowStateを生成して初期状態を設定する() {
        var session = new TranslationCreationSession();

        session.Load( CreateLoadState(
            new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
            new TranslationDictionaryItem( "DictKey_sortie_2", "o2" ) ) );

        Assert.Equal( 2, session.Rows.Count );
        Assert.Equal( "DictKey_sortie_1", session.Rows[0].Key );
        Assert.Equal( "o2", session.Rows[1].Original );
        Assert.True( session.HasLoadedItems );
        Assert.False( session.HasPendingChangesForClose() );
    }

    [Fact]
    public void SelectedRow設定はSelectedTranslatedDraftを同期する() {
        var session = new TranslationCreationSession();
        session.Load( CreateLoadState( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) { Translated = "t1" } ) );

        session.SelectedRow = session.Rows[0];

        Assert.Equal( "o1", session.SelectedOriginal );
        Assert.Equal( "t1", session.SelectedTranslatedDraft );
        Assert.True( session.CanEditSelectedTranslated );
    }

    [Fact]
    public void FlushPendingSelectedTranslatedEditは選択行へDraftを反映する() {
        var session = new TranslationCreationSession();
        session.Load( CreateLoadState( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) ) );
        session.SelectedRow = session.Rows[0];
        session.SelectedTranslatedDraft = "translated";

        session.FlushPendingSelectedTranslatedEdit();

        Assert.Equal( "translated", session.Rows[0].Translated );
        Assert.True( session.HasPendingChangesForClose() );
    }

    [Fact]
    public void HasPendingChangesForCloseはDraft差分を反映前でも検出する() {
        var session = new TranslationCreationSession();
        session.Load( CreateLoadState( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) ) );
        session.SelectedRow = session.Rows[0];
        session.SelectedTranslatedDraft = "translated";

        Assert.True( session.HasPendingChangesForClose() );
        Assert.Equal( string.Empty, session.Rows[0].Translated );
    }

    [Fact]
    public void MoveSelectionは表示中行一覧に対して境界を考慮して移動する() {
        var session = new TranslationCreationSession();
        session.Load( CreateLoadState(
            new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ),
            new TranslationDictionaryItem( "DictKey_sortie_2", "o2" ) ) );
        var visibleRows = session.Rows;

        var movedFirst = session.MoveSelection( visibleRows, 1 );
        var movedSecond = session.MoveSelection( visibleRows, 1 );
        var movedOverflow = session.MoveSelection( visibleRows, 1 );

        Assert.True( movedFirst );
        Assert.True( movedSecond );
        Assert.False( movedOverflow );
        Assert.Equal( "DictKey_sortie_2", session.SelectedRow?.Key );
    }

    [Fact]
    public void CanEditSelectedTranslatedは選択行のIsEnabledに追従する() {
        var session = new TranslationCreationSession();
        session.Load( CreateLoadState( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) ) );
        session.SelectedRow = session.Rows[0];

        session.Rows[0].IsEnabled = false;

        Assert.False( session.CanEditSelectedTranslated );
        Assert.Equal( string.Empty, session.SelectedTranslatedDraft );
    }

    /// <summary>
    /// テスト用の読込状態を生成する。
    /// </summary>
    /// <param name="items">生成対象の dictionary 項目一覧。</param>
    /// <returns>生成した読込状態を返す。</returns>
    private static TranslationCreationDictionaryLoadState CreateLoadState( params TranslationDictionaryItem[] items ) =>
        new(
            [.. items.Select( static item => new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Translated,
                IsEnabled = item.IsEnabled,
            } )],
            [.. items.Select( static item => new TranslationCreationRowState(
                new TranslationDictionaryItem( item.Key, item.Original )
                {
                    Translated = item.Translated,
                    IsEnabled = item.IsEnabled,
                } ) )] );
}