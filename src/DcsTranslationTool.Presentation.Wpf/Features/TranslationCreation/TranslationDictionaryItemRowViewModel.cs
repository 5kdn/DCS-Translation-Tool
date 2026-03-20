using Caliburn.Micro;

using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// dictionary 項目の表示行を表す ViewModel である。
/// </summary>
/// <param name="model">表示対象の dictionary 項目。</param>
/// <param name="isPossibleNonTranslationTarget">翻訳対象ではない可能性があるかどうか。</param>
public sealed class TranslationDictionaryItemRowViewModel(
    TranslationDictionaryItem model,
    bool isPossibleNonTranslationTarget = false ) : PropertyChangedBase {
    private string _initialTranslated = model.Translated;
    private bool _initialIsEnabled = model.IsEnabled;
    private bool _hasPendingChanges;

    /// <summary>
    /// dictionary のキーを取得する。
    /// </summary>
    public string Key => model.Key;

    /// <summary>
    /// 元文を取得する。
    /// </summary>
    public string Original => model.Original;

    /// <summary>
    /// DataGrid 表示用の元文を取得する。
    /// </summary>
    public string OriginalDisplayText => EscapeLineBreaks( Original );

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
            NotifyOfPropertyChange( nameof( TranslatedDisplayText ) );
        }
    }

    /// <summary>
    /// 項目が有効かどうかを取得または設定する。
    /// </summary>
    public bool IsEnabled {
        get => model.IsEnabled;
        set {
            if(model.IsEnabled == value) {
                return;
            }

            model.IsEnabled = value;
            NotifyOfPropertyChange();
        }
    }

    /// <summary>
    /// DataGrid 表示用の翻訳文を取得する。
    /// </summary>
    public string TranslatedDisplayText => EscapeLineBreaks( Translated );

    /// <summary>
    /// 翻訳対象ではない可能性があるかどうかを取得する。
    /// </summary>
    public bool IsPossibleNonTranslationTarget { get; } = isPossibleNonTranslationTarget;

    /// <summary>
    /// 初期読込時点から未反映変更が存在するかどうかを取得する。
    /// </summary>
    internal bool HasPendingChanges => _hasPendingChanges;

    /// <summary>
    /// 現在値を基準値と比較して dirty 状態を更新する。
    /// </summary>
    /// <returns>dirty 状態が変化した場合は <see langword="true"/> を返す。</returns>
    internal bool UpdatePendingChanges() {
        var hasPendingChanges =
            !string.Equals( model.Translated, _initialTranslated, StringComparison.Ordinal )
            || model.IsEnabled != _initialIsEnabled;
        if(_hasPendingChanges == hasPendingChanges) {
            return false;
        }

        _hasPendingChanges = hasPendingChanges;
        return true;
    }

    /// <summary>
    /// 保留中の翻訳文を適用したと仮定した場合に dirty かどうかを判定する。
    /// </summary>
    /// <param name="translatedOverride">比較に利用する翻訳文。</param>
    /// <returns>保留値を加味して未反映変更が存在する場合は <see langword="true"/> を返す。</returns>
    internal bool HasPendingChangesWithTranslatedOverride( string translatedOverride ) =>
        !string.Equals( translatedOverride, _initialTranslated, StringComparison.Ordinal )
        || model.IsEnabled != _initialIsEnabled;

    /// <summary>
    /// 現在値を dirty 判定の基準値として再設定する。
    /// </summary>
    internal void ResetPendingChangesBaseline() {
        _initialTranslated = model.Translated;
        _initialIsEnabled = model.IsEnabled;
        _hasPendingChanges = false;
    }

    private static string EscapeLineBreaks( string value ) => NormalizeLineEndings( value )
        .Replace( "\n", @"\", StringComparison.Ordinal );

    private static string NormalizeLineEndings( string value ) => value
        .Replace( "\r\n", "\n", StringComparison.Ordinal )
        .Replace( '\r', '\n' );
}