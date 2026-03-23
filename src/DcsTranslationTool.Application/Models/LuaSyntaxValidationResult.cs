namespace DcsTranslationTool.Application.Models;

/// <summary>
/// Lua 構文検証結果を表す。
/// </summary>
/// <param name="Failures">検証失敗一覧を示す。</param>
public sealed record LuaSyntaxValidationResult( IReadOnlyList<LuaSyntaxValidationFailure> Failures ) {
    /// <summary>
    /// 構文検証が成功したかどうかを取得する。
    /// </summary>
    public bool IsSuccess => Failures.Count == 0;
}