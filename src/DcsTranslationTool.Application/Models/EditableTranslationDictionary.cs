namespace DcsTranslationTool.Application.Models;

/// <summary>
/// 元の dictionary テキストと編集対象範囲を保持するモデル。
/// </summary>
/// <param name="originalText">元の dictionary テキスト。</param>
/// <param name="items">有効な dictionary 項目一覧。</param>
/// <param name="valueRanges">キーごとの value 置換範囲。</param>
public sealed class EditableTranslationDictionary(
    string originalText,
    IReadOnlyList<TranslationDictionaryItem> items,
    IReadOnlyDictionary<string, TranslationDictionaryValueRange> valueRanges
) {
    /// <summary>
    /// 元の dictionary テキストを取得する。
    /// </summary>
    public string OriginalText { get; } = originalText;

    /// <summary>
    /// 有効な dictionary 項目一覧を取得する。
    /// </summary>
    public IReadOnlyList<TranslationDictionaryItem> Items { get; } = items;

    /// <summary>
    /// キーごとの value 置換範囲を取得する。
    /// </summary>
    public IReadOnlyDictionary<string, TranslationDictionaryValueRange> ValueRanges { get; } = valueRanges;
}