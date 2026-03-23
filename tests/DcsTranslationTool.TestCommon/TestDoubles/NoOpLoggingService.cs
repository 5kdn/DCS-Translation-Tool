using DcsTranslationTool.Application.Interfaces;

namespace DcsTranslationTool.TestCommon.TestDoubles;

/// <summary>
/// 何も出力しないテスト用ロガーを提供する。
/// </summary>
public sealed class NoOpLoggingService : ILoggingService {
    /// <summary>
    /// 共有インスタンスを取得する。
    /// </summary>
    public static ILoggingService Instance { get; } = new NoOpLoggingService();

    private NoOpLoggingService() { }

    /// <summary>
    /// トレースログ出力を無視する。
    /// </summary>
    public void Trace( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    /// <summary>
    /// デバッグログ出力を無視する。
    /// </summary>
    public void Debug( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    /// <summary>
    /// 情報ログ出力を無視する。
    /// </summary>
    public void Info( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    /// <summary>
    /// 警告ログ出力を無視する。
    /// </summary>
    public void Warn( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    /// <summary>
    /// エラーログ出力を無視する。
    /// </summary>
    public void Error( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    /// <summary>
    /// 致命ログ出力を無視する。
    /// </summary>
    public void Fatal( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
}