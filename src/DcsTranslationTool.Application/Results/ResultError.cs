using FluentResults;

namespace DcsTranslationTool.Application.Results;

/// <summary>
/// 分類情報を持つ Result エラーを表す。
/// </summary>
public sealed class ResultError : Error {
    /// <summary>
    /// 初期化する。
    /// </summary>
    /// <param name="kind">失敗分類。</param>
    /// <param name="message">エラーメッセージ。</param>
    /// <param name="code">任意のエラーコード。</param>
    /// <param name="exception">任意の例外情報。</param>
    public ResultError( ResultErrorKind kind, string message, string? code = null, Exception? exception = null )
        : base( message ) {
        this.Kind = kind;
        this.Metadata["kind"] = kind.ToString();
        if(!string.IsNullOrWhiteSpace( code )) {
            this.Metadata["code"] = code;
        }

        if(exception is not null) {
            this.CausedBy( exception );
        }
    }

    /// <summary>
    /// 失敗分類を取得する。
    /// </summary>
    public ResultErrorKind Kind { get; }
}
