namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

public interface IDialogProvider {
    /// <summary>
    /// フォルダ選択ダイアログを表示し、選択されたパスを取得する
    /// </summary>
    /// <param name="initialDirectory">初期表示ディレクトリ</param>
    /// <param name="selectedPath">選択されたパス</param>
    /// <returns>OK で閉じた場合は true</returns>
    bool ShowFolderPicker( string initialDirectory, out string selectedPath );

    /// <summary>
    /// 名前を付けて保存ダイアログを表示し、選択されたファイルパスを取得する。
    /// </summary>
    /// <param name="initialPath">初期表示ファイルパス。</param>
    /// <param name="filter">ファイル種別フィルター。</param>
    /// <param name="selectedPath">選択されたファイルパス。</param>
    /// <returns>OK で閉じた場合は true。</returns>
    bool ShowSaveFilePicker( string initialPath, string filter, out string selectedPath );

    /// <summary>
    /// ファイルを開くダイアログを表示し、選択されたファイルパスを取得する。
    /// </summary>
    /// <param name="initialPath">初期表示ファイルパス。</param>
    /// <param name="filter">ファイル種別フィルター。</param>
    /// <param name="selectedPath">選択されたファイルパス。</param>
    /// <returns>OK で閉じた場合は true。</returns>
    bool ShowOpenFilePicker( string initialPath, string filter, out string selectedPath );
}