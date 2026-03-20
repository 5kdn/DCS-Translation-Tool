using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation のパス解決を担う。
/// </summary>
internal sealed class TranslationCreationPathService : ITranslationCreationPathService {
    /// <inheritdoc />
    public string GetDictionaryExportPath( AppSettings settings, string archiveFullPath ) =>
        new TranslationCreationPathResolver( settings, archiveFullPath ).GetDictionaryExportPath();

    /// <inheritdoc />
    public string GetPoExportPath( AppSettings settings, string archiveFullPath ) =>
        new TranslationCreationPathResolver( settings, archiveFullPath ).GetPoExportPath();

    /// <inheritdoc />
    public string GetCsvExportPath( AppSettings settings, string archiveFullPath ) =>
        new TranslationCreationPathResolver( settings, archiveFullPath ).GetCsvExportPath();

    /// <inheritdoc />
    public string GetPoImportInitialPath( AppSettings settings, string archiveFullPath ) =>
        new TranslationCreationPathResolver( settings, archiveFullPath ).GetPoImportInitialPath();

    /// <inheritdoc />
    public string GetDictionaryImportInitialPath( AppSettings settings, string archiveFullPath ) =>
        new TranslationCreationPathResolver( settings, archiveFullPath ).GetDictionaryImportInitialPath();

    /// <inheritdoc />
    public string GetCsvImportInitialPath( AppSettings settings, string archiveFullPath ) =>
        new TranslationCreationPathResolver( settings, archiveFullPath ).GetCsvImportInitialPath();
}