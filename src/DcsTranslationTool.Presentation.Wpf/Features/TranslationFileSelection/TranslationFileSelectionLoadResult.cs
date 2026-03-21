using DcsTranslationTool.Presentation.Wpf.ViewModels;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

/// <summary>
/// Translation File Selection の読み込み結果を表す。
/// </summary>
/// <param name="Tabs">表示するタブ一覧。</param>
/// <param name="StatusMessage">画面へ表示する状態メッセージ。</param>
/// <param name="NotificationMessage">通知が必要な場合のメッセージ。</param>
public sealed record TranslationFileSelectionLoadResult(
    IReadOnlyList<TabItemViewModel> Tabs,
    string StatusMessage,
    string? NotificationMessage = null
);