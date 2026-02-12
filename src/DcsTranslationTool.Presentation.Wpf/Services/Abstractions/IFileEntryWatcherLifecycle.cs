namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// ファイル監視の開始と停止を管理するサービス契約を表す。
/// </summary>
public interface IFileEntryWatcherLifecycle {
    /// <summary>
    /// ファイル監視の利用を開始する。
    /// </summary>
    void StartWatching();

    /// <summary>
    /// ファイル監視の利用を停止する。
    /// </summary>
    void StopWatching();
}