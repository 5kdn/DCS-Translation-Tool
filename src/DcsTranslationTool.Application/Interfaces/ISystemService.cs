namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// システム操作を行うサービスインターフェース。
/// </summary>
public interface ISystemService {
    /// <summary>
    /// 指定したテキストをクリップボードへ設定する。
    /// </summary>
    /// <param name="text">クリップボードへ設定するテキスト。</param>
    void SetClipboardText( string text );

    /// <summary>
    /// 既定のブラウザーで URL を開く。
    /// </summary>
    /// <param name="url">開くURL</param>
    void OpenInWebBrowser( string url );

    /// <summary>
    /// 指定されたパスをエクスプローラーで開く。
    /// パスがディレクトリでない場合は、ファイルが存在する場合にその親ディレクトリを開く。
    /// </summary>
    /// <param name="path">開く対象のディレクトリまたはファイルのパス</param>
    /// <exception cref="DirectoryNotFoundException">
    /// 指定されたパスが存在せず、またファイルの親ディレクトリも取得できなかった場合に発生する
    /// </exception>
    /// <exception cref="System.ComponentModel.Win32Exception">
    /// エクスプローラーの起動に失敗した場合に発生する
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// プロセスオブジェクトが既に破棄されている場合に発生する
    /// </exception>
    void OpenDirectory( string path );

    /// <summary>
    /// 現在日時を取得する。
    /// </summary>
    /// <returns>現在日時を返す。</returns>
    DateTimeOffset GetCurrentDateTimeOffset();
}