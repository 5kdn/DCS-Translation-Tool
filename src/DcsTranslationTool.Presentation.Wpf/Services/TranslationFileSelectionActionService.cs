using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// Translation File Selection から起動する外部操作を提供する。
/// </summary>
public sealed class TranslationFileSelectionActionService(
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService,
    ITranslationCreationViewModelFactory translationCreationViewModelFactory,
    IWindowManager windowManager
) : ITranslationFileSelectionActionService {
    /// <inheritdoc/>
    public void OpenDirectory( string path ) {
        try {
            logger.Info( $"選択ノードのディレクトリを開く。Path={path}" );
            systemService.OpenDirectory( path );
        }
        catch(Exception ex) {
            logger.Error( "選択ノードのディレクトリを開く処理に失敗した。", ex );
            snackbarService.Show( Strings_Translation.OpenDirectoryFailedMessage );
        }
    }

    /// <inheritdoc/>
    public async Task OpenTranslationCreationAsync( string archiveFullPath ) {
        ITranslationCreationViewModel translationCreationViewModel;

        try {
            logger.Info( $"翻訳作成 ViewModel を生成する。Archive={archiveFullPath}" );
            translationCreationViewModel = translationCreationViewModelFactory.Create( archiveFullPath );
        }
        catch(Exception ex) {
            logger.Error( $"翻訳作成 ViewModel の生成に失敗した。Archive={archiveFullPath}", ex );
            snackbarService.Show( Strings_Translation.CreateTranslationWindowOpenFailedMessage );
            return;
        }

        try {
            logger.Info( $"翻訳作成ウィンドウを表示する。Archive={archiveFullPath}" );
            await windowManager.ShowWindowAsync( translationCreationViewModel );
        }
        catch(Exception ex) {
            logger.Error( $"翻訳作成ウィンドウの表示に失敗した。Archive={archiveFullPath}", ex );
            snackbarService.Show( Strings_Translation.CreateTranslationWindowOpenFailedMessage );
        }
    }

    /// <inheritdoc/>
    public void ClearNotifications() => snackbarService.Clear();
}