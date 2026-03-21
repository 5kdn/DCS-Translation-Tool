namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// Translation File Selection から起動する外部操作を提供するサービス契約を表す。
/// </summary>
public interface ITranslationFileSelectionActionService {
    /// <summary>
    /// 指定パスに対応するフォルダーを開く。
    /// </summary>
    /// <param name="path">対象パス。</param>
    void OpenDirectory( string path );

    /// <summary>
    /// 指定アーカイブを対象に翻訳作成ウィンドウを表示する。
    /// </summary>
    /// <param name="archiveFullPath">翻訳対象アーカイブの絶対パス。</param>
    /// <returns>非同期タスク。</returns>
    Task OpenTranslationCreationAsync( string archiveFullPath );

    /// <summary>
    /// 通知キューをクリアする。
    /// </summary>
    void ClearNotifications();
}