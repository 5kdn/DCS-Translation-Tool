using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

using Microsoft.Xaml.Behaviors;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// 更新再起動前に翻訳作成ウィンドウの終了確認を調整する。
/// </summary>
/// <param name="windowProvider">アプリケーションウィンドウ提供元を受け取る。</param>
public sealed class ApplicationRestartCoordinator(
    IApplicationWindowProvider windowProvider
) : IApplicationRestartCoordinator {
    /// <inheritdoc />
    public async Task<bool> PrepareForRestartAsync( CancellationToken cancellationToken = default ) {
        var targets = windowProvider.GetWindows()
            .Where( window => window.DataContext is ITranslationCreationViewModel )
            .Select( window => (Window: window, ViewModel: (ITranslationCreationViewModel)window.DataContext) )
            .ToArray();

        foreach(var target in targets) {
            cancellationToken.ThrowIfCancellationRequested();
            if(!await target.ViewModel.ConfirmCloseAsync()) {
                return false;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        foreach(var target in targets) {
            foreach(var behavior in Interaction.GetBehaviors( target.Window ).OfType<TranslationCreationLifecycleBehavior>()) {
                behavior.ApproveClose();
            }
            target.Window.Close();
        }

        return true;
    }
}