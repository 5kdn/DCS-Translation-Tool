using FluentResults;

namespace DcsTranslationTool.Application.Results;

/// <summary>
/// Result エラーの分類メタデータを扱う拡張を提供する。
/// </summary>
public static class ResultErrorMetadataExtensions {
    /// <summary>
    /// 最初の失敗エラーから分類を取得する。
    /// </summary>
    /// <param name="result">対象 Result。</param>
    /// <returns>分類。取得できない場合は null。</returns>
    public static ResultErrorKind? GetFirstErrorKind( this IResultBase result ) {
        ArgumentNullException.ThrowIfNull( result );
        if(result.Errors.Count == 0) {
            return null;
        }

        return result.Errors[0].GetErrorKind();
    }

    /// <summary>
    /// エラーから分類を取得する。
    /// </summary>
    /// <param name="error">対象エラー。</param>
    /// <returns>分類。取得できない場合は null。</returns>
    public static ResultErrorKind? GetErrorKind( this IError error ) {
        ArgumentNullException.ThrowIfNull( error );
        if(error is ResultError resultError) {
            return resultError.Kind;
        }

        if(!error.Metadata.TryGetValue( "kind", out var value ) || value is null) {
            return null;
        }

        var kindText = value.ToString();
        return Enum.TryParse<ResultErrorKind>( kindText, ignoreCase: true, out var kind ) ? kind : null;
    }
}
