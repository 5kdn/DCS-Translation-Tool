namespace DcsTranslationTool.Application.Models;

/// <summary>
/// アーカイブ起動時に必要な dictionary 読込結果をまとめて保持するモデル。
/// </summary>
/// <param name="defaultDictionaryItems">default dictionary の項目一覧。</param>
/// <param name="hasJapaneseDictionary">JP dictionary が存在するかどうか。</param>
/// <param name="japaneseDictionaryItems">JP dictionary の項目一覧。</param>
public sealed class TranslationArchiveDictionaries(
    IReadOnlyList<TranslationDictionaryItem> defaultDictionaryItems,
    bool hasJapaneseDictionary,
    IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems
) {
    /// <summary>
    /// default dictionary の項目一覧を取得する。
    /// </summary>
    public IReadOnlyList<TranslationDictionaryItem> DefaultDictionaryItems { get; } = defaultDictionaryItems;

    /// <summary>
    /// JP dictionary が存在するかどうかを取得する。
    /// </summary>
    public bool HasJapaneseDictionary { get; } = hasJapaneseDictionary;

    /// <summary>
    /// JP dictionary の項目一覧を取得する。
    /// </summary>
    public IReadOnlyList<TranslationDictionaryItem> JapaneseDictionaryItems { get; } = japaneseDictionaryItems;
}