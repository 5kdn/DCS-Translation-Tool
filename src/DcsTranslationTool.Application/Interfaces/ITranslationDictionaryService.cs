using DcsTranslationTool.Application.Models;

using FluentResults;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// アーカイブ内 dictionary の読込機能を提供するサービスである。
/// </summary>
public interface ITranslationDictionaryService {
    /// <summary>
    /// アーカイブから dictionary を読み込む。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>dictionary 項目一覧を含む結果。</returns>
    Result<IReadOnlyList<TranslationDictionaryItem>> LoadDictionary( string archiveFullPath );
}