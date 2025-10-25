using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Views;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// Material Design ダイアログを利用して汎用的なダイアログ処理を提供する。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public sealed class DialogService( ILoggingService logger ) : IDialogService {
    /// <inheritdoc/>
    public async Task<bool> ShowAsync( ConfirmationDialogParameters parameters ) {
        ArgumentNullException.ThrowIfNull( parameters );
        logger.Info( $"確認ダイアログを表示する。Title={parameters.Title}, Identifier={parameters.DialogIdentifier}" );
        var dialog = new ConfirmationDialog
        {
            DataContext = parameters
        };
        var result = await DialogHost.Show( dialog, parameters.DialogIdentifier );
        var isConfirmed = result switch
        {
            true => true,
            string str when bool.TryParse( str, out var parsed ) => parsed,
            _ => false
        };
        logger.Info( $"確認ダイアログが閉じられた。Confirmed={isConfirmed}" );
        return isConfirmed;
    }
}