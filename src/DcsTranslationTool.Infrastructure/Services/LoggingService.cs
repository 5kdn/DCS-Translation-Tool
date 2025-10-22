using System.Diagnostics;
using System.Text;

using DcsTranslationTool.Infrastructure.Interfaces;

using NLog;
using NLog.Config;
using NLog.Layouts;
using NLog.Targets;
using NLog.Targets.Wrappers;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// NLog を内部で利用するログ出力サービスの実装。
/// ファイルは JSON 形式（構造化ログ）、デバッグは人間可読形式で出力。
/// </summary>
public sealed class LoggingService() : ILoggingService {
    private readonly Logger _logger = LogManager.GetCurrentClassLogger();

    #region ILoggingService 実装

    /// <inheritdoc/>
    public void Trace( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => Write( LogLevel.Trace, message, ex, member, file, line );

    /// <inheritdoc/>
    public void Debug( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => Write( LogLevel.Debug, message, ex, member, file, line );

    /// <inheritdoc/>
    public void Info( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => Write( LogLevel.Info, message, ex, member, file, line );

    /// <inheritdoc/>
    public void Warn( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => Write( LogLevel.Warn, message, ex, member, file, line );

    /// <inheritdoc/>
    public void Error( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => Write( LogLevel.Error, message, ex, member, file, line );

    /// <inheritdoc/>
    public void Fatal( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => Write( LogLevel.Fatal, message, ex, member, file, line );

    #endregion

    /// <summary>
    /// ログイベントを構築し、指定レベルで NLog に渡す。
    /// 呼び出し元情報（メソッド名・ファイル名・行番号）はプロパティとして含む。
    /// </summary>
    private void Write( LogLevel level, string message, Exception? ex, string? member, string? file, int line ) {
        if(!_logger.IsEnabled( level )) return;

        var evt = new LogEventInfo(level, _logger.Name, message) { Exception = ex };
        // 空文字でも必ず message を出力させるためにイベントプロパティへ格納
        evt.Properties["message"] = message ?? string.Empty;
        if(!string.IsNullOrEmpty( member )) evt.Properties["member"] = member!;
        if(!string.IsNullOrEmpty( file )) evt.Properties["file"] = Path.GetFileName( file );
        evt.Properties["line"] = line;
        // 発行元クラス名を logger として記録
        var callerType = ResolveCallerType();
        if(!string.IsNullOrEmpty( callerType )) evt.Properties["logger"] = callerType!;
        _logger.Log( evt );
    }

    /// <summary>
    /// <see cref="LoggingOptions"/> から NLog の構成を生成。
    /// JSON形式で構造化ログを出力し、デバッグ出力も設定する。
    /// </summary>
    public static LoggingConfiguration BuildConfiguration( LoggingOptions options ) {
        Directory.CreateDirectory( options.LogDirectory );

        var config = new LoggingConfiguration();

        // ファイル出力ターゲット（JSON構造化ログ）
        var fileTarget = new FileTarget("file")
        {
            FileName = $"{options.LogDirectory}/{options.FileName}",
            KeepFileOpen = true ,
            Layout = new JsonLayout
            {
                IncludeEventProperties = false,
                Attributes = {
                    new JsonAttribute("timestamp", "${date:universalTime=true:format=o}"),
                    new JsonAttribute("level", "${level:uppercase=true}"),
                    new JsonAttribute("logger", "${event-properties:item=logger}"),
                    new JsonAttribute("message", "${event-properties:item=message}", true){IncludeEmptyValue = true},
                    new JsonAttribute("exception", "${exception:format=ToString:innerFormat=ToString}"),
                    new JsonAttribute("member", "${event-properties:item=member}", true),
                    new JsonAttribute("file", "${event-properties:item=file}", true),
                    new JsonAttribute("line", "${event-properties:item=line}", false)
                },
                RenderEmptyObject = false,
                SuppressSpaces = true
            },
            ArchiveEvery = options.ArchiveEvery,
            ArchiveAboveSize = options.ArchiveAboveSizeMB * 1024 * 1024,
            MaxArchiveFiles = options.MaxArchiveFiles,
            ArchiveSuffixFormat = options.ArchiveSuffixFormat,
            Encoding = Encoding.UTF8
        };

        // デバッグコンソール出力ターゲット
        var debugTarget = new DebuggerTarget("debug")
        {
            Layout = options.DebugLayout
        };

        // 非同期化
        var asyncFile = new AsyncTargetWrapper( fileTarget )
        {
            QueueLimit = 10000,
            OverflowAction = AsyncTargetWrapperOverflowAction.Block
        };
        var asyncDebug = new AsyncTargetWrapper( debugTarget )
        {
            QueueLimit = 2000,
            OverflowAction = AsyncTargetWrapperOverflowAction.Discard
        };

        config.AddTarget( asyncFile );
        config.AddTarget( asyncDebug );

        // 出力ルール
        config.AddRule( options.MinLevel, LogLevel.Fatal, asyncFile );
        if(options.EnableDebugOutput)
            config.AddRule( LogLevel.Debug, LogLevel.Fatal, asyncDebug );

        return config;
    }

    private static string? ResolveCallerType() {
        var st = new StackTrace(skipFrames: 2, fNeedFileInfo: false);
        for(var i = 0; i < st.FrameCount; i++) {
            var m = st.GetFrame(i)?.GetMethod();
            var dt = m?.DeclaringType;
            if(dt == null) continue;
            if(dt == typeof( LoggingService )) continue;
            return dt.FullName;
        }
        return null;
    }
}

/// <summary>
/// ログ出力全体の設定値を保持する構成オブジェクト。
/// </summary>
public sealed class LoggingOptions {
    /// <summary>ログファイル出力先ディレクトリ。</summary>
    public string LogDirectory { get; init; } = "Logs";

    /// <summary>ログファイル名。</summary>
    public string FileName { get; init; } = "app.log";

    /// <summary>最低出力レベル。</summary>
    public LogLevel MinLevel { get; init; } = LogLevel.Info;

    /// <summary>デバッグコンソールへの出力を有効にするか。</summary>
    public bool EnableDebugOutput { get; init; } = true;

    /// <summary>日次ローテーション設定。</summary>
    public FileArchivePeriod ArchiveEvery { get; init; } = FileArchivePeriod.Day;

    /// <summary>ファイルサイズ上限（MB）。超過時にローテーション。</summary>
    public int ArchiveAboveSizeMB { get; init; } = 10;

    /// <summary>最大アーカイブ数。</summary>
    public int MaxArchiveFiles { get; init; } = 14;

    /// <summary>
    /// アーカイブ時のサフィックス書式。
    /// </summary>
    public string ArchiveSuffixFormat { get; init; } = "${shortdate}.{#}";

    /// <summary>デバッグ出力用レイアウト（人間可読形式）。</summary>
    public string DebugLayout { get; init; } =
        "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=ToString}" +
        " | ${callsite:fileName=true:includeSourcePath=false}:${callsite-linenumber}";
}