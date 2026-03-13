namespace DcsTranslationTool.Application.Models;

/// <summary>
/// dictionary 内 value 文字列の範囲を表すモデル。
/// </summary>
/// <param name="key">対象キー。</param>
/// <param name="startIndex">元テキスト内の value 開始位置。</param>
/// <param name="length">元テキスト内の value 長さ。</param>
public sealed class TranslationDictionaryValueRange( string key, int startIndex, int length ) {
    /// <summary>
    /// 対象キーを取得する。
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// 元テキスト内の value 開始位置を取得する。
    /// </summary>
    public int StartIndex { get; } = startIndex;

    /// <summary>
    /// 元テキスト内の value 長さを取得する。
    /// </summary>
    public int Length { get; } = length;
}