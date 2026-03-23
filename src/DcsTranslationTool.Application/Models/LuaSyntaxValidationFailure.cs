namespace DcsTranslationTool.Application.Models;

/// <summary>
/// Lua 構文検証失敗 1 件を表す。
/// </summary>
/// <param name="FilePath">失敗したファイルパスを示す。</param>
/// <param name="Message">失敗メッセージを示す。</param>
public sealed record LuaSyntaxValidationFailure( string FilePath, string Message );