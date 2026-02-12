using DcsTranslationTool.Presentation.Wpf.ViewModels;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// ダウンロードユースケース実行に必要な入力を表す。
/// </summary>
/// <param name="SelectedTab">選択中タブ。</param>
/// <param name="SaveRootPath">翻訳ファイル保存先ルート。</param>
public sealed record DownloadExecutionRequest(
    TabItemViewModel? SelectedTab,
    string? SaveRootPath
);