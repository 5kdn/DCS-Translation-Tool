using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;

using FluentResults;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の dictionary 読込前処理を担う。
/// </summary>
/// <param name="translationDictionaryService">dictionary 読込サービス。</param>
internal sealed class TranslationCreationDictionaryLoader(
    ITranslationDictionaryService translationDictionaryService ) {
    /// <summary>
    /// アーカイブから TranslationCreation 用の dictionary 状態を読み込む。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>読込状態を返す。</returns>
    internal Result<TranslationCreationArchiveDictionaryLoadState> LoadArchiveDictionaryState( string archiveFullPath ) {
        var result = translationDictionaryService.LoadArchiveDictionaries( archiveFullPath );
        if(result.IsFailed) {
            return Result.Fail<TranslationCreationArchiveDictionaryLoadState>( result.Errors );
        }

        return Result.Ok( new TranslationCreationArchiveDictionaryLoadState(
            BuildDictionaryLoadState( result.Value.DefaultDictionaryItems ),
            result.Value.HasJapaneseDictionary,
            [.. result.Value.JapaneseDictionaryItems.Select( CloneDictionaryItem )] ) );
    }

    /// <summary>
    /// JP dictionary 項目一覧から初期取り込み用項目一覧を生成する。
    /// </summary>
    /// <param name="japaneseDictionaryItems">JP dictionary 項目一覧。</param>
    /// <returns>取り込み用項目一覧を返す。</returns>
    internal static IReadOnlyList<TranslationDictionaryItem> CreateJapaneseImportSourceItems( IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems ) =>
        [.. japaneseDictionaryItems.Select( item => new TranslationDictionaryItem( item.Key, item.Original )
        {
            Translated = item.Original
        } )];

    private static TranslationCreationDictionaryLoadState BuildDictionaryLoadState( IReadOnlyList<TranslationDictionaryItem> items ) {
        var initializedItems = items
            .Select( InitializeDictionaryItem )
            .OrderBy( GetDictionaryItemSortOrder )
            .ThenBy( item => item.Key, TranslationCreationNaturalKeyComparer.Instance )
            .ToArray();

        List<TranslationDictionaryItem> loadedItems = new( initializedItems.Length );
        List<TranslationDictionaryItemRowViewModel> rowItems = new( initializedItems.Length );
        foreach(var item in initializedItems) {
            var isPossibleNonTranslationTarget = IsPossibleNonTranslationTarget( item );
            loadedItems.Add( new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Translated,
                IsEnabled = item.IsEnabled
            } );
            rowItems.Add( new TranslationDictionaryItemRowViewModel( item, isPossibleNonTranslationTarget ) );
        }

        return new TranslationCreationDictionaryLoadState( loadedItems, rowItems );
    }

    private static TranslationDictionaryItem InitializeDictionaryItem( TranslationDictionaryItem item ) =>
        new( item.Key, item.Original )
        {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled && !IsPossibleNonTranslationTarget( item )
        };

    private static TranslationDictionaryItem CloneDictionaryItem( TranslationDictionaryItem item ) =>
        new( item.Key, item.Original )
        {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled
        };

    private static bool IsPossibleNonTranslationTarget( TranslationDictionaryItem item ) =>
        IsPossibleNonTranslationTarget( item.Key, item.Original );

    private static bool IsPossibleNonTranslationTarget( string key, string original ) {
        if(string.IsNullOrWhiteSpace( original )) {
            return true;
        }

        if(!key.StartsWith( "DictKey_", StringComparison.Ordinal )) {
            return true;
        }

        return key.StartsWith( "DictKey_WptName_", StringComparison.Ordinal )
            || key.StartsWith( "DictKey_ActionComment_", StringComparison.Ordinal )
            || key.StartsWith( "DictKey_GroupName_", StringComparison.Ordinal )
            || key.StartsWith( "DictKey_UnitName_", StringComparison.Ordinal )
            || LuaCodeStringDetector.IsLuaCodeString( original );
    }

    private static int GetDictionaryItemSortOrder( TranslationDictionaryItem item ) {
        if(item.Key.StartsWith( "DictKey_sortie_", StringComparison.Ordinal )) {
            return 0;
        }

        if(item.Key.StartsWith( "DictKey_descriptionText_", StringComparison.Ordinal )) {
            return 1;
        }

        if(item.Key.StartsWith( "DictKey_descriptionBlueTask_", StringComparison.Ordinal )) {
            return 2;
        }

        if(item.Key.StartsWith( "DictKey_descriptionRedTask_", StringComparison.Ordinal )) {
            return 3;
        }

        if(item.Key.StartsWith( "DictKey_descriptionNeutralsTask_", StringComparison.Ordinal )) {
            return 4;
        }

        if(item.Key.StartsWith( "DictKey_description", StringComparison.Ordinal )) {
            return 5;
        }

        if(item.Key.StartsWith( "DictKey_", StringComparison.Ordinal )) {
            return 6;
        }

        return 7;
    }
}

/// <summary>
/// TranslationCreation の読込済み dictionary 状態を表す。
/// </summary>
/// <param name="LoadedItems">dirty 判定基準の項目一覧。</param>
/// <param name="RowItems">画面表示用行一覧。</param>
internal sealed record TranslationCreationDictionaryLoadState(
    IReadOnlyList<TranslationDictionaryItem> LoadedItems,
    IReadOnlyList<TranslationDictionaryItemRowViewModel> RowItems );

/// <summary>
/// アーカイブから読み込んだ dictionary 状態を表す。
/// </summary>
/// <param name="LoadState">DEFAULT dictionary 読込状態。</param>
/// <param name="HasJapaneseDictionary">JP dictionary を内包するかどうか。</param>
/// <param name="JapaneseDictionaryItems">JP dictionary 項目一覧。</param>
internal sealed record TranslationCreationArchiveDictionaryLoadState(
    TranslationCreationDictionaryLoadState LoadState,
    bool HasJapaneseDictionary,
    IReadOnlyList<TranslationDictionaryItem> JapaneseDictionaryItems );