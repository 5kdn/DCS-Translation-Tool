using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の import/export 処理を提供する。
/// </summary>
public interface ITranslationCreationImportExportService {
    /// <summary>
    /// 現在の編集結果を dictionary ファイルとして書き出す。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ExportDictionaryAsync( string archiveFullPath, ITranslationCreationSession session, CancellationToken cancellationToken = default );

    /// <summary>
    /// 現在の編集結果を PO ファイルとして書き出す。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ExportPoAsync( string archiveFullPath, ITranslationCreationSession session, CancellationToken cancellationToken = default );

    /// <summary>
    /// 現在の編集結果を CSV ファイルとして書き出す。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ExportCsvAsync( string archiveFullPath, ITranslationCreationSession session, CancellationToken cancellationToken = default );

    /// <summary>
    /// PO ファイルを読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ImportPoAsync( string archiveFullPath, ITranslationCreationSession session, CancellationToken cancellationToken = default );

    /// <summary>
    /// dictionary ファイルを読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ImportDictionaryAsync( string archiveFullPath, ITranslationCreationSession session, CancellationToken cancellationToken = default );

    /// <summary>
    /// CSV ファイルを読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ImportCsvAsync( string archiveFullPath, ITranslationCreationSession session, CancellationToken cancellationToken = default );

    /// <summary>
    /// 埋め込み JP dictionary を読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="session">現在の編集セッション。</param>
    /// <param name="japaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationOperationResult> ImportJapaneseDictionaryAsync(
        string archiveFullPath,
        ITranslationCreationSession session,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default );
}