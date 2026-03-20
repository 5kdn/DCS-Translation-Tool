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
    /// 埋め込み JP dictionary の確認と初期取り込みを実行する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="importContext">現在の編集状態。</param>
    /// <param name="hasJapaneseDictionary">埋め込み JP dictionary が存在するかどうか。</param>
    /// <param name="japaneseDictionaryItems">埋め込み JP dictionary 項目一覧。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>初期確認結果を返す。</returns>
    Task<TranslationCreationInitialPromptResult> HandleEmbeddedJapaneseDictionaryAsync(
        string archiveFullPath,
        TranslationCreationImportContext importContext,
        bool hasJapaneseDictionary,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default );
}

/// <summary>
/// TranslationCreation の読込と入出力ワークフローを実装する。
/// </summary>
/// <param name="dictionaryLoader">dictionary 読込前処理。</param>
/// <param name="dialogService">対話操作サービス。</param>
/// <param name="importExportService">入出力サービス。</param>
/// <param name="logger">ロギングサービス。</param>
public sealed class TranslationCreationWorkflowService(
    TranslationCreationDictionaryLoader dictionaryLoader,
    ITranslationCreationDialogService dialogService,
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
    public async Task<TranslationCreationInitialPromptResult> HandleEmbeddedJapaneseDictionaryAsync(
        string archiveFullPath,
        TranslationCreationImportContext importContext,
        bool hasJapaneseDictionary,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default ) {
        if(!hasJapaneseDictionary || importContext.Rows.Count == 0) {
            return TranslationCreationInitialPromptResult.None;
        }

        cancellationToken.ThrowIfCancellationRequested();

        if(!await dialogService.ConfirmArchiveContainsJapaneseDictionaryAsync( archiveFullPath )) {
            logger.Info( $"既存JP dictionary 警告でキャンセルされたためウィンドウを閉じる。Archive={archiveFullPath}" );
            return new TranslationCreationInitialPromptResult( true, null );
        }

        if(!await dialogService.ConfirmJapaneseDictionaryImportAsync()) {
            return TranslationCreationInitialPromptResult.None;
        }

        if(japaneseDictionaryItems.Count == 0) {
            logger.Warn( $"JP dictionary の読込に失敗したため DEFAULT dictionary のみで継続する。Archive={archiveFullPath}" );
            return TranslationCreationInitialPromptResult.None;
        }

        var commandResult = await importExportService.ImportJapaneseDictionaryAsync(
            archiveFullPath,
            importContext.Rows,
            japaneseDictionaryItems,
            cancellationToken );
        return new TranslationCreationInitialPromptResult( false, commandResult );
    }
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
/// 埋め込み JP dictionary 初期確認の結果を表す。
/// </summary>
/// <param name="ShouldCloseWindow">ウィンドウを閉じる必要があるかどうか。</param>
/// <param name="CommandResult">実行した操作結果。</param>
public sealed record TranslationCreationInitialPromptResult(
    bool ShouldCloseWindow,
    TranslationCreationCommandResult? CommandResult ) {
    /// <summary>
    /// 何も行わない結果を表す。
    /// </summary>
    public static TranslationCreationInitialPromptResult None { get; } = new( false, null );
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