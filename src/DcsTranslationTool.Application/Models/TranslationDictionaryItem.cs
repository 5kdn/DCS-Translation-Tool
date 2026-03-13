namespace DcsTranslationTool.Application.Models;

/// <summary>
/// dictionary の翻訳項目を表すモデルである。
/// </summary>
/// <param name="key">dictionary のキー。</param>
/// <param name="original">dictionary の元文。</param>
public sealed class TranslationDictionaryItem( string key, string original ) {
    /// <summary>
    /// dictionary のキーを取得する。
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// dictionary の元文を取得する。
    /// </summary>
    public string Original { get; } = original;

    /// <summary>
    /// 翻訳文を取得または設定する。
    /// </summary>
    public string Translated { get; set; } = string.Empty;

    /// <summary>
    /// 項目が有効かどうかを取得または設定する。
    /// </summary>
    public bool IsEnabled { get; set; } = true;
}