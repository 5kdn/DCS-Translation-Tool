using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Tests.TestDoubles;

/// <summary>
/// 何も出力しないテスト用ロガーを提供する。
/// </summary>
public sealed class NoOpLoggingService : ILoggingService {
    public static ILoggingService Instance { get; } = new NoOpLoggingService();

    private NoOpLoggingService() { }

    public void Trace( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    public void Debug( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    public void Info( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    public void Warn( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    public void Error( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }

    public void Fatal( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
}