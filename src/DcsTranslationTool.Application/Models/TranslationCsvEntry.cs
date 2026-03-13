namespace DcsTranslationTool.Application.Models;

/// <summary>
/// CSV ファイルの 1 行を表現する。
/// </summary>
/// <param name="key">Key 列の値。</param>
/// <param name="original">Original 列の値。</param>
/// <param name="translated">Translated 列の値。</param>
/// <param name="isEnabled">行が有効かどうか。</param>
public sealed class TranslationCsvEntry( string key, string original, string translated, bool isEnabled = true ) {
    /// <summary>
    /// Key 列の値を取得する。
    /// </summary>
    public string Key { get; } = key;

    /// <summary>
    /// Original 列の値を取得する。
    /// </summary>
    public string Original { get; } = original;

    /// <summary>
    /// Translated 列の値を取得する。
    /// </summary>
    public string Translated { get; } = translated;

    /// <summary>
    /// 行が有効かどうかを取得する。
    /// </summary>
    public bool IsEnabled { get; } = isEnabled;
}