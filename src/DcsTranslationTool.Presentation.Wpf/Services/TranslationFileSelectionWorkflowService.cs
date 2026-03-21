using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// Translation File Selection の読み込みワークフローを提供する。
/// </summary>
public sealed class TranslationFileSelectionWorkflowService(
    IAppSettingsService appSettingsService,
    ILoggingService logger,
    ITranslationArchiveDiscoveryService translationArchiveDiscoveryService,
    ITranslationArchiveTreeService translationArchiveTreeService
) : ITranslationFileSelectionWorkflowService {
    /// <inheritdoc/>
    public async Task<TranslationFileSelectionLoadResult> LoadAsync( CancellationToken cancellationToken ) {
        if(IsSourceDirectoryNotConfigured()) {
            logger.Warn( "探索元ディレクトリが未設定のため読み込みを中断する。" );
            var emptyTabs = translationArchiveTreeService.BuildTabs( [], string.Empty, string.Empty );
            return new TranslationFileSelectionLoadResult( emptyTabs, Strings_Translation.SettingsNotConfiguredMessage );
        }

        try {
            var settings = appSettingsService.Settings;
            var entries = await translationArchiveDiscoveryService.DiscoverAsync(
                settings.DcsWorldInstallDir,
                settings.SourceUserMissionDir,
                cancellationToken );
            var tabs = translationArchiveTreeService.BuildTabs(
                entries,
                settings.DcsWorldInstallDir,
                settings.SourceUserMissionDir );
            return new TranslationFileSelectionLoadResult( tabs, string.Empty );
        }
        catch(OperationCanceledException) {
            logger.Warn( "翻訳対象アーカイブ一覧の読み込みがキャンセルされた。" );
            throw;
        }
        catch(Exception ex) {
            logger.Error( "翻訳対象アーカイブ一覧の読み込みに失敗した。", ex );
            var emptyTabs = translationArchiveTreeService.BuildTabs( [], string.Empty, string.Empty );
            return new TranslationFileSelectionLoadResult(
                emptyTabs,
                Strings_Translation.LoadFailedMessage,
                Strings_Translation.LoadFailedMessage );
        }
    }

    /// <summary>
    /// 探索元ディレクトリが未設定かどうかを判定する。
    /// </summary>
    /// <returns>両方未設定の場合は <see langword="true"/>。</returns>
    private bool IsSourceDirectoryNotConfigured() {
        var settings = appSettingsService.Settings;
        return string.IsNullOrWhiteSpace( settings.DcsWorldInstallDir )
            && string.IsNullOrWhiteSpace( settings.SourceUserMissionDir );
    }
}