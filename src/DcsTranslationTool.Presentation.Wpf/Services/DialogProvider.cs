using System.Runtime.InteropServices;

using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

using Microsoft.Win32;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// ダイアログ表示を担当するプロバイダ。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public class DialogProvider( ILoggingService logger ) : IDialogProvider {
    /// <inheritdoc/>
    public bool ShowFolderPicker( string initialDirectory, out string selectedPath ) {
        selectedPath = string.Empty;
        try {
            logger.Info( $"フォルダー選択ダイアログを表示する。InitialDirectory={initialDirectory}" );
            var dialog = new OpenFolderDialog
            {
                Title = "フォルダを選択してください",
                InitialDirectory = initialDirectory,
                Multiselect = false,
            };
            var result = dialog.ShowDialog();
            if(result is true) {
                selectedPath = dialog.FolderName;
                logger.Info( $"フォルダー選択ダイアログで選択された。Path={selectedPath}" );
                return true;
            }
            logger.Info( "フォルダー選択ダイアログがキャンセルされた。" );
            return false;
        }
        catch(COMException ex) {
            logger.Error( "フォルダー選択ダイアログの表示に COM 例外が発生した。", ex );
            return false;
        }
        catch(Exception ex) {
            logger.Error( "フォルダー選択ダイアログの表示中に予期しない例外が発生した。", ex );
            return false;
        }
    }
}