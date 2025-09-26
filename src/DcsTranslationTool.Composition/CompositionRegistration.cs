using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Providers;
using DcsTranslationTool.Infrastructure.Services;

using NLog;
using NLog.Targets;

namespace DcsTranslationTool.Composition;

/// <summary>
/// DI登録と横断的初期化を担う接着層。
/// </summary>
public static class CompositionRegistration {
    /// <summary>
    /// Infrastructure のサービス登録を一括実行。
    /// </summary>
    public static void Register( SimpleContainer c ) {
        var loggingOptions = CreateLoggingOptions();
        NLog.LogManager.Configuration = LoggingService.BuildConfiguration( loggingOptions );
        var loggingService = new LoggingService();
        c.Instance<ILoggingService>( loggingService );

        c.Singleton<IApiService, ApiService>();
        c.Singleton<IFileEntryService, FileEntryService>();
        c.Singleton<IFileService, FileService>();
        c.Singleton<IZipService, ZipService>();
        c.Singleton<IEnvironmentProvider, EnvironmentProvider>();
    }

    /// <summary>
    /// アプリケーションのロギング設定を生成する。
    /// </summary>
    /// <returns>
    /// 生成した <see cref="LoggingOptions"/> のインスタンスを返す。
    /// </returns>
    private static LoggingOptions CreateLoggingOptions() {
#if DEBUG
        var logDirectory = ResolveLogDirectory();
        return new LoggingOptions
        {
            MinLevel = LogLevel.Debug,
            EnableDebugOutput = true,
            LogDirectory = logDirectory,
            FileName = "app.debug.log",
            ArchiveEvery = FileArchivePeriod.Hour,
            ArchiveAboveSizeMB = 50,
            MaxArchiveFiles = 3,
            ArchiveSuffixFormat = "${shortdate}.{#}"
        };
#else
        var logDirectory = ResolveLogDirectory();
        return new LoggingOptions {
            MinLevel = LogLevel.Info,
            EnableDebugOutput = false,
            LogDirectory = logDirectory,
            FileName = "app.log",
            ArchiveEvery = FileArchivePeriod.Day,
            ArchiveAboveSizeMB = 3,
            MaxArchiveFiles = 5,
            ArchiveSuffixFormat = "${shortdate}.{#}"
        };
#endif
    }

    /// <summary>
    /// ログ出力先ディレクトリを実行ファイルと同階層に解決する。
    /// </summary>
    /// <returns>ログディレクトリの絶対パス。</returns>
    private static string ResolveLogDirectory() {
        var processPath = Environment.ProcessPath;
        var baseDir = !string.IsNullOrEmpty( processPath )
            ? Path.GetDirectoryName( processPath )
            : null;
        baseDir ??= AppContext.BaseDirectory;
        return Path.GetFullPath( Path.Combine( baseDir, "Logs" ) );
    }
}