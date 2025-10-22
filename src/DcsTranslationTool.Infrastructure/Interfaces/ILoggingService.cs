using System.Runtime.CompilerServices;

namespace DcsTranslationTool.Infrastructure.Interfaces;

/// <summary>
/// アプリケーション全体で利用可能なログ出力サービスの契約。
/// ログレベルに応じたメッセージと例外情報を出力する。
/// </summary>
public interface ILoggingService {
    /// <summary>トレースレベルのログを出力。</summary>
    void Trace( string message, Exception? ex = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0 );

    /// <summary>デバッグレベルのログを出力。</summary>
    void Debug( string message, Exception? ex = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0 );

    /// <summary>情報レベルのログを出力。</summary>
    void Info( string message, Exception? ex = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0 );

    /// <summary>警告レベルのログを出力。</summary>
    void Warn( string message, Exception? ex = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0 );

    /// <summary>エラーレベルのログを出力。</summary>
    void Error( string message, Exception? ex = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0 );

    /// <summary>致命的エラーを出力。</summary>
    void Fatal( string message, Exception? ex = null,
        [CallerMemberName] string? member = null,
        [CallerFilePath] string? file = null,
        [CallerLineNumber] int line = 0 );
}