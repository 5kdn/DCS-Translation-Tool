using DcsTranslationTool.Presentation.Wpf.ViewModels;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 適用ユースケース実行に必要な入力を表す。
/// </summary>
/// <param name="SelectedTab">選択中タブ。</param>
/// <param name="DcsWorldInstallDir">DCS Worldのインストールフォルダー。</param>
/// <param name="SourceUserMissionDir">ユーザーミッションの適用先ルート。</param>
/// <param name="UseExternalAircraftInjectionDir">Aircraft用外部保存を有効にするかどうか。</param>
/// <param name="ExternalAircraftInjectionDir">Aircraft外部保存フォルダー。</param>
/// <param name="UseExternalCampaignInjectionDir">DLC Campaigns用外部保存を有効にするかどうか。</param>
/// <param name="ExternalCampaignInjectionDir">DLC Campaigns用外部保存フォルダー。</param>
/// <param name="TranslateRootPath">翻訳ファイルルート。</param>
public sealed record ApplyExecutionRequest(
    TabItemViewModel? SelectedTab,
    string? DcsWorldInstallDir,
    string? SourceUserMissionDir,
    bool UseExternalAircraftInjectionDir,
    string? ExternalAircraftInjectionDir,
    bool UseExternalCampaignInjectionDir,
    string? ExternalCampaignInjectionDir,
    string? TranslateRootPath
);