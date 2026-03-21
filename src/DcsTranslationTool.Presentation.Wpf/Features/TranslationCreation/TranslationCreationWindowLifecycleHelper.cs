using System.Windows.Threading;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView の表示後初期化処理を補助する。
/// </summary>
internal static class TranslationCreationWindowLifecycleHelper {
    /// <summary>
    /// ContentRendered 後の初期化処理を UI スレッドで最後まで待機して実行する。
    /// </summary>
    /// <param name="dispatcher">実行に利用するディスパッチャー。</param>
    /// <param name="windowLoadedAction">実行対象の初期化処理。</param>
    /// <param name="priority">ディスパッチャー実行優先度。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    internal static async Task ExecuteWindowLoadedAsync(
        Dispatcher dispatcher,
        Func<Task> windowLoadedAction,
        DispatcherPriority priority = DispatcherPriority.ContextIdle,
        CancellationToken cancellationToken = default ) {
        ArgumentNullException.ThrowIfNull( dispatcher );
        ArgumentNullException.ThrowIfNull( windowLoadedAction );

        await await dispatcher.InvokeAsync(
            () => {
                cancellationToken.ThrowIfCancellationRequested();
                return windowLoadedAction();
            },
            priority );
    }
}