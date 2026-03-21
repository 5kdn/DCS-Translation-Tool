using System.IO;
using System.Runtime.InteropServices;

using DcsTranslationTool.Application.Interfaces;
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

    /// <inheritdoc/>
    public bool ShowSaveFilePicker( string initialPath, string filter, out string selectedPath ) {
        selectedPath = string.Empty;
        try {
            logger.Info( $"名前を付けて保存ダイアログを表示する。InitialPath={initialPath}, Filter={filter}" );
            var dialog = new SaveFileDialog
            {
                Title = "保存先を選択してください",
                FileName = Path.GetFileName( initialPath ),
                InitialDirectory = Path.GetDirectoryName( initialPath ),
                Filter = filter,
                AddExtension = false,
                CheckFileExists = false,
                OverwritePrompt = false,
                ValidateNames = true,
            };
            var result = dialog.ShowDialog();
            if(result is true) {
                selectedPath = dialog.FileName;
                logger.Info( $"名前を付けて保存ダイアログで選択された。Path={selectedPath}" );
                return true;
            }

            logger.Info( "名前を付けて保存ダイアログがキャンセルされた。" );
            return false;
        }
        catch(COMException ex) {
            logger.Error( "名前を付けて保存ダイアログの表示に COM 例外が発生した。", ex );
            return false;
        }
        catch(Exception ex) {
            logger.Error( "名前を付けて保存ダイアログの表示中に予期しない例外が発生した。", ex );
            return false;
        }
    }

    /// <inheritdoc/>
    public bool ShowOpenFilePicker( string initialPath, string filter, out string selectedPath ) {
        selectedPath = string.Empty;
        try {
            logger.Info( $"ファイルを開くダイアログを表示する。InitialPath={initialPath}, Filter={filter}" );
            var dialog = new OpenFileDialog
            {
                Title = "読み込むファイルを選択してください",
                FileName = Path.GetFileName( initialPath ),
                InitialDirectory = Path.GetDirectoryName( initialPath ),
                Filter = filter,
                AddExtension = false,
                CheckFileExists = true,
                Multiselect = false,
                ValidateNames = true,
            };
            var result = dialog.ShowDialog();
            if(result is true) {
                selectedPath = dialog.FileName;
                logger.Info( $"ファイルを開くダイアログで選択された。Path={selectedPath}" );
                return true;
            }

            logger.Info( "ファイルを開くダイアログがキャンセルされた。" );
            return false;
        }
        catch(COMException ex) {
            logger.Error( "ファイルを開くダイアログの表示に COM 例外が発生した。", ex );
            return false;
        }
        catch(Exception ex) {
            logger.Error( "ファイルを開くダイアログの表示中に予期しない例外が発生した。", ex );
            return false;
        }
    }
}