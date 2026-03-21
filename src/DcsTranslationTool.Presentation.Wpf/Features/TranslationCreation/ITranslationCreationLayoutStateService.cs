using DcsTranslationTool.Application.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation のレイアウト状態の読込と保存を提供する。
/// </summary>
public interface ITranslationCreationLayoutStateService {
    /// <summary>
    /// 保存済みレイアウト状態を読み込む。
    /// </summary>
    /// <returns>読み込んだレイアウト状態を返す。</returns>
    TranslationCreationLayoutState Load();

    /// <summary>
    /// レイアウト状態を保存する。
    /// </summary>
    /// <param name="state">保存する状態。</param>
    void Save( TranslationCreationLayoutState state );
}

/// <summary>
/// TranslationCreation のレイアウト状態をアプリ設定へ永続化する。
/// </summary>
/// <param name="appSettingsService">アプリケーション設定サービス。</param>
public sealed class TranslationCreationLayoutStateService(
    IAppSettingsService appSettingsService ) : ITranslationCreationLayoutStateService {
    /// <inheritdoc />
    public TranslationCreationLayoutState Load() =>
        new(
            TranslationCreationLayoutDefaults.NormalizeWindowLength(
                appSettingsService.Settings.TranslationCreationWindowWidth,
                TranslationCreationLayoutDefaults.DefaultWindowWidth,
                TranslationCreationLayoutDefaults.MinWindowWidth ),
            TranslationCreationLayoutDefaults.NormalizeWindowLength(
                appSettingsService.Settings.TranslationCreationWindowHeight,
                TranslationCreationLayoutDefaults.DefaultWindowHeight,
                TranslationCreationLayoutDefaults.MinWindowHeight ),
            TranslationCreationLayoutDefaults.NormalizeDictionaryPaneRatio(
                appSettingsService.Settings.TranslationCreationDictionaryPaneRatio ),
            appSettingsService.Settings.TranslationCreationWrapDictionaryDetailsText );

    /// <inheritdoc />
    public void Save( TranslationCreationLayoutState state ) {
        appSettingsService.Settings.TranslationCreationWindowWidth =
            TranslationCreationLayoutDefaults.NormalizeWindowLength(
                state.WindowWidth,
                TranslationCreationLayoutDefaults.DefaultWindowWidth,
                TranslationCreationLayoutDefaults.MinWindowWidth );
        appSettingsService.Settings.TranslationCreationWindowHeight =
            TranslationCreationLayoutDefaults.NormalizeWindowLength(
                state.WindowHeight,
                TranslationCreationLayoutDefaults.DefaultWindowHeight,
                TranslationCreationLayoutDefaults.MinWindowHeight );
        appSettingsService.Settings.TranslationCreationDictionaryPaneRatio =
            TranslationCreationLayoutDefaults.NormalizeDictionaryPaneRatio( state.DictionaryPaneRatio );
        appSettingsService.Settings.TranslationCreationWrapDictionaryDetailsText = state.IsDictionaryDetailsWrapEnabled;
    }
}