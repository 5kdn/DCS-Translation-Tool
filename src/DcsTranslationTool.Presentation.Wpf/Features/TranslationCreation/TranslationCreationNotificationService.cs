using System.IO;
using System.Windows.Threading;

using DcsTranslationTool.Application.Interfaces;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 画面専用の通知表示を担う。
/// </summary>
public sealed class TranslationCreationNotificationService(
    ISystemService systemService,
    ILoggingService logger ) : ITranslationCreationNotificationService {
    private SnackbarMessageQueue? _messageQueue;

    /// <inheritdoc />
    public SnackbarMessageQueue MessageQueue => _messageQueue ??= CreateMessageQueue();

    /// <inheritdoc />
    public void ShowCompleted( string message ) =>
        MessageQueue.Enqueue( message, (string?)null, null, null, false, false, null );

    /// <inheritdoc />
    public void ShowExportSucceeded( string exportPath ) {
        var exportDirectoryPath = Path.GetDirectoryName( exportPath );
        if(string.IsNullOrWhiteSpace( exportDirectoryPath )) {
            logger.Warn( $"dictionary 書き出し成功後の保存先ディレクトリ解決に失敗した。Path={exportPath}" );
            return;
        }

        MessageQueue.Enqueue(
            "書き出しが完了しました",
            "開く",
            new Action<object?>( exportDirectoryPathObject => {
                if(exportDirectoryPathObject is not string directoryPath || string.IsNullOrWhiteSpace( directoryPath )) {
                    logger.Warn( "Snackbar から渡された保存先ディレクトリが不正なため開く処理を中断する。" );
                    return;
                }

                logger.Info( $"dictionary 保存先ディレクトリを開く。Directory={directoryPath}" );
                systemService.OpenDirectory( directoryPath );
            } ),
            exportDirectoryPath,
            false,
            false,
            null );
    }

    /// <summary>
    /// 通知表示に利用するメッセージキューを生成する。
    /// </summary>
    /// <returns>生成したメッセージキューを返す。</returns>
    private static SnackbarMessageQueue CreateMessageQueue() {
        _ = Dispatcher.CurrentDispatcher;
        return new SnackbarMessageQueue();
    }
}