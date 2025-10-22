using System.Reflection;

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
        var assembly = Assembly.GetEntryAssembly() ?? throw new InvalidOperationException( "エントリーアセンブリを取得できませんでした。" );
        string? title;
        try {
            title = assembly.GetCustomAttribute<AssemblyTitleAttribute>()?.Title;
        }
        catch(Exception ex) {
            throw new InvalidOperationException( "AssemblyTitleAttribute の取得に失敗しました。", ex );
        }
        var saveDir = Path.Combine(
                Environment.GetFolderPath( Environment.SpecialFolder.ApplicationData ),
                title ?? "DcsTranslationTool"
            );
        const string fileName = "appsettings.json";
        var loggingOptions = CreateLoggingOptions();
        NLog.LogManager.Configuration = LoggingService.BuildConfiguration( loggingOptions );
        var loggingService = new LoggingService();
        c.Instance<ILoggingService>( loggingService );
        var appSettingsService = new AppSettingsService( loggingService, saveDir, fileName );
        c.Instance<IAppSettingsService>( appSettingsService );

        c.Singleton<IApiService, ApiService>();
        c.Singleton<IFileEntryService, FileEntryService>();
        c.Singleton<IFileService, FileService>();
        c.Singleton<IZipService, ZipService>();

        c.Singleton<IApplicationInfoService, ApplicationInfoService>();
        c.Singleton<IEnvironmentProvider, EnvironmentProvider>();
        c.Singleton<ISystemService, SystemService>();
    }

    /// <summary>
    /// アプリケーションのロギング設定を生成する。
    /// </summary>
    /// <returns>
    /// 生成した <see cref="LoggingOptions"/> のインスタンスを返す。
    /// </returns>
    private static LoggingOptions CreateLoggingOptions() {
#if DEBUG
        return new LoggingOptions
        {
            MinLevel = LogLevel.Debug,
            EnableDebugOutput = true,
            LogDirectory = "Logs",
            FileName = "app.debug.log",
            ArchiveEvery = FileArchivePeriod.Hour,
            ArchiveAboveSizeMB = 50,
            MaxArchiveFiles = 3,
            ArchiveSuffixFormat = "${shortdate}.{#}"
        };
#else
            return new LoggingOptions {
                MinLevel = LogLevel.Info,
                EnableDebugOutput = false,
                LogDirectory = "Logs",
                FileName = "app.log",
                ArchiveEvery = FileArchivePeriod.Day,
                ArchiveAboveSizeMB = 3,
                MaxArchiveFiles = 5,
                ArchiveSuffixFormat = "${shortdate}.{#}"
            };
#endif
    }
}
