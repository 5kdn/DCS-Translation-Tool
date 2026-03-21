namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 一覧フィルター条件を表す。
/// </summary>
/// <param name="ShowEnabledItems">有効状態の項目を表示するかどうか。</param>
/// <param name="ShowDisabledItems">無効状態の項目を表示するかどうか。</param>
/// <param name="ShowOnlyUntranslated">未翻訳のみ表示するかどうか。</param>
/// <param name="HidePossibleNonTranslationTargets">翻訳対象ではない可能性がある行を非表示にするかどうか。</param>
/// <param name="HideEmptyOriginal">Original が空欄の行を非表示にするかどうか。</param>
public sealed record TranslationCreationFilterOptions(
    bool ShowEnabledItems,
    bool ShowDisabledItems,
    bool ShowOnlyUntranslated,
    bool HidePossibleNonTranslationTargets,
    bool HideEmptyOriginal );