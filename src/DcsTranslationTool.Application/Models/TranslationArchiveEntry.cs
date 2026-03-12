using DcsTranslationTool.Application.Enums;

namespace DcsTranslationTool.Application.Models;

/// <summary>
/// 翻訳対象アーカイブの一覧項目を表す。
/// </summary>
/// <param name="Name">アーカイブ名。</param>
/// <param name="FullPath">アーカイブの絶対パス。</param>
/// <param name="RelativePath">カテゴリルートからの相対パス。</param>
/// <param name="Category">所属カテゴリ。</param>
/// <param name="ArchiveType">アーカイブ種別。</param>
/// <param name="HasDictionary">dictionary エントリを保持しているかどうか。</param>
public sealed record TranslationArchiveEntry(
    string Name,
    string FullPath,
    string RelativePath,
    TranslationArchiveCategory Category,
    TranslationArchiveType ArchiveType,
    bool HasDictionary
);