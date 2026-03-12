using Caliburn.Micro;

using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// dictionary 項目の表示行を表す ViewModel である。
/// </summary>
/// <param name="model">表示対象の dictionary 項目。</param>
public sealed class TranslationDictionaryItemRowViewModel( TranslationDictionaryItem model ) : PropertyChangedBase {
    /// <summary>
    /// dictionary のキーを取得する。
    /// </summary>
    public string Key => model.Key;

    /// <summary>
    /// 元文を取得する。
    /// </summary>
    public string Original => model.Original;

    /// <summary>
    /// 翻訳文を取得または設定する。
    /// </summary>
    public string Translated {
        get => model.Translated;
        set {
            if(string.Equals( model.Translated, value, StringComparison.Ordinal )) {
                return;
            }

            model.Translated = value;
            NotifyOfPropertyChange();
        }
    }
}