using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// Lua 構文検証を提供するサービスを表す。
/// </summary>
public interface ILuaSyntaxValidationService {
    /// <summary>
    /// 指定したファイル群の Lua 構文を検証する。
    /// </summary>
    /// <param name="targets">検証対象のファイル群。</param>
    /// <returns>構文検証結果を返す。</returns>
    LuaSyntaxValidationResult Validate( IReadOnlyList<LuaSyntaxValidationTarget> targets );
}