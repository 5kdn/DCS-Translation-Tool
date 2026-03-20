using Caliburn.Micro;

using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の編集行状態を表す。
/// </summary>
/// <param name="item">編集対象の dictionary 項目。</param>
/// <param name="isPossibleNonTranslationTarget">翻訳対象ではない可能性があるかどうか。</param>
public sealed class TranslationCreationRowState(
    TranslationDictionaryItem item,
    bool isPossibleNonTranslationTarget = false ) : PropertyChangedBase {
    private string _initialTranslated = item.Translated;
    private bool _initialIsEnabled = item.IsEnabled;
    private bool _hasPendingChanges;

    /// <summary>
    /// dictionary のキーを取得する。
    /// </summary>
    public string Key => item.Key;

    /// <summary>
    /// 元文を取得する。
    /// </summary>
    public string Original => item.Original;

    /// <summary>
    /// 翻訳文を取得または設定する。
    /// </summary>
    public string Translated {
        get => item.Translated;
        set {
            if(string.Equals( item.Translated, value, StringComparison.Ordinal )) {
                return;
            }

            item.Translated = value;
            NotifyOfPropertyChange();
        }
    }

    /// <summary>
    /// 項目が有効かどうかを取得または設定する。
    /// </summary>
    public bool IsEnabled {
        get => item.IsEnabled;
        set {
            if(item.IsEnabled == value) {
                return;
            }

            item.IsEnabled = value;
            NotifyOfPropertyChange();
        }
    }

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
            !string.Equals( item.Translated, _initialTranslated, StringComparison.Ordinal )
            || item.IsEnabled != _initialIsEnabled;
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
        || item.IsEnabled != _initialIsEnabled;

    /// <summary>
    /// 現在値を dirty 判定の基準値として再設定する。
    /// </summary>
    internal void ResetPendingChangesBaseline() {
        _initialTranslated = item.Translated;
        _initialIsEnabled = item.IsEnabled;
        _hasPendingChanges = false;
    }

    /// <summary>
    /// 現在の状態から dictionary 項目を生成する。
    /// </summary>
    /// <returns>生成した dictionary 項目を返す。</returns>
    internal TranslationDictionaryItem ToTranslationDictionaryItem() =>
        new( item.Key, item.Original )
        {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled,
        };
}