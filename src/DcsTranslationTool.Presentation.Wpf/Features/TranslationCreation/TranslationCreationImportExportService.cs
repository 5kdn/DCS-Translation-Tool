using System.Globalization;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Resources;

using FluentResults;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の import/export 処理を担う。
/// </summary>
public sealed class TranslationCreationImportExportService(
    IAppSettingsService appSettingsService,
    IApplicationInfoService applicationInfoService,
    ITranslationDictionaryService translationDictionaryService,
    ITranslationCreationDialogService dialogService,
    ITranslationCreationPathService pathService,
    ISystemService systemService,
    ILoggingService logger ) : ITranslationCreationImportExportService {
    #region Constants

    private const string DictionaryOpenFileFilter = "dictionary|dictionary|すべてのファイル|*.*";
    private const string DictionarySaveFileFilter = "dictionary|dictionary|すべてのファイル|*.*";
    private const string CsvFileFilter = "CSV files|*.csv|すべてのファイル|*.*";
    private const string PoSaveFileFilter = "PO files|*.po|すべてのファイル|*.*";
    #endregion

    #region PublicMethods

    /// <inheritdoc />
    public async Task<TranslationCreationCommandResult> ExportDictionaryAsync( string archiveFullPath, TranslationCreationDocumentSnapshot snapshot, CancellationToken cancellationToken = default ) {
        return await ExportFileAsync(
            archiveFullPath,
            failedMessage: Strings_Translation.CreateTranslationDictionaryExportFailedMessage,
            exportPathFactory: () => pathService.GetDictionaryExportPath( appSettingsService.Settings, archiveFullPath ),
            saveFileFilter: DictionarySaveFileFilter,
            logTargetName: "dictionary",
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationDictionaryExportSucceededMessage, path ),
            saveAsync: async exportPath => {
                var editableDictionaryResult = translationDictionaryService.LoadEditableDictionary( archiveFullPath );
                if(editableDictionaryResult.IsFailed) {
                    logger.Warn( $"dictionary 書き出し用の元データ取得に失敗した。Archive={archiveFullPath}" );
                    throw new InvalidOperationException( "dictionary 書き出し用の元データ取得に失敗した。" );
                }

                var currentTranslations = snapshot.Items
                    .Where( item => item.IsEnabled )
                    .ToDictionary(
                        item => item.Key,
                        item => item.Translated,
                        StringComparer.Ordinal );

                await translationDictionaryService.SaveDictionaryAsync(
                    exportPath,
                    editableDictionaryResult.Value,
                    currentTranslations,
                    cancellationToken );
            } );
    }

    /// <inheritdoc />
    public async Task<TranslationCreationCommandResult> ExportPoAsync( string archiveFullPath, TranslationCreationDocumentSnapshot snapshot, CancellationToken cancellationToken = default ) {
        var currentTimestamp = FormatPoTimestamp( systemService.GetCurrentDateTimeOffset() );

        return await ExportFileAsync(
            archiveFullPath,
            failedMessage: Strings_Translation.CreateTranslationPoExportFailedMessage,
            exportPathFactory: () => pathService.GetPoExportPath( appSettingsService.Settings, archiveFullPath ),
            saveFileFilter: PoSaveFileFilter,
            logTargetName: "PO",
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationPoExportSucceededMessage, path ),
            saveAsync: exportPath => translationDictionaryService.SavePoAsync(
                exportPath,
                snapshot.Items,
                $"DCS Translation Japanese {applicationInfoService.GetVersion()}",
                currentTimestamp,
                currentTimestamp,
                $"{Strings_Shared.AppDisplayName} {applicationInfoService.GetVersion()}",
                cancellationToken ) );
    }

    /// <inheritdoc />
    public async Task<TranslationCreationCommandResult> ExportCsvAsync( string archiveFullPath, TranslationCreationDocumentSnapshot snapshot, CancellationToken cancellationToken = default ) {
        return await ExportFileAsync(
            archiveFullPath,
            failedMessage: Strings_Translation.CreateTranslationCsvExportFailedMessage,
            exportPathFactory: () => pathService.GetCsvExportPath( appSettingsService.Settings, archiveFullPath ),
            saveFileFilter: CsvFileFilter,
            logTargetName: "CSV",
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationCsvExportSucceededMessage, path ),
            saveAsync: exportPath => translationDictionaryService.SaveCsvAsync(
                exportPath,
                snapshot.Items,
                cancellationToken ) );
    }

    /// <inheritdoc />
    public Task<TranslationCreationCommandResult> ImportPoAsync( string archiveFullPath, TranslationCreationImportContext importContext, CancellationToken cancellationToken = default ) =>
        ImportFileAsync(
            archiveFullPath,
            importContext,
            initialPath: pathService.GetPoImportInitialPath( appSettingsService.Settings, archiveFullPath ),
            openFileFilter: PoSaveFileFilter,
            failedMessage: Strings_Translation.CreateTranslationPoImportFailedMessage,
            logTargetName: "PO",
            confirmOverwriteAsync: dialogService.ConfirmPoOverwriteAsync,
            loadEntries: translationDictionaryService.LoadPo,
            analyzeImport: entries => AnalyzePoImport( importContext.Rows, entries ),
            confirmPartialImportAsync: dialogService.ConfirmPoPartialImportAsync,
            applyMatch: static match => {
                match.Row.Translated = match.Translated;
                match.Row.IsEnabled = match.IsEnabled;
            },
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationPoImportSucceededMessage, path ),
            partialSuccessMessageFactory: ( matchedCount, path ) => string.Format( Strings_Translation.CreateTranslationPoImportPartialSucceededMessage, matchedCount, path ) );

    /// <inheritdoc />
    public Task<TranslationCreationCommandResult> ImportDictionaryAsync( string archiveFullPath, TranslationCreationImportContext importContext, CancellationToken cancellationToken = default ) =>
        ImportFileAsync(
            archiveFullPath,
            importContext,
            initialPath: pathService.GetDictionaryImportInitialPath( appSettingsService.Settings, archiveFullPath ),
            openFileFilter: DictionaryOpenFileFilter,
            failedMessage: Strings_Translation.CreateTranslationDictionaryImportFailedMessage,
            logTargetName: "dictionary",
            confirmOverwriteAsync: dialogService.ConfirmDictionaryOverwriteAsync,
            loadEntries: translationDictionaryService.LoadDictionaryFile,
            analyzeImport: entries => AnalyzeDictionaryImport( importContext.Rows, entries ),
            confirmPartialImportAsync: dialogService.ConfirmDictionaryPartialImportAsync,
            applyMatch: static match => match.Row.Translated = match.Translated,
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationDictionaryImportSucceededMessage, path ),
            partialSuccessMessageFactory: ( matchedCount, path ) => string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialSucceededMessage, matchedCount, path ) );

    /// <inheritdoc />
    public Task<TranslationCreationCommandResult> ImportCsvAsync( string archiveFullPath, TranslationCreationImportContext importContext, CancellationToken cancellationToken = default ) =>
        ImportFileAsync(
            archiveFullPath,
            importContext,
            initialPath: pathService.GetCsvImportInitialPath( appSettingsService.Settings, archiveFullPath ),
            openFileFilter: CsvFileFilter,
            failedMessage: Strings_Translation.CreateTranslationCsvImportFailedMessage,
            logTargetName: "CSV",
            confirmOverwriteAsync: dialogService.ConfirmCsvOverwriteAsync,
            loadEntries: translationDictionaryService.LoadCsv,
            analyzeImport: entries => AnalyzeCsvImport( importContext.Rows, entries ),
            confirmPartialImportAsync: dialogService.ConfirmCsvPartialImportAsync,
            applyMatch: static match => {
                match.Row.Translated = match.Translated;
                match.Row.IsEnabled = match.IsEnabled;
            },
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationCsvImportSucceededMessage, path ),
            partialSuccessMessageFactory: ( matchedCount, path ) => string.Format( Strings_Translation.CreateTranslationCsvImportPartialSucceededMessage, matchedCount, path ) );

    /// <inheritdoc />
    public async Task<TranslationCreationCommandResult> ImportJapaneseDictionaryAsync(
        string archiveFullPath,
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TranslationDictionaryItem> japaneseDictionaryItems,
        CancellationToken cancellationToken = default ) {
        cancellationToken.ThrowIfCancellationRequested();

        var japaneseSourceItems = TranslationCreationDictionaryLoader.CreateJapaneseImportSourceItems( japaneseDictionaryItems );
        var importAnalysis = AnalyzeDictionaryImport( rows, japaneseSourceItems );
        if(!importAnalysis.IsFullMatch && !await dialogService.ConfirmDictionaryPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"JP dictionary 読み込みの部分取り込み確認がキャンセルされた。Archive={archiveFullPath}, MatchCount={importAnalysis.Matches.Count}" );
            return new TranslationCreationCommandResult( false, true, false, 0, null );
        }

        foreach(var match in importAnalysis.Matches) {
            match.Row.Translated = match.Translated;
        }

        logger.Info( $"JP dictionary の初期取り込みが完了した。Archive={archiveFullPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
        return new TranslationCreationCommandResult( true, false, !importAnalysis.IsFullMatch, importAnalysis.Matches.Count, null );
    }
    #endregion

    #region PrivateHelpers

    /// <summary>
    /// 指定形式の書き出し処理共通フローを実行する。
    /// </summary>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="failedMessage">失敗時メッセージ。</param>
    /// <param name="exportPathFactory">既定出力先生成処理。</param>
    /// <param name="saveFileFilter">保存ダイアログフィルタ。</param>
    /// <param name="logTargetName">ログ出力用対象名。</param>
    /// <param name="successMessageFactory">成功時メッセージ生成処理。</param>
    /// <param name="saveAsync">保存処理。</param>
    /// <returns>操作結果を返す。</returns>
    private async Task<TranslationCreationCommandResult> ExportFileAsync(
        string archiveFullPath,
        string failedMessage,
        Func<string> exportPathFactory,
        string saveFileFilter,
        string logTargetName,
        Func<string, string> successMessageFactory,
        Func<string, Task> saveAsync ) {
        string exportPath;
        try {
            exportPath = exportPathFactory();
        }
        catch(Exception ex) {
            logger.Error( $"{logTargetName} 書き出し先の解決に失敗した。Archive={archiveFullPath}", ex );
            return new TranslationCreationCommandResult( false, false, false, 0, failedMessage );
        }

        try {
            var selectedExportPath = await dialogService.ConfirmExportPathAsync( exportPath, saveFileFilter, logTargetName, archiveFullPath );
            if(string.IsNullOrWhiteSpace( selectedExportPath )) {
                return new TranslationCreationCommandResult( false, true, false, 0, null );
            }

            await saveAsync( selectedExportPath );
            logger.Info( $"{logTargetName} の書き出しが完了した。Archive={archiveFullPath}, Path={selectedExportPath}" );
            return new TranslationCreationCommandResult(
                true,
                false,
                false,
                0,
                successMessageFactory( selectedExportPath ),
                TranslationCreationNotificationKind.ExportSucceeded,
                selectedExportPath );
        }
        catch(Exception ex) {
            logger.Error( $"{logTargetName} の書き出しに失敗した。Archive={archiveFullPath}", ex );
            return new TranslationCreationCommandResult( false, false, false, 0, failedMessage );
        }
    }

    /// <summary>
    /// 指定形式の取り込み処理共通フローを実行する。
    /// </summary>
    /// <typeparam name="TEntry">取り込み元項目型。</typeparam>
    /// <typeparam name="TMatch">一致結果型。</typeparam>
    /// <param name="archiveFullPath">対象アーカイブの絶対パス。</param>
    /// <param name="importContext">現在の取り込み対象状態。</param>
    /// <param name="initialPath">ファイル選択初期パス。</param>
    /// <param name="openFileFilter">ファイル選択フィルタ。</param>
    /// <param name="failedMessage">失敗時メッセージ。</param>
    /// <param name="logTargetName">ログ出力用対象名。</param>
    /// <param name="confirmOverwriteAsync">上書き確認処理。</param>
    /// <param name="loadEntries">項目一覧読込処理。</param>
    /// <param name="analyzeImport">一致解析処理。</param>
    /// <param name="confirmPartialImportAsync">部分一致確認処理。</param>
    /// <param name="applyMatch">一致結果適用処理。</param>
    /// <param name="successMessageFactory">全件一致時メッセージ生成処理。</param>
    /// <param name="partialSuccessMessageFactory">部分一致時メッセージ生成処理。</param>
    /// <returns>操作結果を返す。</returns>
    private async Task<TranslationCreationCommandResult> ImportFileAsync<TEntry, TMatch>(
        string archiveFullPath,
        TranslationCreationImportContext importContext,
        string initialPath,
        string openFileFilter,
        string failedMessage,
        string logTargetName,
        Func<Task<bool>> confirmOverwriteAsync,
        Func<string, Result<IReadOnlyList<TEntry>>> loadEntries,
        Func<IReadOnlyList<TEntry>, TranslationCreationImportAnalysis<TMatch>> analyzeImport,
        Func<int, Task<bool>> confirmPartialImportAsync,
        Action<TMatch> applyMatch,
        Func<string, string> successMessageFactory,
        Func<int, string, string> partialSuccessMessageFactory ) {
        if(!dialogService.TrySelectImportFile( initialPath, openFileFilter, out var selectedPath )) {
            logger.Info( $"{logTargetName} 読み込みファイル選択がキャンセルされた。Archive={archiveFullPath}, InitialPath={initialPath}" );
            return new TranslationCreationCommandResult( false, true, false, 0, null );
        }

        if(importContext.HasAnyTranslatedText && !await confirmOverwriteAsync()) {
            logger.Info( $"{logTargetName} 読み込みの上書き確認がキャンセルされた。Archive={archiveFullPath}, Path={selectedPath}" );
            return new TranslationCreationCommandResult( false, true, false, 0, null );
        }

        Result<IReadOnlyList<TEntry>> loadResult;
        try {
            loadResult = loadEntries( selectedPath );
        }
        catch(Exception ex) {
            logger.Error( $"{logTargetName} 読み込み中に例外が発生した。Archive={archiveFullPath}, Path={selectedPath}", ex );
            return new TranslationCreationCommandResult( false, false, false, 0, failedMessage );
        }

        if(loadResult.IsFailed) {
            return new TranslationCreationCommandResult( false, false, false, 0, failedMessage );
        }

        var importAnalysis = analyzeImport( loadResult.Value );
        if(!importAnalysis.IsFullMatch && !await confirmPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"{logTargetName} 読み込みの部分取り込み確認がキャンセルされた。Archive={archiveFullPath}, Path={selectedPath}, MatchCount={importAnalysis.Matches.Count}" );
            return new TranslationCreationCommandResult( false, true, false, 0, null );
        }

        foreach(var match in importAnalysis.Matches) {
            applyMatch( match );
        }

        logger.Info( $"{logTargetName} 読み込みが完了した。Archive={archiveFullPath}, Path={selectedPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
        return new TranslationCreationCommandResult(
            true,
            false,
            !importAnalysis.IsFullMatch,
            importAnalysis.Matches.Count,
            importAnalysis.IsFullMatch
                ? successMessageFactory( selectedPath )
                : partialSuccessMessageFactory( importAnalysis.Matches.Count, selectedPath ),
            TranslationCreationNotificationKind.Completed );
    }

    /// <summary>
    /// PO 項目一覧と画面上の dictionary 行との一致結果を解析する。
    /// </summary>
    /// <param name="rows">画面上の dictionary 行一覧。</param>
    /// <param name="entries">解析対象の PO 項目一覧。</param>
    /// <returns>一致解析結果を返す。</returns>
    private static TranslationCreationImportAnalysis<PoImportMatch> AnalyzePoImport(
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TranslationPoEntry> entries ) =>
        TranslationCreationImportMatcher.MatchByTranslationPair<TranslationPoEntry, PoImportMatch>(
            rows,
            entries,
            static row => (row.Key, row.Original),
            static entry => (entry.Context, entry.Original),
            ( row, entry ) => new PoImportMatch( row, entry.Translated, entry.IsEnabled ) );

    /// <summary>
    /// CSV 項目一覧と画面上の dictionary 行との一致結果を解析する。
    /// </summary>
    /// <param name="rows">画面上の dictionary 行一覧。</param>
    /// <param name="entries">解析対象の CSV 項目一覧。</param>
    /// <returns>一致解析結果を返す。</returns>
    private static TranslationCreationImportAnalysis<CsvImportMatch> AnalyzeCsvImport(
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TranslationCsvEntry> entries ) =>
        TranslationCreationImportMatcher.MatchByTranslationPair<TranslationCsvEntry, CsvImportMatch>(
            rows,
            entries,
            static row => (row.Key, row.Original),
            static entry => (entry.Key, entry.Original),
            ( row, entry ) => new CsvImportMatch( row, entry.Translated, entry.IsEnabled ) );

    /// <summary>
    /// dictionary 項目一覧と画面上の dictionary 行との一致結果を解析する。
    /// </summary>
    /// <param name="rows">画面上の dictionary 行一覧。</param>
    /// <param name="items">解析対象の dictionary 項目一覧。</param>
    /// <returns>一致解析結果を返す。</returns>
    private static TranslationCreationImportAnalysis<DictionaryImportMatch> AnalyzeDictionaryImport(
        IReadOnlyList<TranslationDictionaryItemRowViewModel> rows,
        IReadOnlyList<TranslationDictionaryItem> items ) =>
        TranslationCreationImportMatcher.MatchByNormalizedKey<TranslationDictionaryItem, DictionaryImportMatch>(
            rows,
            items,
            static row => row.Key,
            item => item.Key,
            ( row, item ) => new DictionaryImportMatch( row, item.Translated ) );

    /// <summary>
    /// PO ヘッダー用タイムスタンプ文字列へ変換する。
    /// </summary>
    /// <param name="value">変換対象の日時。</param>
    /// <returns>変換後文字列を返す。</returns>
    private static string FormatPoTimestamp( DateTimeOffset value ) =>
        value.ToString( "yyyy-MM-dd HH:mmzzz", CultureInfo.InvariantCulture );

    #endregion

    #region NestedTypes

    /// <summary>
    /// PO 取り込み時の一致結果を表す。
    /// </summary>
    /// <param name="Row">適用先行。</param>
    /// <param name="Translated">適用する翻訳文。</param>
    /// <param name="IsEnabled">適用する有効状態。</param>
    private sealed record PoImportMatch( TranslationDictionaryItemRowViewModel Row, string Translated, bool IsEnabled );

    /// <summary>
    /// dictionary 取り込み時の一致結果を表す。
    /// </summary>
    /// <param name="Row">適用先行。</param>
    /// <param name="Translated">適用する翻訳文。</param>
    private sealed record DictionaryImportMatch( TranslationDictionaryItemRowViewModel Row, string Translated );

    /// <summary>
    /// CSV 取り込み時の一致結果を表す。
    /// </summary>
    /// <param name="Row">適用先行。</param>
    /// <param name="Translated">適用する翻訳文。</param>
    /// <param name="IsEnabled">適用する有効状態。</param>
    private sealed record CsvImportMatch( TranslationDictionaryItemRowViewModel Row, string Translated, bool IsEnabled );
    #endregion
}