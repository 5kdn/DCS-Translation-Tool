namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

/// <summary>
/// Translation File Selection の現在選択状態を表す。
/// </summary>
/// <param name="HasSelectedEntry">選択中ノードが存在するかどうか。</param>
/// <param name="CanOpenDirectory">フォルダーを開く操作が可能かどうか。</param>
/// <param name="CanCreateTranslation">翻訳作成操作が可能かどうか。</param>
/// <param name="SelectedEntryPath">選択ノードに対応する絶対パス。</param>
/// <param name="SelectedArchiveFullPath">翻訳作成対象アーカイブの絶対パス。</param>
public sealed record TranslationFileSelectionSelectionState(
    bool HasSelectedEntry,
    bool CanOpenDirectory,
    bool CanCreateTranslation,
    string? SelectedEntryPath,
    string? SelectedArchiveFullPath
);