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

    /// <summary>
    /// アーカイブから元テキストを保持した編集用 dictionary を読み込む。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <returns>編集用 dictionary を含む結果。</returns>
    Result<EditableTranslationDictionary> LoadEditableDictionary( string archiveFullPath );

    /// <summary>
    /// ローカル dictionary ファイルから項目一覧を読み込む。
    /// </summary>
    /// <param name="path">対象 dictionary ファイルパス。</param>
    /// <returns>dictionary 項目一覧を含む結果。</returns>
    Result<IReadOnlyList<TranslationDictionaryItem>> LoadDictionaryFile( string path );

    /// <summary>
    /// PO ファイルからエントリー一覧を読み込む。
    /// </summary>
    /// <param name="path">対象 PO ファイルパス。</param>
    /// <returns>PO エントリー一覧を含む結果。</returns>
    Result<IReadOnlyList<TranslationPoEntry>> LoadPo( string path );

    /// <summary>
    /// CSV ファイルからエントリー一覧を読み込む。
    /// </summary>
    /// <param name="path">対象 CSV ファイルパス。</param>
    /// <returns>CSV エントリー一覧を含む結果。</returns>
    Result<IReadOnlyList<TranslationCsvEntry>> LoadCsv( string path );

    /// <summary>
    /// dictionary 項目一覧を Lua 形式で保存する。
    /// </summary>
    /// <param name="path">保存先ファイルパス。</param>
    /// <param name="items">保存対象の dictionary 項目一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveDictionaryAsync( string path, IReadOnlyList<TranslationDictionaryItem> items, CancellationToken cancellationToken = default );

    /// <summary>
    /// dictionary 項目一覧を PO 形式で保存する。
    /// </summary>
    /// <param name="path">保存先ファイルパス。</param>
    /// <param name="items">保存対象の dictionary 項目一覧。</param>
    /// <param name="projectIdVersion">Project-Id-Version ヘッダー値。</param>
    /// <param name="potCreationDate">POT-Creation-Date ヘッダー値。</param>
    /// <param name="poRevisionDate">PO-Revision-Date ヘッダー値。</param>
    /// <param name="xGenerator">X-Generator ヘッダー値。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SavePoAsync(
        string path,
        IReadOnlyList<TranslationDictionaryItem> items,
        string projectIdVersion,
        string potCreationDate,
        string poRevisionDate,
        string xGenerator,
        CancellationToken cancellationToken = default );

    /// <summary>
    /// dictionary 項目一覧を CSV 形式で保存する。
    /// </summary>
    /// <param name="path">保存先ファイルパス。</param>
    /// <param name="items">保存対象の dictionary 項目一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveCsvAsync( string path, IReadOnlyList<TranslationDictionaryItem> items, CancellationToken cancellationToken = default );

    /// <summary>
    /// 元の dictionary 構造を保持したまま value のみを書き換えて保存する。
    /// </summary>
    /// <param name="path">保存先ファイルパス。</param>
    /// <param name="dictionary">元テキストを保持した編集用 dictionary。</param>
    /// <param name="translatedByKey">キーごとの書き出し値。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    Task SaveDictionaryAsync(
        string path,
        EditableTranslationDictionary dictionary,
        IReadOnlyDictionary<string, string> translatedByKey,
        CancellationToken cancellationToken = default );
}