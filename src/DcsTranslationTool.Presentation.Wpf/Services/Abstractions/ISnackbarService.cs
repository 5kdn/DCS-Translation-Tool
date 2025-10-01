using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// Snackbar を表示するサービスのインターフェース。
/// </summary>
public interface ISnackbarService {
    /// <summary>
    /// Snackbar のメッセージキューを取得するプロパティ。
    /// </summary>
    ISnackbarMessageQueue MessageQueue { get; }

    /// <summary>
    /// メッセージを表示する。
    /// </summary>
    /// <param name="message">表示するメッセージ</param>
    /// <param name="actionContent">アクションボタンの表示文字列。表示しない場合は null を指定する</param>
    /// <param name="actionHandler">
    /// アクションボタン押下時に実行する処理。アクションボタンを表示しない場合は null を指定する
    /// </param>
    /// <param name="actionArgument">アクション処理に渡す引数。アクションボタンを表示しない場合は null を指定する</param>
    /// <param name="duration">表示時間。指定しない場合は既定値である 3 秒が適用される</param>
    void Show(
        string message,
        string? actionContent = null,
        Action? actionHandler = null,
        object? actionArgument = null,
        TimeSpan? duration = null );

    /// <summary>
    /// メッセージキューをすべて削除する。
    /// </summary>
    void Clear();
}