using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の読込と入出力ワークフローを提供する。
/// </summary>
public interface ITranslationCreationWorkflowService {
    /// <summary>
    /// アーカイブから dictionary を読み込む。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>読込結果を返す。</returns>
    Task<TranslationCreationLoadResult> LoadAsync( string archiveFullPath, CancellationToken cancellationToken = default );

    /// <summary>
    /// 読み込み形式に応じた取り込み処理を実行する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="format">取り込み形式。</param>
    /// <param name="importContext">現在の編集状態。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ImportAsync(
        string archiveFullPath,
        TranslationCreationImportFormat format,
        TranslationCreationImportContext importContext,
        CancellationToken cancellationToken = default );

    /// <summary>
    /// 書き出し形式に応じた書き出し処理を実行する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="format">書き出し形式。</param>
    /// <param name="snapshot">現在の編集状態のスナップショット。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ExportAsync(
        string archiveFullPath,
        TranslationCreationExportFormat format,
        TranslationCreationDocumentSnapshot snapshot,
        CancellationToken cancellationToken = default );

    /// <summary>
    /// 埋め込み JP dictionary の初期 plan を生成する。
    /// </summary>
    /// <param name="importContext">現在の編集状態。</param>
    /// <param name="hasJapaneseDictionary">埋め込み JP dictionary が存在するかどうか。</param>
    /// <param name="japaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
    /// <returns>初期確認 plan を返す。</returns>
    TranslationCreationInitialPromptPlan CreateInitialPromptPlan(
        TranslationCreationImportContext importContext,
        bool hasJapaneseDictionary,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems );

    /// <summary>
    /// 埋め込み JP dictionary の取り込み処理を実行する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="importContext">現在の編集状態。</param>
    /// <param name="japaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>操作結果を返す。</returns>
    Task<TranslationCreationCommandResult> ImportEmbeddedJapaneseDictionaryAsync(
        string archiveFullPath,
        TranslationCreationImportContext importContext,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default );
}

