using InfraLoggingService = DcsTranslationTool.Infrastructure.Interfaces.ILoggingService;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// インフラ層のロギングサービスへ委譲してプレゼンテーション層の契約を満たすアダプターを提供する。
/// </summary>
/// <param name="inner">委譲先のロギングサービスを受け取る。</param>
public sealed class LoggingServiceAdapter( InfraLoggingService inner ) : ILoggingService {
    /// <inheritdoc />
    public void Trace( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => inner.Trace( message, ex, member, file, line );

    /// <inheritdoc />
    public void Debug( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => inner.Debug( message, ex, member, file, line );

    /// <inheritdoc />
    public void Info( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => inner.Info( message, ex, member, file, line );

    /// <inheritdoc />
    public void Warn( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => inner.Warn( message, ex, member, file, line );

    /// <inheritdoc />
    public void Error( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => inner.Error( message, ex, member, file, line );

    /// <inheritdoc />
    public void Fatal( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 )
        => inner.Fatal( message, ex, member, file, line );
}