using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;

using FluentResults;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の dictionary 読込前処理を担う。
/// </summary>
/// <param name="translationDictionaryService">dictionary 読込サービス。</param>
public sealed class TranslationCreationDictionaryLoader(
    ITranslationDictionaryService translationDictionaryService ) {
    #region PublicMethods

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
    #endregion

    #region PrivateHelpers

    /// <summary>
    /// 読み込んだ dictionary 項目一覧を画面表示用状態へ変換する。
    /// </summary>
    /// <param name="items">変換対象の dictionary 項目一覧。</param>
    /// <returns>変換後の読込状態を返す。</returns>
    private static TranslationCreationDictionaryLoadState BuildDictionaryLoadState( IReadOnlyList<TranslationDictionaryItem> items ) {
        var initializedItems = items
            .Select( InitializeDictionaryItem )
            .OrderBy( GetDictionaryItemSortOrder )
            .ThenBy( item => item.Key, TranslationCreationNaturalKeyComparer.Instance )
            .ToArray();

        List<TranslationDictionaryItem> loadedItems = new( initializedItems.Length );
        List<TranslationCreationRowState> rowStates = new( initializedItems.Length );
        foreach(var item in initializedItems) {
            var isPossibleNonTranslationTarget = IsPossibleNonTranslationTarget( item );
            loadedItems.Add( new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Translated,
                IsEnabled = item.IsEnabled
            } );
            rowStates.Add( new TranslationCreationRowState( item, isPossibleNonTranslationTarget ) );
        }

        return new TranslationCreationDictionaryLoadState( loadedItems, rowStates );
    }

    /// <summary>
    /// 画面初期表示向けに dictionary 項目を初期化する。
    /// </summary>
    /// <param name="item">初期化対象の項目。</param>
    /// <returns>初期化後の項目を返す。</returns>
    private static TranslationDictionaryItem InitializeDictionaryItem( TranslationDictionaryItem item ) =>
        new( item.Key, item.Original )
        {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled && !IsPossibleNonTranslationTarget( item )
        };

    /// <summary>
    /// dictionary 項目を複製する。
    /// </summary>
    /// <param name="item">複製元の項目。</param>
    /// <returns>複製した項目を返す。</returns>
    private static TranslationDictionaryItem CloneDictionaryItem( TranslationDictionaryItem item ) =>
        new( item.Key, item.Original )
        {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled
        };

    /// <summary>
    /// 指定項目が翻訳対象ではない可能性があるかどうかを判定する。
    /// </summary>
    /// <param name="item">判定対象の項目。</param>
    /// <returns>翻訳対象ではない可能性がある場合は <see langword="true"/> を返す。</returns>
    private static bool IsPossibleNonTranslationTarget( TranslationDictionaryItem item ) =>
        IsPossibleNonTranslationTarget( item.Key, item.Original );

    /// <summary>
    /// 指定 key と original が翻訳対象ではない可能性があるかどうかを判定する。
    /// </summary>
    /// <param name="key">判定対象の key。</param>
    /// <param name="original">判定対象の original。</param>
    /// <returns>翻訳対象ではない可能性がある場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// dictionary 項目の表示順序を取得する。
    /// </summary>
    /// <param name="item">判定対象の項目。</param>
    /// <returns>表示順序を表す整数値を返す。</returns>
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
    #endregion
}

/// <summary>
/// TranslationCreation の読込済み dictionary 状態を表す。
/// </summary>
/// <param name="LoadedItems">読み込み済み項目一覧。</param>
/// <param name="RowStates">画面表示用行状態一覧。</param>
public sealed record TranslationCreationDictionaryLoadState(
    IReadOnlyList<TranslationDictionaryItem> LoadedItems,
    IReadOnlyList<TranslationCreationRowState> RowStates );

/// <summary>
/// アーカイブから読み込んだ dictionary 状態を表す。
/// </summary>
/// <param name="LoadState">DEFAULT dictionary 読込状態。</param>
/// <param name="HasJapaneseDictionary">JP dictionary を内包するかどうか。</param>
/// <param name="JapaneseDictionaryItems">JP dictionary 項目一覧。</param>
public sealed record TranslationCreationArchiveDictionaryLoadState(
    TranslationCreationDictionaryLoadState LoadState,
    bool HasJapaneseDictionary,
    IReadOnlyList<TranslationDictionaryItem> JapaneseDictionaryItems );