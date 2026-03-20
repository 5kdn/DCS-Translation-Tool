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
    /// <param name="snapshot">現在の編集状態のスナップショット。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ExportDictionaryAsync( string archiveFullPath, TranslationCreationDocumentSnapshot snapshot, CancellationToken cancellationToken = default );

    /// <summary>
    /// 現在の編集結果を PO ファイルとして書き出す。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="snapshot">現在の編集状態のスナップショット。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ExportPoAsync( string archiveFullPath, TranslationCreationDocumentSnapshot snapshot, CancellationToken cancellationToken = default );

    /// <summary>
    /// 現在の編集結果を CSV ファイルとして書き出す。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="snapshot">現在の編集状態のスナップショット。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ExportCsvAsync( string archiveFullPath, TranslationCreationDocumentSnapshot snapshot, CancellationToken cancellationToken = default );

    /// <summary>
    /// PO ファイルを読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="importContext">現在の取り込み対象状態。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ImportPoAsync( string archiveFullPath, TranslationCreationImportContext importContext, CancellationToken cancellationToken = default );

    /// <summary>
    /// dictionary ファイルを読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="importContext">現在の取り込み対象状態。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ImportDictionaryAsync( string archiveFullPath, TranslationCreationImportContext importContext, CancellationToken cancellationToken = default );

    /// <summary>
    /// CSV ファイルを読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="importContext">現在の取り込み対象状態。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ImportCsvAsync( string archiveFullPath, TranslationCreationImportContext importContext, CancellationToken cancellationToken = default );

    /// <summary>
    /// 埋め込み JP dictionary を読み込んで現在の編集状態へ反映する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="rows">現在表示中の行一覧。</param>
    /// <param name="japaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ImportJapaneseDictionaryAsync(
        string archiveFullPath,
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default );
}