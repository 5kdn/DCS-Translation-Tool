namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// ダイアログの完了状態と結果値を表すユーティリティ型。
/// </summary>
/// <typeparam name="T">結果の値の型。</typeparam>
public readonly record struct DialogResult<T> {
    /// <summary>
    /// OKで閉じられたかどうか。
    /// </summary>
    public bool IsOk { get; init; }

    /// <summary>
    /// キャンセルで閉じられたかどうか。
    /// </summary>
    public bool IsCanceled { get; init; }

    /// <summary>
    /// エラーが発生した場合の例外。正常終了時は <see langword="null"/>。
    /// </summary>
    public Exception? Error { get; init; }

    /// <summary>
    /// OK時の結果値。Cancelやエラー時は既定値。
    /// </summary>
    public T? Value { get; init; }

    /// <summary>
    /// OK結果を生成する。
    /// </summary>
    /// <param name="value">結果値</param>
    /// <returns>OKの <see cref="DialogResult{T}"/></returns>
    public static DialogResult<T> Ok( T value ) => new()
    {
        IsOk = true,
        IsCanceled = false,
        Error = null,
        Value = value
    };

    /// <summary>
    /// キャンセル結果を生成する。
    /// </summary>
    /// <returns>Cancelの <see cref="DialogResult{T}"/></returns>
    public static DialogResult<T> Canceled() => new()
    {
        IsOk = false,
        IsCanceled = true,
        Error = null,
        Value = default
    };

    /// <summary>
    /// エラー結果を生成する。
    /// </summary>
    /// <param name="exception">発生した例外。<see langword="null"/>は不可</param>
    /// <returns>エラーの <see cref="DialogResult{T}"/></returns>
    /// <exception cref="ArgumentNullException"><paramref name="exception"/> が <see langword="null"/> の場合</exception>
    public static DialogResult<T> FromError( Exception exception ) {
        ArgumentNullException.ThrowIfNull( exception );
        return new DialogResult<T>
        {
            IsOk = false,
            IsCanceled = false,
            Error = exception,
            Value = default
        };
    }
}