using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の生成を担うファクトリ。
/// </summary>
/// <param name="appSettingsService">アプリケーション設定サービス。</param>
/// <param name="systemService">システム連携サービス。</param>
/// <param name="logger">ロギングサービス。</param>
/// <param name="workflowService">ワークフローサービス。</param>
/// <param name="layoutStateService">レイアウト状態サービス。</param>
/// <param name="dialogService">ダイアログサービス。</param>
/// <param name="filterService">フィルターサービス。</param>
/// <param name="notificationService">通知サービス。</param>
public sealed class TranslationCreationViewModelFactory(
    IAppSettingsService appSettingsService,
    ISystemService systemService,
    ILoggingService logger,
    ITranslationCreationWorkflowService workflowService,
    ITranslationCreationLayoutStateService layoutStateService,
    ITranslationCreationDialogService dialogService,
    ITranslationCreationFilterService filterService,
    ITranslationCreationNotificationService notificationService
) : ITranslationCreationViewModelFactory {
    /// <inheritdoc />
    public ITranslationCreationViewModel Create( string archiveFullPath ) =>
        new TranslationCreationViewModel(
            archiveFullPath,
            appSettingsService,
            systemService,
            logger,
            new TranslationCreationSession(),
            workflowService,
            layoutStateService,
            dialogService,
            filterService,
            notificationService );
}