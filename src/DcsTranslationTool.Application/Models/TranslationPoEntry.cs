namespace DcsTranslationTool.Application.Models;

/// <summary>
/// PO ファイルの 1 エントリーを表現する。
/// </summary>
/// <param name="context">msgctxt の値。</param>
/// <param name="original">msgid の値。</param>
/// <param name="translated">msgstr の値。</param>
/// <param name="isEnabled">エントリーが有効かどうか。</param>
public sealed class TranslationPoEntry( string context, string original, string translated, bool isEnabled = true ) {
    /// <summary>
    /// msgctxt の値を取得する。
    /// </summary>
    public string Context { get; } = context;

    /// <summary>
    /// msgid の値を取得する。
    /// </summary>
    public string Original { get; } = original;

    /// <summary>
    /// msgstr の値を取得する。
    /// </summary>
    public string Translated { get; } = translated;

    /// <summary>
    /// エントリーが有効かどうかを取得する。
    /// </summary>
    public bool IsEnabled { get; } = isEnabled;
}