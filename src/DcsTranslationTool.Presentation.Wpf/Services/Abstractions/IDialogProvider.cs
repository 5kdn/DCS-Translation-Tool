namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

public interface IDialogProvider {
    /// <summary>
    /// フォルダ選択ダイアログを表示し、選択されたパスを取得する
    /// </summary>
    /// <param name="initialDirectory">初期表示ディレクトリ</param>
    /// <param name="selectedPath">選択されたパス</param>
    /// <returns>OK で閉じた場合は true</returns>
    bool ShowFolderPicker( string initialDirectory, out string selectedPath );
}