namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 一覧のフィルター判定を担う。
/// </summary>
internal sealed class TranslationCreationFilterService : ITranslationCreationFilterService {
    /// <inheritdoc />
    public bool ShouldInclude( TranslationDictionaryItemRowViewModel row, TranslationCreationFilterOptions options ) {
        if(row.IsEnabled && !options.ShowEnabledItems) {
            return false;
        }

        if(!row.IsEnabled && !options.ShowDisabledItems) {
            return false;
        }

        if(options.HidePossibleNonTranslationTargets && row.IsPossibleNonTranslationTarget) {
            return false;
        }

        if(options.HideEmptyOriginal && string.IsNullOrWhiteSpace( row.Original )) {
            return false;
        }

        if(options.ShowOnlyUntranslated && !string.IsNullOrWhiteSpace( row.Translated )) {
            return false;
        }

        return true;
    }
}