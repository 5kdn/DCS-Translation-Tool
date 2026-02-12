using DcsTranslationTool.Presentation.Wpf.ViewModels;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 適用ユースケース実行に必要な入力を表す。
/// </summary>
/// <param name="SelectedTab">選択中タブ。</param>
/// <param name="SourceAircraftDir">航空機ミッションの適用先ルート。</param>
/// <param name="SourceDlcCampaignDir">DLC キャンペーンの適用先ルート。</param>
/// <param name="SourceUserMissionDir">ユーザーミッションの適用先ルート。</param>
/// <param name="TranslateRootPath">翻訳ファイルルート。</param>
public sealed record ApplyExecutionRequest(
    TabItemViewModel? SelectedTab,
    string? SourceAircraftDir,
    string? SourceDlcCampaignDir,
    string? SourceUserMissionDir,
    string? TranslateRootPath
);