using System.ComponentModel;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の行変更通知を表す。
/// </summary>
/// <param name="row">変更対象の行。</param>
/// <param name="propertyName">変更対象プロパティ名。</param>
public sealed class TranslationCreationRowPropertyChangedEventArgs(
    TranslationDictionaryItemRowViewModel row,
    string propertyName ) : PropertyChangedEventArgs( propertyName ) {
    /// <summary>
    /// 変更対象の行を取得する。
    /// </summary>
    public TranslationDictionaryItemRowViewModel Row { get; } = row;
}