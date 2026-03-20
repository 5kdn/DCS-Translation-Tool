namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 一覧のフィルター判定を提供する。
/// </summary>
public interface ITranslationCreationFilterService {
    /// <summary>
    /// 指定行を現在のフィルター条件で表示対象に含めるかどうかを判定する。
    /// </summary>
    /// <param name="row">判定対象の行。</param>
    /// <param name="options">判定に利用するフィルター条件。</param>
    /// <returns>表示対象の場合は <see langword="true"/> を返す。</returns>
    bool ShouldInclude( TranslationDictionaryItemRowViewModel row, TranslationCreationFilterOptions options );
}