/// <summary>
/// TranslationCreation の読込と入出力ワークフローを実装する。
/// </summary>
/// <param name="dictionaryLoader">dictionary 読込前処理。</param>
/// <param name="importExportService">入出力サービス。</param>
/// <param name="logger">ロギングサービス。</param>
public sealed class TranslationCreationWorkflowService(
    TranslationCreationDictionaryLoader dictionaryLoader,
    ITranslationCreationImportExportService importExportService,
    ILoggingService logger ) : ITranslationCreationWorkflowService {
    /// <inheritdoc />
    public async Task<TranslationCreationLoadResult> LoadAsync( string archiveFullPath, CancellationToken cancellationToken = default ) {
        cancellationToken.ThrowIfCancellationRequested();
        logger.Info( $"TranslationCreationWorkflowService の dictionary 読込を開始する。Archive={archiveFullPath}" );

        try {
            var result = await Task.Run(
                () => dictionaryLoader.LoadArchiveDictionaryState( archiveFullPath ),
                cancellationToken ).ConfigureAwait( false );

            if(result.IsFailed) {
                logger.Warn( $"TranslationCreationWorkflowService の dictionary 読込に失敗した。Archive={archiveFullPath}" );
                return TranslationCreationLoadResult.Failed();
            }

            var statusMessage = result.Value.LoadState.RowStates.Count == 0
                ? Strings_Translation.CreateTranslationDictionaryEmptyMessage
                : string.Empty;
            logger.Info( $"TranslationCreationWorkflowService の dictionary 読込を終了する。Archive={archiveFullPath}, Count={result.Value.LoadState.RowStates.Count}" );
            return TranslationCreationLoadResult.Succeeded(
                result.Value.LoadState,
                result.Value.HasJapaneseDictionary,
                result.Value.JapaneseDictionaryItems,
                statusMessage );
        }
        catch(Exception ex) {
            logger.Error( $"TranslationCreationWorkflowService の dictionary 読込中に例外が発生した。Archive={archiveFullPath}", ex );
            return TranslationCreationLoadResult.Failed();
        }
    }

    /// <inheritdoc />
    public Task<TranslationCreationCommandResult> ImportAsync(
        string archiveFullPath,
        TranslationCreationImportFormat format,
        TranslationCreationImportContext importContext,
        CancellationToken cancellationToken = default ) =>
        format switch
        {
            TranslationCreationImportFormat.Dictionary => importExportService.ImportDictionaryAsync( archiveFullPath, importContext, cancellationToken ),
            TranslationCreationImportFormat.Csv => importExportService.ImportCsvAsync( archiveFullPath, importContext, cancellationToken ),
            _ => importExportService.ImportPoAsync( archiveFullPath, importContext, cancellationToken )
        };

    /// <inheritdoc />
    public Task<TranslationCreationCommandResult> ExportAsync(
        string archiveFullPath,
        TranslationCreationExportFormat format,
        TranslationCreationDocumentSnapshot snapshot,
        CancellationToken cancellationToken = default ) =>
        format switch
        {
            TranslationCreationExportFormat.Po => importExportService.ExportPoAsync( archiveFullPath, snapshot, cancellationToken ),
            TranslationCreationExportFormat.Csv => importExportService.ExportCsvAsync( archiveFullPath, snapshot, cancellationToken ),
            _ => importExportService.ExportDictionaryAsync( archiveFullPath, snapshot, cancellationToken )
        };

    /// <inheritdoc />
    public TranslationCreationInitialPromptPlan CreateInitialPromptPlan(
        TranslationCreationImportContext importContext,
        bool hasJapaneseDictionary,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems ) {
        if(!hasJapaneseDictionary || importContext.Rows.Count == 0) {
            return TranslationCreationInitialPromptPlan.None;
        }

        if(japaneseDictionaryItems.Count == 0) {
            return new TranslationCreationInitialPromptPlan( false, false, [] );
        }

        return new TranslationCreationInitialPromptPlan( true, true, japaneseDictionaryItems );
    }

    /// <inheritdoc />
    public Task<TranslationCreationCommandResult> ImportEmbeddedJapaneseDictionaryAsync(
        string archiveFullPath,
        TranslationCreationImportContext importContext,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default ) =>
        importExportService.ImportJapaneseDictionaryAsync(
            archiveFullPath,
            importContext.Rows,
            japaneseDictionaryItems,
            cancellationToken );
}

/// <summary>
/// TranslationCreation の読込結果を表す。
/// </summary>
/// <param name="IsSuccess">読込に成功したかどうか。</param>
/// <param name="LoadState">読込済み状態。</param>
/// <param name="HasJapaneseDictionary">埋め込み JP dictionary を含むかどうか。</param>
/// <param name="JapaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
/// <param name="StatusMessage">画面へ反映する状態メッセージ。</param>
public sealed record TranslationCreationLoadResult(
    bool IsSuccess,
    TranslationCreationDictionaryLoadState LoadState,
    bool HasJapaneseDictionary,
    IReadOnlyList<TranslationDictionaryItem> JapaneseDictionaryItems,
    string StatusMessage ) {
    /// <summary>
    /// 読込成功結果を生成する。
    /// </summary>
    /// <param name="loadState">読込済み状態。</param>
    /// <param name="hasJapaneseDictionary">埋め込み JP dictionary を含むかどうか。</param>
    /// <param name="japaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
    /// <param name="statusMessage">状態メッセージ。</param>
    /// <returns>生成した結果を返す。</returns>
    public static TranslationCreationLoadResult Succeeded(
        TranslationCreationDictionaryLoadState loadState,
        bool hasJapaneseDictionary,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        string statusMessage ) =>
        new( true, loadState, hasJapaneseDictionary, japaneseDictionaryItems, statusMessage );

    /// <summary>
    /// 読込失敗結果を生成する。
    /// </summary>
    /// <returns>生成した結果を返す。</returns>
    public static TranslationCreationLoadResult Failed() =>
        new(
            false,
            new TranslationCreationDictionaryLoadState( [], [] ),
            false,
            [],
            Strings_Translation.CreateTranslationDictionaryLoadFailedMessage );
}

