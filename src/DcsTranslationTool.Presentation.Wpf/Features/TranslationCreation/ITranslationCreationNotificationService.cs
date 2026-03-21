using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 画面の通知表示を提供する。
/// </summary>
public interface ITranslationCreationNotificationService {
    /// <summary>
    /// 画面専用の Snackbar メッセージキューを取得する。
    /// </summary>
    SnackbarMessageQueue MessageQueue { get; }

    /// <summary>
    /// 操作完了メッセージを表示する。
    /// </summary>
    /// <param name="message">表示メッセージ。</param>
    void ShowCompleted( string message );

    /// <summary>
    /// 書き出し成功通知を表示する。
    /// </summary>
    /// <param name="exportPath">書き出し先パス。</param>
    void ShowExportSucceeded( string exportPath );
}