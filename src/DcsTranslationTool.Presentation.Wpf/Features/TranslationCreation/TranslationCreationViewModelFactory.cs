using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の生成を担うファクトリ。
/// </summary>
/// <param name="appSettingsService">アプリケーション設定サービス。</param>
/// <param name="applicationInfoService">アプリケーション情報サービス。</param>
/// <param name="dialogService">ダイアログ表示サービス。</param>
/// <param name="logger">ロギングサービス。</param>
/// <param name="translationDictionaryService">dictionary 読込サービス。</param>
public sealed class TranslationCreationViewModelFactory(
    IAppSettingsService appSettingsService,
    IApplicationInfoService applicationInfoService,
    IDialogService dialogService,
    IDialogProvider dialogProvider,
    ISystemService systemService,
    ILoggingService logger,
    ITranslationDictionaryService translationDictionaryService
) : ITranslationCreationViewModelFactory {
    /// <inheritdoc />
    public TranslationCreationViewModel Create( string archiveFullPath ) =>
        new( archiveFullPath, appSettingsService, applicationInfoService, dialogService, dialogProvider, systemService, logger, translationDictionaryService );
}