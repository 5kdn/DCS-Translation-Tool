namespace DcsTranslationTool.Application.Models;

/// <summary>
/// Lua 構文検証対象ファイルを表す。
/// </summary>
/// <param name="FilePath">表示用のファイルパスを示す。</param>
/// <param name="Content">検証対象テキストを示す。</param>
public sealed record LuaSyntaxValidationTarget( string FilePath, string Content );