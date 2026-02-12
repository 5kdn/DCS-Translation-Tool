using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Features.Download;

/// <summary>
/// Download 画面の進捗表示と通知表示を仲介する。
/// </summary>
public sealed class DownloadWorkflowUiAdapter(
    IDispatcherService dispatcherService,
    ISnackbarService snackbarService,
    Action<double> setDownloadedProgress,
    Action<double> setAppliedProgress
) {
    /// <summary>
    /// ダウンロード進捗を更新する。
    /// </summary>
    /// <param name="value">進捗率。</param>
    /// <returns>非同期タスク。</returns>
    public Task UpdateDownloadProgressAsync( double value ) =>
        dispatcherService.InvokeAsync( () => {
            setDownloadedProgress( value );
            return Task.CompletedTask;
        } );

    /// <summary>
    /// 適用進捗を更新する。
    /// </summary>
    /// <param name="value">進捗率。</param>
    /// <returns>非同期タスク。</returns>
    public Task UpdateApplyProgressAsync( double value ) =>
        dispatcherService.InvokeAsync( () => {
            setAppliedProgress( value );
            return Task.CompletedTask;
        } );

    /// <summary>
    /// スナックバーへメッセージを表示する。
    /// </summary>
    /// <param name="message">表示メッセージ。</param>
    /// <returns>非同期タスク。</returns>
    public Task ShowSnackbarAsync( string message ) =>
        dispatcherService.InvokeAsync( () => {
            snackbarService.Show( message );
            return Task.CompletedTask;
        } );
}