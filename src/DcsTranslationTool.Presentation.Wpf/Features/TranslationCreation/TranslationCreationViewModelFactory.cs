using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の生成を担うファクトリである。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
/// <param name="translationDictionaryService">dictionary 読込サービス。</param>
public sealed class TranslationCreationViewModelFactory(
    ILoggingService logger,
    ITranslationDictionaryService translationDictionaryService
) : ITranslationCreationViewModelFactory {
    /// <inheritdoc />
    public TranslationCreationViewModel Create( string archiveFullPath ) =>
        new( archiveFullPath, logger, translationDictionaryService );
}