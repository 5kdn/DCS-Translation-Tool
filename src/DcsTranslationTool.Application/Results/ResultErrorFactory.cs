using FluentResults;

namespace DcsTranslationTool.Application.Results;

/// <summary>
/// 分類付き Result エラーを生成する。
/// </summary>
public static class ResultErrorFactory {
    /// <summary>
    /// Validation エラーを生成する。
    /// </summary>
    public static IError Validation( string message, string? code = null, Exception? exception = null ) =>
        new ResultError( ResultErrorKind.Validation, message, code, exception );

    /// <summary>
    /// NotFound エラーを生成する。
    /// </summary>
    public static IError NotFound( string message, string? code = null, Exception? exception = null ) =>
        new ResultError( ResultErrorKind.NotFound, message, code, exception );

    /// <summary>
    /// Conflict エラーを生成する。
    /// </summary>
    public static IError Conflict( string message, string? code = null, Exception? exception = null ) =>
        new ResultError( ResultErrorKind.Conflict, message, code, exception );

    /// <summary>
    /// External エラーを生成する。
    /// </summary>
    public static IError External( string message, string? code = null, Exception? exception = null ) =>
        new ResultError( ResultErrorKind.External, message, code, exception );

    /// <summary>
    /// Unexpected エラーを生成する。
    /// </summary>
    public static IError Unexpected( Exception exception, string? code = null ) =>
        new ResultError( ResultErrorKind.Unexpected, exception.Message, code, exception );
}
