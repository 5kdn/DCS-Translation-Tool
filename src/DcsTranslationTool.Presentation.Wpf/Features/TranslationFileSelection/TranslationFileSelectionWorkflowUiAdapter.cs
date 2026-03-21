using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;

/// <summary>
/// Translation File Selection 画面の状態反映と通知表示を仲介する。
/// </summary>
public sealed class TranslationFileSelectionWorkflowUiAdapter(
    IDispatcherService dispatcherService,
    ISnackbarService snackbarService
) : ITranslationFileSelectionWorkflowUiAdapter {
    /// <inheritdoc/>
    public Task ApplyLoadResultAsync(
        TranslationFileSelectionLoadResult loadResult,
        Action<TranslationFileSelectionLoadResult> applyLoadResult ) =>
        dispatcherService.InvokeAsync( () => {
            applyLoadResult( loadResult );

            if(!string.IsNullOrWhiteSpace( loadResult.NotificationMessage )) {
                snackbarService.Show( loadResult.NotificationMessage );
            }

            return Task.CompletedTask;
        } );
}