using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// ファイル監視のライフサイクルを管理する。
/// </summary>
public sealed class FileEntryWatcherLifecycle(
    IAppSettingsService appSettingsService,
    IFileEntryService fileEntryService,
    ILoggingService logger
) : IFileEntryWatcherLifecycle {
    private readonly object _gate = new();
    private int _watchingClientCount;
    private bool _isWatching;

    /// <inheritdoc />
    public void StartWatching() {
        lock(_gate) {
            _watchingClientCount++;
            if(_isWatching) {
                logger.Debug( $"ファイル監視は既に開始済みのため再利用する。RefCount={_watchingClientCount}" );
                return;
            }

            var watchPath = appSettingsService.Settings.TranslateFileDir;
            fileEntryService.Watch( watchPath );
            _isWatching = true;
            logger.Info( $"ファイル監視を開始した。Directory={watchPath}, RefCount={_watchingClientCount}" );
        }
    }

    /// <inheritdoc />
    public void StopWatching() {
        lock(_gate) {
            if(_watchingClientCount <= 0) {
                logger.Debug( "ファイル監視停止は既に実行済みのため処理を省略する。" );
                return;
            }

            _watchingClientCount--;
            if(_watchingClientCount > 0) {
                logger.Debug( $"他の利用者が存在するためファイル監視を継続する。RefCount={_watchingClientCount}" );
                return;
            }

            if(!_isWatching) {
                logger.Debug( "ファイル監視は開始されていないため停止処理を省略する。" );
                return;
            }

            fileEntryService.Dispose();
            _isWatching = false;
            logger.Info( "ファイル監視を停止した。" );
        }
    }
}