/// <summary>
/// TranslationCreation のコマンド実行結果を表す。
/// </summary>
/// <param name="IsSuccess">操作が成功したかどうか。</param>
/// <param name="WasCancelled">ユーザー操作で中断されたかどうか。</param>
/// <param name="IsPartial">部分適用かどうか。</param>
/// <param name="AppliedCount">適用件数。</param>
/// <param name="StatusMessage">画面へ反映する状態メッセージ。</param>
/// <param name="NotificationKind">表示する通知種別。</param>
/// <param name="OutputPath">通知に紐づく出力先パス。</param>
public sealed record TranslationCreationCommandResult(
    bool IsSuccess,
    bool WasCancelled,
    bool IsPartial,
    int AppliedCount,
    string? StatusMessage,
    TranslationCreationNotificationKind NotificationKind = TranslationCreationNotificationKind.None,
    string? OutputPath = null );

/// <summary>
/// TranslationCreation の通知種別を表す。
/// </summary>
public enum TranslationCreationNotificationKind {
    /// <summary>
    /// 通知不要を表す。
    /// </summary>
    None,

    /// <summary>
    /// 完了メッセージ通知を表す。
    /// </summary>
    Completed,

    /// <summary>
    /// 書き出し成功通知を表す。
    /// </summary>
    ExportSucceeded,
}

/// <summary>
/// 埋め込み JP dictionary 初期確認の plan を表す。
/// </summary>
/// <param name="RequiresEmbeddedJapaneseDictionaryPrompt">起動時 prompt が必要かどうか。</param>
/// <param name="CanImportEmbeddedJapaneseDictionary">埋め込み JP dictionary を取り込み可能かどうか。</param>
/// <param name="JapaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
public sealed record TranslationCreationInitialPromptPlan(
    bool RequiresEmbeddedJapaneseDictionaryPrompt,
    bool CanImportEmbeddedJapaneseDictionary,
    IReadOnlyList<TranslationDictionaryItem> JapaneseDictionaryItems ) {
    /// <summary>
    /// 何も行わない plan を表す。
    /// </summary>
    public static TranslationCreationInitialPromptPlan None { get; } = new( false, false, [] );
}

/// <summary>
/// 埋め込み JP dictionary の起動時選択を表す。
/// </summary>
public enum TranslationCreationEmbeddedJapaneseDictionaryStartupChoice {
    /// <summary>
    /// 埋め込み JP dictionary を取り込む選択を表す。
    /// </summary>
    Import,

    /// <summary>
    /// 埋め込み JP dictionary を取り込まず継続する選択を表す。
    /// </summary>
    ContinueWithoutImport,

    /// <summary>
    /// TranslationCreation を閉じる選択を表す。
    /// </summary>
    Close,
}

/// <summary>
/// TranslationCreation の書き出しスナップショットを表す。
/// </summary>
/// <param name="Items">現在の dictionary 項目一覧。</param>
public sealed record TranslationCreationDocumentSnapshot(
    IReadOnlyList<TranslationDictionaryItem> Items );

/// <summary>
/// TranslationCreation の取り込み対象状態を表す。
/// </summary>
/// <param name="Rows">現在表示中の行一覧。</param>
/// <param name="HasAnyTranslatedText">既存翻訳文を保持しているかどうか。</param>
public sealed record TranslationCreationImportContext(
    IReadOnlyList<TranslationDictionaryItemRowViewModel> Rows,
    bool HasAnyTranslatedText );

/// <summary>
/// TranslationCreation の取り込み形式を表す。
/// </summary>
public enum TranslationCreationImportFormat {
    /// <summary>
    /// dictionary 形式を表す。
    /// </summary>
    Dictionary,

    /// <summary>
    /// PO 形式を表す。
    /// </summary>
    Po,

    /// <summary>
    /// CSV 形式を表す。
    /// </summary>
    Csv,
}

/// <summary>
/// TranslationCreation の書き出し形式を表す。
/// </summary>
public enum TranslationCreationExportFormat {
    /// <summary>
    /// dictionary 形式を表す。
    /// </summary>
    Dictionary,

    /// <summary>
    /// PO 形式を表す。
    /// </summary>
    Po,

    /// <summary>
    /// CSV 形式を表す。
    /// </summary>
    Csv,
}