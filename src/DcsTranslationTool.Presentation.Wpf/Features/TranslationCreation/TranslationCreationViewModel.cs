using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// 翻訳作成ウィンドウの状態を管理する ViewModel である。
/// </summary>
/// <param name="archiveFullPath">翻訳対象のアーカイブ絶対パス。</param>
/// <param name="appSettingsService">アプリケーション設定サービス。</param>
/// <param name="applicationInfoService">アプリケーション情報サービス。</param>
/// <param name="dialogService">ダイアログ表示サービス。</param>
/// <param name="systemService">システム連携サービス。</param>
/// <param name="logger">ロギングサービス。</param>
/// <param name="translationDictionaryService">dictionary 読込サービス。</param>
public sealed class TranslationCreationViewModel(
    string archiveFullPath,
    IAppSettingsService appSettingsService,
    IApplicationInfoService applicationInfoService,
    IDialogService dialogService,
    IDialogProvider dialogProvider,
    ISystemService systemService,
    ILoggingService logger,
    ITranslationDictionaryService translationDictionaryService
) : Screen {
    private static readonly TimeSpan SelectedTranslatedCommitDelay = TimeSpan.FromMilliseconds( 250 );
    private const string JapaneseDictionaryEntryPath = "l10n/JP/dictionary";
    private const string DictionaryOpenFileFilter = "dictionary|dictionary|すべてのファイル|*.*";
    private const string DictionarySaveFileFilter = "dictionary|dictionary|すべてのファイル|*.*";
    private const string CsvFileFilter = "CSV files|*.csv|すべてのファイル|*.*";
    private const string PoSaveFileFilter = "PO files|*.po|すべてのファイル|*.*";
    private TranslationImportFormat _selectedImportFormat = TranslationImportFormat.Dictionary;
    private TranslationExportFormat _selectedExportFormat = TranslationExportFormat.Dictionary;
    private ObservableCollection<TranslationDictionaryItemRowViewModel> _dictionaryItems = [];
    private TranslationDictionaryItemRowViewModel? _selectedDictionaryItem;
    private string _selectedTranslated = string.Empty;
    private SnackbarMessageQueue? _messageQueue;
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _showEnabledItems = true;
    private bool _showDisabledItems = true;
    private bool _showOnlyUntranslated;
    private bool _hidePossibleNonTranslationTargets = true;
    private bool _hideEmptyOriginal = true;
    private bool _hasProcessedJapaneseDictionaryPrompt;
    private bool _hasPendingSelectedTranslatedEdit;
    private IReadOnlyList<TranslationDictionaryItem> _loadedDictionaryItems = [];
    private DispatcherTimer? _selectedTranslatedCommitTimer;

    /// <summary>
    /// ウィンドウの表示名を取得する。
    /// </summary>
    public string WindowTitle { get; } = Strings_Translation.CreateTranslationWindowTitle;

    /// <summary>
    /// TranslationCreation Window 専用の Snackbar メッセージキューを取得する。
    /// </summary>
    public SnackbarMessageQueue MessageQueue => _messageQueue ??= new();

    /// <summary>
    /// TranslationCreation Window に関連するアプリケーション設定を取得する。
    /// </summary>
    internal AppSettings AppSettings => appSettingsService.Settings;

    /// <summary>
    /// 選択中アーカイブの絶対パスを取得する。
    /// </summary>
    public string ArchiveFullPath { get; } = string.IsNullOrWhiteSpace( archiveFullPath )
        ? throw new ArgumentException( "アーカイブ絶対パスは必須です。", nameof( archiveFullPath ) )
        : archiveFullPath;

    /// <summary>
    /// dictionary 項目一覧を取得または設定する。
    /// </summary>
    public ObservableCollection<TranslationDictionaryItemRowViewModel> DictionaryItems {
        get => _dictionaryItems;
        private set {
            if(!Set( ref _dictionaryItems, value )) {
                return;
            }

            SubscribeDictionaryItems( value );
            FilteredDictionaryItemsView = CollectionViewSource.GetDefaultView( value );
            FilteredDictionaryItemsView.Filter = FilterDictionaryItem;
            FilteredDictionaryItemsView.Refresh();
            NotifyOfPropertyChange( nameof( HasDictionaryItems ) );
        }
    }

    /// <summary>
    /// フィルター済み dictionary 項目一覧を取得または設定する。
    /// </summary>
    public ICollectionView FilteredDictionaryItemsView { get; private set; } =
        CollectionViewSource.GetDefaultView( Array.Empty<object>() );

    /// <summary>
    /// 選択中の dictionary 項目を取得または設定する。
    /// </summary>
    public TranslationDictionaryItemRowViewModel? SelectedDictionaryItem {
        get => _selectedDictionaryItem;
        set {
            FlushPendingSelectedTranslatedEdit();

            if(!Set( ref _selectedDictionaryItem, value )) {
                return;
            }

            SyncSelectedTranslatedFromSelection();
            NotifyOfPropertyChange( nameof( SelectedOriginal ) );
            NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
        }
    }

    /// <summary>
    /// 選択中項目の Original を取得する。
    /// </summary>
    public string SelectedOriginal => SelectedDictionaryItem?.Original ?? string.Empty;

    /// <summary>
    /// 選択中項目の Translated を取得または設定する。
    /// </summary>
    public string SelectedTranslated {
        get => _selectedTranslated;
        set {
            if(SelectedDictionaryItem?.IsEnabled != true) {
                return;
            }

            if(string.Equals( _selectedTranslated, value, StringComparison.Ordinal )) {
                return;
            }

            _selectedTranslated = value;
            NotifyOfPropertyChange();
            ScheduleSelectedTranslatedCommit();
        }
    }

    /// <summary>
    /// 選択中項目の翻訳文を編集可能かどうかを取得する。
    /// </summary>
    public bool CanEditSelectedTranslated => SelectedDictionaryItem?.IsEnabled == true;

    /// <summary>
    /// 読み込み中かどうかを取得または設定する。
    /// </summary>
    public bool IsLoading {
        get => _isLoading;
        private set {
            if(!Set( ref _isLoading, value )) {
                return;
            }

            NotifyOfPropertyChange( nameof( CanExport ) );
            NotifyOfPropertyChange( nameof( CanImport ) );
            NotifyOfPropertyChange( nameof( CanImportDictionary ) );
            NotifyOfPropertyChange( nameof( CanImportCsv ) );
            NotifyOfPropertyChange( nameof( CanImportPo ) );
        }
    }

    /// <summary>
    /// 状態メッセージを取得または設定する。
    /// </summary>
    public string StatusMessage {
        get => _statusMessage;
        private set {
            if(!Set( ref _statusMessage, value )) {
                return;
            }

            NotifyOfPropertyChange( nameof( HasStatusMessage ) );
            NotifyOfPropertyChange( nameof( HasDictionaryItems ) );
        }
    }

    /// <summary>
    /// 状態メッセージが存在するかどうかを取得する。
    /// </summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace( StatusMessage );

    /// <summary>
    /// dictionary 項目が存在するかどうかを取得する。
    /// </summary>
    public bool HasDictionaryItems => DictionaryItems.Count > 0;

    /// <summary>
    /// dictionary を書き出し可能かどうかを取得する。
    /// </summary>
    public bool CanExport =>
        !IsLoading
        && _loadedDictionaryItems.Count > 0
        && !string.IsNullOrWhiteSpace( appSettingsService.Settings.TranslateFileDir );

    /// <summary>
    /// dictionary ファイルを読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanImportDictionary =>
        !IsLoading
        && _loadedDictionaryItems.Count > 0;

    /// <summary>
    /// PO ファイルを読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanImportPo => CanImportDictionary;

    /// <summary>
    /// CSV ファイルを読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanImportCsv => CanImportDictionary;

    /// <summary>
    /// いずれかの形式で読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanImport => CanImportDictionary;

    /// <summary>
    /// 現在の読み込み主動作用表示文言を取得する。
    /// </summary>
    public string ImportSplitButtonContent => _selectedImportFormat switch
    {
        TranslationImportFormat.Dictionary => Strings_Translation.CreateTranslationImportDictionaryButtonContent,
        TranslationImportFormat.Po => Strings_Translation.CreateTranslationImportPoSplitButtonContent,
        TranslationImportFormat.Csv => Strings_Translation.CreateTranslationImportCsvButtonContent,
        _ => Strings_Translation.CreateTranslationImportPoSplitButtonContent
    };

    /// <summary>
    /// 現在の書き出し主動作用表示文言を取得する。
    /// </summary>
    public string ExportSplitButtonContent => _selectedExportFormat switch
    {
        TranslationExportFormat.Dictionary => Strings_Translation.CreateTranslationExportButtonContent,
        TranslationExportFormat.Po => Strings_Translation.CreateTranslationExportPoSplitButtonContent,
        TranslationExportFormat.Csv => Strings_Translation.CreateTranslationExportCsvButtonContent,
        _ => Strings_Translation.CreateTranslationExportButtonContent
    };

    /// <summary>
    /// 有効状態の項目を表示するかどうかを取得または設定する。
    /// </summary>
    public bool ShowEnabledItems {
        get => _showEnabledItems;
        set {
            if(!Set( ref _showEnabledItems, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// 無効状態の項目を表示するかどうかを取得または設定する。
    /// </summary>
    public bool ShowDisabledItems {
        get => _showDisabledItems;
        set {
            if(!Set( ref _showDisabledItems, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// 未翻訳のみ表示するかどうかを取得または設定する。
    /// </summary>
    public bool ShowOnlyUntranslated {
        get => _showOnlyUntranslated;
        set {
            if(!Set( ref _showOnlyUntranslated, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// 翻訳対象ではない可能性がある行を非表示にするかどうかを取得または設定する。
    /// </summary>
    public bool HidePossibleNonTranslationTargets {
        get => _hidePossibleNonTranslationTargets;
        set {
            if(!Set( ref _hidePossibleNonTranslationTargets, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// Original が空欄の行を非表示にするかどうかを取得または設定する。
    /// </summary>
    public bool HideEmptyOriginal {
        get => _hideEmptyOriginal;
        set {
            if(!Set( ref _hideEmptyOriginal, value )) {
                return;
            }

            RefreshFilter();
        }
    }

    /// <summary>
    /// 表示中の dictionary 項目選択を 1 件上へ移動する。
    /// </summary>
    /// <returns>選択項目が変化したかどうか。</returns>
    public bool MoveSelectionUp() => MoveSelection( -1 );

    /// <summary>
    /// 表示中の dictionary 項目選択を 1 件下へ移動する。
    /// </summary>
    /// <returns>選択項目が変化したかどうか。</returns>
    public bool MoveSelectionDown() => MoveSelection( 1 );

    /// <summary>
    /// 現在選択中の読み込み形式を実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public Task ExecuteImportAsync() => _selectedImportFormat switch
    {
        TranslationImportFormat.Dictionary => ImportDictionaryAsync(),
        TranslationImportFormat.Csv => ImportCsvAsync(),
        _ => ImportPoAsync()
    };

    /// <summary>
    /// 現在選択中の書き出し形式を実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public Task ExecuteExportAsync() => _selectedExportFormat switch
    {
        TranslationExportFormat.Po => ExportPoAsync(),
        TranslationExportFormat.Csv => ExportCsvAsync(),
        _ => ExportAsync()
    };

    /// <summary>
    /// 読み込み形式を PO に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task SelectImportPoAsync() {
        SetSelectedImportFormat( TranslationImportFormat.Po );
        await ImportPoAsync();
    }

    /// <summary>
    /// 読み込み形式を dictionary に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task SelectImportDictionaryAsync() {
        SetSelectedImportFormat( TranslationImportFormat.Dictionary );
        await ImportDictionaryAsync();
    }

    /// <summary>
    /// 読み込み形式を CSV に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task SelectImportCsvAsync() {
        SetSelectedImportFormat( TranslationImportFormat.Csv );
        await ImportCsvAsync();
    }

    /// <summary>
    /// 書き出し形式を dictionary に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task SelectExportDictionaryAsync() {
        SetSelectedExportFormat( TranslationExportFormat.Dictionary );
        await ExportAsync();
    }

    /// <summary>
    /// 書き出し形式を PO に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task SelectExportPoAsync() {
        SetSelectedExportFormat( TranslationExportFormat.Po );
        await ExportPoAsync();
    }

    /// <summary>
    /// 書き出し形式を CSV に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task SelectExportCsvAsync() {
        SetSelectedExportFormat( TranslationExportFormat.Csv );
        await ExportCsvAsync();
    }

    /// <summary>
    /// 指定行の Original をクリップボードへコピーする。
    /// </summary>
    /// <param name="row">コピー対象行。</param>
    public void CopyOriginalToClipboard( TranslationDictionaryItemRowViewModel? row ) {
        if(row is null || string.IsNullOrWhiteSpace( row.Original )) {
            return;
        }

        systemService.SetClipboardText( row.Original );
        ShowCompletedSnackbar( Strings_Translation.CreateTranslationOriginalCopiedMessage );
    }

    /// <summary>
    /// アクティブ化完了時に dictionary を読み込む。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    protected override async Task OnActivatedAsync( CancellationToken cancellationToken ) {
        await base.OnActivatedAsync( cancellationToken );
        await LoadDictionaryAsync( cancellationToken );
    }

    /// <summary>
    /// ウィンドウ表示後に埋め込みJP dictionary の確認と取り込みを行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    internal async Task HandleWindowLoadedAsync( CancellationToken cancellationToken = default ) {
        if(_hasProcessedJapaneseDictionaryPrompt) {
            return;
        }

        _hasProcessedJapaneseDictionaryPrompt = true;
        cancellationToken.ThrowIfCancellationRequested();

        var hasJapaneseDictionaryResult = translationDictionaryService.HasArchiveEntry( ArchiveFullPath, JapaneseDictionaryEntryPath );
        if(hasJapaneseDictionaryResult is null) {
            logger.Warn( $"JP dictionary 存在確認結果が null のため警告処理をスキップする。Archive={ArchiveFullPath}" );
            return;
        }

        if(hasJapaneseDictionaryResult.IsFailed) {
            logger.Warn( $"JP dictionary 存在確認に失敗したため警告処理をスキップする。Archive={ArchiveFullPath}" );
            return;
        }

        if(!hasJapaneseDictionaryResult.Value || _loadedDictionaryItems.Count == 0) {
            return;
        }

        if(!await ConfirmArchiveContainsJapaneseDictionaryAsync()) {
            logger.Info( $"既存JP dictionary 警告でキャンセルされたためウィンドウを閉じる。Archive={ArchiveFullPath}" );
            await TryCloseAsync( false );
            return;
        }

        if(!await ConfirmJapaneseDictionaryImportAsync()) {
            return;
        }

        var japaneseSourceResult = translationDictionaryService.LoadDictionary( ArchiveFullPath, JapaneseDictionaryEntryPath );
        if(japaneseSourceResult.IsFailed) {
            logger.Warn( $"JP dictionary の読込に失敗したため DEFAULT dictionary のみで継続する。Archive={ArchiveFullPath}" );
            return;
        }

        var japaneseSourceItems = japaneseSourceResult.Value
            .Select( item => new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Original
            } )
            .ToArray();
        var importAnalysis = AnalyzeDictionaryImport( japaneseSourceItems );
        if(!importAnalysis.IsFullMatch && !await ConfirmDictionaryPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"JP dictionary 読み込みの部分取り込み確認がキャンセルされた。Archive={ArchiveFullPath}, MatchCount={importAnalysis.Matches.Count}" );
            return;
        }

        foreach(var match in importAnalysis.Matches) {
            match.Row.Translated = match.Translated;
        }

        logger.Info( $"JP dictionary の初期取り込みが完了した。Archive={ArchiveFullPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
    }

    private Task LoadDictionaryAsync( CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        logger.Info( $"TranslationCreationViewModel の dictionary 読込を開始する。Archive={ArchiveFullPath}" );
        IsLoading = true;
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadingMessage;

        try {
            var result = translationDictionaryService.LoadDictionary( ArchiveFullPath );
            if(result.IsFailed) {
                SetDictionaryLoadFailedState();
                return Task.CompletedTask;
            }

            var loadState = BuildDictionaryLoadState( result.Value );

            ApplyDictionaryLoadState( loadState );
            SelectedDictionaryItem = null;
            StatusMessage = DictionaryItems.Count == 0
                ? Strings_Translation.CreateTranslationDictionaryEmptyMessage
                : string.Empty;
            NotifyDictionaryAvailabilityChanged();
            logger.Info( $"TranslationCreationViewModel の dictionary 読込詳細。Archive={ArchiveFullPath}" );
            return Task.CompletedTask;
        }
        catch(Exception ex) {
            logger.Error( $"TranslationCreationViewModel の dictionary 読込中に例外が発生した。Archive={ArchiveFullPath}", ex );
            SetDictionaryLoadFailedState();
            return Task.CompletedTask;
        }
        finally {
            IsLoading = false;
            logger.Info( $"TranslationCreationViewModel の dictionary 読込を終了する。Archive={ArchiveFullPath}, Count={DictionaryItems.Count}" );
        }
    }

    /// <summary>
    /// 編集結果を翻訳ファイルとして書き出す。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task ExportAsync() {
        FlushPendingSelectedTranslatedEdit();

        if(!CanExport) {
            logger.Warn( "dictionary を書き出せない状態のため処理を中断する。" );
            return;
        }

        await ExportFileAsync(
            exportPathFactory: GetDictionaryExportPath,
            exportingMessage: Strings_Translation.CreateTranslationDictionaryExportingMessage,
            failedMessage: Strings_Translation.CreateTranslationDictionaryExportFailedMessage,
            succeededMessageFactory: path => string.Format( Strings_Translation.CreateTranslationDictionaryExportSucceededMessage, path ),
            saveFileFilter: DictionarySaveFileFilter,
            saveAsync: async exportPath => {
                var editableDictionaryResult = translationDictionaryService.LoadEditableDictionary( ArchiveFullPath );
                if(editableDictionaryResult.IsFailed) {
                    logger.Warn( $"dictionary 書き出し用の元データ取得に失敗した。Archive={ArchiveFullPath}" );
                    throw new InvalidOperationException( "dictionary 書き出し用の元データ取得に失敗した。" );
                }

                var currentTranslations = DictionaryItems
                    .Where( item => item.IsEnabled )
                    .ToDictionary(
                        item => item.Key,
                        item => item.Translated,
                        StringComparer.Ordinal );

                await translationDictionaryService.SaveDictionaryAsync(
                    exportPath,
                    editableDictionaryResult.Value,
                    currentTranslations );
            },
            logTargetName: "dictionary" );
    }

    /// <summary>
    /// 編集結果を PO ファイルとして書き出す。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task ExportPoAsync() {
        FlushPendingSelectedTranslatedEdit();

        if(!CanExport) {
            logger.Warn( "PO を書き出せない状態のため処理を中断する。" );
            return;
        }

        var currentTimestamp = FormatPoTimestamp( systemService.GetCurrentDateTimeOffset() );

        await ExportFileAsync(
            exportPathFactory: GetPoExportPath,
            exportingMessage: Strings_Translation.CreateTranslationPoExportingMessage,
            failedMessage: Strings_Translation.CreateTranslationPoExportFailedMessage,
            succeededMessageFactory: path => string.Format( Strings_Translation.CreateTranslationPoExportSucceededMessage, path ),
            saveFileFilter: PoSaveFileFilter,
            saveAsync: exportPath => translationDictionaryService.SavePoAsync(
                exportPath,
                CreateCurrentDictionaryItems(),
                GetProjectIdVersion(),
                currentTimestamp,
                currentTimestamp,
                GetXGenerator() ),
            logTargetName: "PO" );
    }

    /// <summary>
    /// 編集結果を CSV ファイルとして書き出す。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task ExportCsvAsync() {
        FlushPendingSelectedTranslatedEdit();

        if(!CanExport) {
            logger.Warn( "CSV を書き出せない状態のため処理を中断する。" );
            return;
        }

        await ExportFileAsync(
            exportPathFactory: GetCsvExportPath,
            exportingMessage: Strings_Translation.CreateTranslationCsvExportingMessage,
            failedMessage: Strings_Translation.CreateTranslationCsvExportFailedMessage,
            succeededMessageFactory: path => string.Format( Strings_Translation.CreateTranslationCsvExportSucceededMessage, path ),
            saveFileFilter: CsvFileFilter,
            saveAsync: exportPath => translationDictionaryService.SaveCsvAsync(
                exportPath,
                CreateCurrentDictionaryItems() ),
            logTargetName: "CSV" );
    }

    /// <summary>
    /// PO ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task ImportPoAsync() {
        FlushPendingSelectedTranslatedEdit();

        if(!CanImportPo) {
            logger.Warn( "PO を読み込めない状態のため処理を中断する。" );
            return;
        }

        var initialPath = GetPoImportInitialPath();
        if(!dialogProvider.ShowOpenFilePicker( initialPath, PoSaveFileFilter, out var selectedPath )) {
            logger.Info( $"PO 読み込みファイル選択がキャンセルされた。Archive={ArchiveFullPath}, InitialPath={initialPath}" );
            return;
        }

        if(HasTranslatedText() && !await ConfirmPoOverwriteAsync()) {
            logger.Info( $"PO 読み込みの上書き確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}" );
            return;
        }

        Result<IReadOnlyList<TranslationPoEntry>> loadResult;
        try {
            IsLoading = true;
            StatusMessage = Strings_Translation.CreateTranslationPoImportingMessage;
            loadResult = translationDictionaryService.LoadPo( selectedPath );
        }
        catch(Exception ex) {
            logger.Error( $"PO 読み込み中に例外が発生した。Archive={ArchiveFullPath}, Path={selectedPath}", ex );
            StatusMessage = Strings_Translation.CreateTranslationPoImportFailedMessage;
            return;
        }
        finally {
            IsLoading = false;
        }

        if(loadResult.IsFailed) {
            StatusMessage = Strings_Translation.CreateTranslationPoImportFailedMessage;
            return;
        }

        var importAnalysis = AnalyzePoImport( loadResult.Value );
        if(!importAnalysis.IsFullMatch && !await ConfirmPoPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"PO 読み込みの部分取り込み確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}, MatchCount={importAnalysis.Matches.Count}" );
            return;
        }

        foreach(var match in importAnalysis.Matches) {
            match.Row.Translated = match.Translated;
            match.Row.IsEnabled = match.IsEnabled;
        }

        StatusMessage = importAnalysis.IsFullMatch
            ? string.Format( Strings_Translation.CreateTranslationPoImportSucceededMessage, selectedPath )
            : string.Format( Strings_Translation.CreateTranslationPoImportPartialSucceededMessage, importAnalysis.Matches.Count, selectedPath );
        ShowCompletedSnackbar( StatusMessage );
        logger.Info( $"PO 読み込みが完了した。Archive={ArchiveFullPath}, Path={selectedPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
    }

    /// <summary>
    /// dictionary ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task ImportDictionaryAsync() {
        FlushPendingSelectedTranslatedEdit();

        if(!CanImportDictionary) {
            logger.Warn( "dictionary を読み込めない状態のため処理を中断する。" );
            return;
        }

        var initialPath = GetDictionaryImportInitialPath();
        if(!dialogProvider.ShowOpenFilePicker( initialPath, DictionaryOpenFileFilter, out var selectedPath )) {
            logger.Info( $"dictionary 読み込みファイル選択がキャンセルされた。Archive={ArchiveFullPath}, InitialPath={initialPath}" );
            return;
        }

        if(HasTranslatedText() && !await ConfirmDictionaryOverwriteAsync()) {
            logger.Info( $"dictionary 読み込みの上書き確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}" );
            return;
        }

        Result<IReadOnlyList<TranslationDictionaryItem>> loadResult;
        try {
            IsLoading = true;
            StatusMessage = Strings_Translation.CreateTranslationDictionaryImportingMessage;
            loadResult = translationDictionaryService.LoadDictionaryFile( selectedPath );
        }
        catch(Exception ex) {
            logger.Error( $"dictionary 読み込み中に例外が発生した。Archive={ArchiveFullPath}, Path={selectedPath}", ex );
            StatusMessage = Strings_Translation.CreateTranslationDictionaryImportFailedMessage;
            return;
        }
        finally {
            IsLoading = false;
        }

        if(loadResult.IsFailed) {
            StatusMessage = Strings_Translation.CreateTranslationDictionaryImportFailedMessage;
            return;
        }

        var importAnalysis = AnalyzeDictionaryImport( loadResult.Value );
        if(!importAnalysis.IsFullMatch && !await ConfirmDictionaryPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"dictionary 読み込みの部分取り込み確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}, MatchCount={importAnalysis.Matches.Count}" );
            return;
        }

        foreach(var match in importAnalysis.Matches) {
            match.Row.Translated = match.Translated;
        }

        StatusMessage = importAnalysis.IsFullMatch
            ? string.Format( Strings_Translation.CreateTranslationDictionaryImportSucceededMessage, selectedPath )
            : string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialSucceededMessage, importAnalysis.Matches.Count, selectedPath );
        ShowCompletedSnackbar( StatusMessage );
        logger.Info( $"dictionary 読み込みが完了した。Archive={ArchiveFullPath}, Path={selectedPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
    }

    /// <summary>
    /// CSV ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task ImportCsvAsync() {
        FlushPendingSelectedTranslatedEdit();

        if(!CanImportCsv) {
            logger.Warn( "CSV を読み込めない状態のため処理を中断する。" );
            return;
        }

        var initialPath = GetCsvImportInitialPath();
        if(!dialogProvider.ShowOpenFilePicker( initialPath, CsvFileFilter, out var selectedPath )) {
            logger.Info( $"CSV 読み込みファイル選択がキャンセルされた。Archive={ArchiveFullPath}, InitialPath={initialPath}" );
            return;
        }

        if(HasTranslatedText() && !await ConfirmCsvOverwriteAsync()) {
            logger.Info( $"CSV 読み込みの上書き確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}" );
            return;
        }

        Result<IReadOnlyList<TranslationCsvEntry>> loadResult;
        try {
            IsLoading = true;
            StatusMessage = Strings_Translation.CreateTranslationCsvImportingMessage;
            loadResult = translationDictionaryService.LoadCsv( selectedPath );
        }
        catch(Exception ex) {
            logger.Error( $"CSV 読み込み中に例外が発生した。Archive={ArchiveFullPath}, Path={selectedPath}", ex );
            StatusMessage = Strings_Translation.CreateTranslationCsvImportFailedMessage;
            return;
        }
        finally {
            IsLoading = false;
        }

        if(loadResult.IsFailed) {
            StatusMessage = Strings_Translation.CreateTranslationCsvImportFailedMessage;
            return;
        }

        var importAnalysis = AnalyzeCsvImport( loadResult.Value );
        if(!importAnalysis.IsFullMatch && !await ConfirmCsvPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"CSV 読み込みの部分取り込み確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}, MatchCount={importAnalysis.Matches.Count}" );
            return;
        }

        foreach(var match in importAnalysis.Matches) {
            match.Row.Translated = match.Translated;
            match.Row.IsEnabled = match.IsEnabled;
        }

        StatusMessage = importAnalysis.IsFullMatch
            ? string.Format( Strings_Translation.CreateTranslationCsvImportSucceededMessage, selectedPath )
            : string.Format( Strings_Translation.CreateTranslationCsvImportPartialSucceededMessage, importAnalysis.Matches.Count, selectedPath );
        ShowCompletedSnackbar( StatusMessage );
        logger.Info( $"CSV 読み込みが完了した。Archive={ArchiveFullPath}, Path={selectedPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
    }

    private bool FilterDictionaryItem( object item ) {
        if(item is not TranslationDictionaryItemRowViewModel row) {
            return false;
        }

        if(row.IsEnabled && !ShowEnabledItems) {
            return false;
        }

        if(!row.IsEnabled && !ShowDisabledItems) {
            return false;
        }

        if(HidePossibleNonTranslationTargets && IsPossibleNonTranslationTarget( row )) {
            return false;
        }

        if(HideEmptyOriginal && string.IsNullOrWhiteSpace( row.Original )) {
            return false;
        }

        if(ShowOnlyUntranslated && !string.IsNullOrWhiteSpace( row.Translated )) {
            return false;
        }

        return true;
    }

    private static bool IsPossibleNonTranslationTarget( TranslationDictionaryItemRowViewModel row ) {
        return IsPossibleNonTranslationTarget( row.Key, row.Original );
    }

    private static bool IsPossibleNonTranslationTarget( TranslationDictionaryItem item ) =>
        IsPossibleNonTranslationTarget( item.Key, item.Original );

    private static bool IsPossibleNonTranslationTarget( string key, string original ) {
        if(string.IsNullOrWhiteSpace( original )) {
            return true;
        }

        if(!key.StartsWith( "DictKey_", StringComparison.Ordinal )) {
            return true;
        }

        return key.StartsWith( "DictKey_WptName_", StringComparison.Ordinal )
            || key.StartsWith( "DictKey_ActionComment_", StringComparison.Ordinal )
            || key.StartsWith( "DictKey_GroupName_", StringComparison.Ordinal )
            || key.StartsWith( "DictKey_UnitName_", StringComparison.Ordinal )
            || LuaCodeStringDetector.IsLuaCodeString( original );
    }

    private static TranslationDictionaryItem InitializeDictionaryItem( TranslationDictionaryItem item ) =>
        new( item.Key, item.Original )
        {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled && !IsPossibleNonTranslationTarget( item )
        };

    private static DictionaryLoadState BuildDictionaryLoadState( IReadOnlyList<TranslationDictionaryItem> items ) {
        var initializedItems = items
            .Select( InitializeDictionaryItem )
            .OrderBy( GetDictionaryItemSortOrder )
            .ThenBy( item => item.Key, TranslationCreationNaturalKeyComparer.Instance )
            .ToArray();

        List<TranslationDictionaryItem> loadedItems = new( initializedItems.Length );
        List<TranslationDictionaryItemRowViewModel> rowItems = new( initializedItems.Length );
        foreach(var item in initializedItems) {
            loadedItems.Add( new TranslationDictionaryItem( item.Key, item.Original )
            {
                Translated = item.Translated,
                IsEnabled = item.IsEnabled
            } );
            rowItems.Add( new TranslationDictionaryItemRowViewModel( item ) );
        }

        return new DictionaryLoadState( loadedItems, rowItems );
    }

    private void ApplyDictionaryLoadState( DictionaryLoadState state ) {
        _loadedDictionaryItems = state.LoadedItems;
        DictionaryItems = [.. state.RowItems];
    }

    private void RefreshFilter() => FilteredDictionaryItemsView.Refresh();

    private void SubscribeDictionaryItems( ObservableCollection<TranslationDictionaryItemRowViewModel> dictionaryItems ) {
        dictionaryItems.CollectionChanged += OnDictionaryItemsCollectionChanged;
        foreach(var item in dictionaryItems) {
            item.PropertyChanged += OnDictionaryItemPropertyChanged;
        }
    }

    private void OnDictionaryItemsCollectionChanged( object? sender, NotifyCollectionChangedEventArgs e ) {
        if(e.OldItems is not null) {
            foreach(var item in e.OldItems.OfType<TranslationDictionaryItemRowViewModel>()) {
                item.PropertyChanged -= OnDictionaryItemPropertyChanged;
            }
        }

        if(e.NewItems is not null) {
            foreach(var item in e.NewItems.OfType<TranslationDictionaryItemRowViewModel>()) {
                item.PropertyChanged += OnDictionaryItemPropertyChanged;
            }
        }

        RefreshFilter();
    }

    private void OnDictionaryItemPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.Translated )) {
            if(ReferenceEquals( sender, SelectedDictionaryItem ) && !_hasPendingSelectedTranslatedEdit) {
                SyncSelectedTranslatedFromSelection();
            }

            RefreshFilter();
        }

        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.IsEnabled )) {
            if(ReferenceEquals( sender, SelectedDictionaryItem )) {
                if(SelectedDictionaryItem?.IsEnabled != true) {
                    CancelSelectedTranslatedCommit();
                    SyncSelectedTranslatedFromSelection();
                }

                NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
            }

            RefreshFilter();
        }
    }

    internal void FlushPendingSelectedTranslatedEdit() {
        if(!_hasPendingSelectedTranslatedEdit) {
            return;
        }

        CancelSelectedTranslatedCommit();
        if(SelectedDictionaryItem?.IsEnabled != true) {
            SyncSelectedTranslatedFromSelection();
            return;
        }

        if(string.Equals( SelectedDictionaryItem.Translated, _selectedTranslated, StringComparison.Ordinal )) {
            return;
        }

        SelectedDictionaryItem.Translated = _selectedTranslated;
    }

    private DispatcherTimer CreateSelectedTranslatedCommitTimer() {
        var timer = new DispatcherTimer( DispatcherPriority.Background )
        {
            Interval = SelectedTranslatedCommitDelay
        };
        timer.Tick += OnSelectedTranslatedCommitTimerTick;
        return timer;
    }

    private void OnSelectedTranslatedCommitTimerTick( object? sender, EventArgs e ) {
        FlushPendingSelectedTranslatedEdit();
    }

    private void ScheduleSelectedTranslatedCommit() {
        _hasPendingSelectedTranslatedEdit = true;
        SelectedTranslatedCommitTimer.Stop();
        SelectedTranslatedCommitTimer.Start();
    }

    private void CancelSelectedTranslatedCommit() {
        _hasPendingSelectedTranslatedEdit = false;
        _selectedTranslatedCommitTimer?.Stop();
    }

    private void SyncSelectedTranslatedFromSelection() {
        var nextValue = SelectedDictionaryItem?.Translated ?? string.Empty;
        if(string.Equals( _selectedTranslated, nextValue, StringComparison.Ordinal )) {
            return;
        }

        _selectedTranslated = nextValue;
        NotifyOfPropertyChange( nameof( SelectedTranslated ) );
    }

    private DispatcherTimer SelectedTranslatedCommitTimer => _selectedTranslatedCommitTimer ??= CreateSelectedTranslatedCommitTimer();

    private static int GetDictionaryItemSortOrder( TranslationDictionaryItem item ) {
        if(item.Key.StartsWith( "DictKey_sortie_", StringComparison.Ordinal )) {
            return 0;
        }

        if(item.Key.StartsWith( "DictKey_descriptionText_", StringComparison.Ordinal )) {
            return 1;
        }

        if(item.Key.StartsWith( "DictKey_descriptionBlueTask_", StringComparison.Ordinal )) {
            return 2;
        }

        if(item.Key.StartsWith( "DictKey_descriptionRedTask_", StringComparison.Ordinal )) {
            return 3;
        }

        if(item.Key.StartsWith( "DictKey_descriptionNeutralsTask_", StringComparison.Ordinal )) {
            return 4;
        }

        if(item.Key.StartsWith( "DictKey_description", StringComparison.Ordinal )) {
            return 5;
        }

        if(item.Key.StartsWith( "DictKey_", StringComparison.Ordinal )) {
            return 6;
        }

        return 7;
    }

    private PoImportAnalysis AnalyzePoImport( IReadOnlyList<TranslationPoEntry> entries ) {
        var rowGroups = DictionaryItems
            .GroupBy( row => CreateTranslationPair( row.Key, row.Original ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var entryGroups = entries
            .GroupBy( entry => CreateTranslationPair( entry.Context, entry.Original ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var matches = rowGroups.Keys
            .Intersect( entryGroups.Keys )
            .Where( key => rowGroups[key].Length == 1 && entryGroups[key].Length == 1 )
            .Select( key => new PoImportMatch( rowGroups[key][0], entryGroups[key][0].Translated, entryGroups[key][0].IsEnabled ) )
            .ToArray();
        var isFullMatch =
            DictionaryItems.Count == entries.Count
            && rowGroups.Count == DictionaryItems.Count
            && entryGroups.Count == entries.Count
            && matches.Length == DictionaryItems.Count;
        return new PoImportAnalysis( isFullMatch, matches );
    }

    private CsvImportAnalysis AnalyzeCsvImport( IReadOnlyList<TranslationCsvEntry> entries ) {
        var rowGroups = DictionaryItems
            .GroupBy( row => CreateTranslationPair( row.Key, row.Original ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var entryGroups = entries
            .GroupBy( entry => CreateTranslationPair( entry.Key, entry.Original ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var matches = rowGroups.Keys
            .Intersect( entryGroups.Keys )
            .Where( key => rowGroups[key].Length == 1 && entryGroups[key].Length == 1 )
            .Select( key => new CsvImportMatch( rowGroups[key][0], entryGroups[key][0].Translated, entryGroups[key][0].IsEnabled ) )
            .ToArray();
        var isFullMatch =
            DictionaryItems.Count == entries.Count
            && rowGroups.Count == DictionaryItems.Count
            && entryGroups.Count == entries.Count
            && matches.Length == DictionaryItems.Count;
        return new CsvImportAnalysis( isFullMatch, matches );
    }

    private DictionaryImportAnalysis AnalyzeDictionaryImport( IReadOnlyList<TranslationDictionaryItem> items ) {
        var rowGroups = DictionaryItems
            .GroupBy( row => NormalizeTranslationPairValue( row.Key ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var itemGroups = items
            .GroupBy( item => NormalizeTranslationPairValue( item.Key ) )
            .ToDictionary( group => group.Key, group => group.ToArray() );
        var matches = rowGroups.Keys
            .Intersect( itemGroups.Keys )
            .Where( key => rowGroups[key].Length == 1 && itemGroups[key].Length == 1 )
            .Select( key => new DictionaryImportMatch( rowGroups[key][0], itemGroups[key][0].Translated ) )
            .ToArray();
        var isFullMatch =
            DictionaryItems.Count == items.Count
            && rowGroups.Count == DictionaryItems.Count
            && itemGroups.Count == items.Count
            && matches.Length == DictionaryItems.Count;
        return new DictionaryImportAnalysis( isFullMatch, matches );
    }

    private bool MoveSelection( int offset ) {
        var visibleItems = FilteredDictionaryItemsView
            .Cast<TranslationDictionaryItemRowViewModel>()
            .ToArray();
        if(visibleItems.Length == 0) {
            return false;
        }

        if(SelectedDictionaryItem is null) {
            SelectedDictionaryItem = offset < 0
                ? visibleItems[^1]
                : visibleItems[0];
            return true;
        }

        var currentIndex = Array.IndexOf( visibleItems, SelectedDictionaryItem );
        if(currentIndex < 0) {
            SelectedDictionaryItem = offset < 0
                ? visibleItems[^1]
                : visibleItems[0];
            return true;
        }

        var nextIndex = currentIndex + offset;
        if(nextIndex < 0 || nextIndex >= visibleItems.Length) {
            return false;
        }

        SelectedDictionaryItem = visibleItems[nextIndex];
        return true;
    }

    private void SetSelectedImportFormat( TranslationImportFormat format ) {
        if(_selectedImportFormat == format) {
            return;
        }

        _selectedImportFormat = format;
        NotifyOfPropertyChange( nameof( ImportSplitButtonContent ) );
    }

    private void SetSelectedExportFormat( TranslationExportFormat format ) {
        if(_selectedExportFormat == format) {
            return;
        }

        _selectedExportFormat = format;
        NotifyOfPropertyChange( nameof( ExportSplitButtonContent ) );
    }

    private async Task ExportFileAsync(
        Func<string> exportPathFactory,
        string exportingMessage,
        string failedMessage,
        Func<string, string> succeededMessageFactory,
        string saveFileFilter,
        Func<string, Task> saveAsync,
        string logTargetName ) {
        string exportPath;
        try {
            exportPath = exportPathFactory();
        }
        catch(Exception ex) {
            logger.Error( $"{logTargetName} 書き出し先の解決に失敗した。Archive={ArchiveFullPath}", ex );
            StatusMessage = failedMessage;
            return;
        }

        try {
            var selectedExportPath = await ConfirmExportPathAsync( exportPath, saveFileFilter, logTargetName );
            if(string.IsNullOrWhiteSpace( selectedExportPath )) {
                return;
            }

            IsLoading = true;
            StatusMessage = exportingMessage;

            await saveAsync( selectedExportPath );
            StatusMessage = succeededMessageFactory( selectedExportPath );
            ShowExportSucceededSnackbar( selectedExportPath );
            logger.Info( $"{logTargetName} の書き出しが完了した。Archive={ArchiveFullPath}, Path={selectedExportPath}" );
        }
        catch(Exception ex) {
            logger.Error( $"{logTargetName} の書き出しに失敗した。Archive={ArchiveFullPath}", ex );
            StatusMessage = failedMessage;
        }
        finally {
            IsLoading = false;
        }
    }

    private async Task<string?> ConfirmExportPathAsync( string exportPath, string saveFileFilter, string logTargetName ) {
        if(!File.Exists( exportPath )) {
            return exportPath;
        }

        var overwriteResult = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = "上書き確認",
                Message = $"保存先にファイルが既に存在します。上書きしますか？{Environment.NewLine}{Environment.NewLine}{exportPath}",
                ConfirmButtonText = "上書き",
                CancelButtonText = "キャンセル",
                SecondaryButtonText = "別名保存",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );

        if(overwriteResult == ConfirmationDialogResult.Cancel) {
            logger.Info( $"{logTargetName} の上書き確認でキャンセルされた。Archive={ArchiveFullPath}, Path={exportPath}" );
            return null;
        }

        if(overwriteResult != ConfirmationDialogResult.Secondary) {
            return exportPath;
        }

        if(!dialogProvider.ShowSaveFilePicker( exportPath, saveFileFilter, out var selectedPath )) {
            logger.Info( $"{logTargetName} の別名保存でキャンセルされた。Archive={ArchiveFullPath}, Path={exportPath}" );
            return null;
        }

        return selectedPath;
    }

    private async Task<bool> ConfirmPoOverwriteAsync() {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationPoImportOverwriteConfirmationTitle,
                Message = Strings_Translation.CreateTranslationPoImportOverwriteConfirmationMessage,
                ConfirmButtonText = "上書き",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmDictionaryOverwriteAsync() {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationDictionaryImportOverwriteConfirmationTitle,
                Message = Strings_Translation.CreateTranslationDictionaryImportOverwriteConfirmationMessage,
                ConfirmButtonText = "上書き",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmCsvOverwriteAsync() {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationCsvImportOverwriteConfirmationTitle,
                Message = Strings_Translation.CreateTranslationCsvImportOverwriteConfirmationMessage,
                ConfirmButtonText = "上書き",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmDictionaryPartialImportAsync( int matchedCount ) {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationTitle,
                Message = string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationMessage, matchedCount ),
                ConfirmButtonText = "取り込む",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmArchiveContainsJapaneseDictionaryAsync() {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryConfirmationTitle,
                Message = GetJapaneseDictionaryEmbeddedMessage(),
                ConfirmButtonText = "継続",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmJapaneseDictionaryImportAsync() {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryImportConfirmationTitle,
                Message = Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryImportConfirmationMessage,
                ConfirmButtonText = "取り込む",
                CancelButtonText = "取り込まない",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmPoPartialImportAsync( int matchedCount ) {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationPoImportPartialConfirmationTitle,
                Message = string.Format( Strings_Translation.CreateTranslationPoImportPartialConfirmationMessage, matchedCount ),
                ConfirmButtonText = "取り込む",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private async Task<bool> ConfirmCsvPartialImportAsync( int matchedCount ) {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationCsvImportPartialConfirmationTitle,
                Message = string.Format( Strings_Translation.CreateTranslationCsvImportPartialConfirmationMessage, matchedCount ),
                ConfirmButtonText = "取り込む",
                CancelButtonText = "キャンセル",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }

    private IReadOnlyList<TranslationDictionaryItem> CreateCurrentDictionaryItems() =>
        [.. DictionaryItems.Select( item => new TranslationDictionaryItem( item.Key, item.Original ) {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled
        } )];

    private bool HasTranslatedText() => DictionaryItems.Any( item => !string.IsNullOrWhiteSpace( item.Translated ) );

    private string GetProjectIdVersion() => $"DCS Translation Japanese {applicationInfoService.GetVersion()}";

    private static string FormatPoTimestamp( DateTimeOffset value ) => value.ToString( "yyyy-MM-dd HH:mmzzz", CultureInfo.InvariantCulture );

    private string GetXGenerator() => $"{Strings_Shared.AppDisplayName} {applicationInfoService.GetVersion()}";

    private string GetDictionaryExportPath() {
        var exportDirectoryPath = GetExportDirectoryPath();
        return Path.Combine( exportDirectoryPath, "dictionary" );
    }

    private string GetPoExportPath() {
        var exportDirectoryPath = GetExportDirectoryPath();
        return Path.Combine( exportDirectoryPath, $"{Path.GetFileNameWithoutExtension( ArchiveFullPath )}.po" );
    }

    private string GetCsvExportPath() {
        var exportDirectoryPath = GetExportDirectoryPath();
        return Path.Combine( exportDirectoryPath, $"{Path.GetFileNameWithoutExtension( ArchiveFullPath )}.csv" );
    }

    private string GetPoImportInitialPath() {
        try {
            return GetPoExportPath();
        }
        catch {
            var archiveDirectory = Path.GetDirectoryName( ArchiveFullPath );
            return string.IsNullOrWhiteSpace( archiveDirectory )
                ? $"{Path.GetFileNameWithoutExtension( ArchiveFullPath )}.po"
                : Path.Combine( archiveDirectory, $"{Path.GetFileNameWithoutExtension( ArchiveFullPath )}.po" );
        }
    }

    private string GetDictionaryImportInitialPath() {
        try {
            return GetDictionaryExportPath();
        }
        catch {
            var archiveDirectory = Path.GetDirectoryName( ArchiveFullPath );
            return string.IsNullOrWhiteSpace( archiveDirectory )
                ? "dictionary"
                : Path.Combine( archiveDirectory, "dictionary" );
        }
    }

    private string GetCsvImportInitialPath() {
        try {
            return GetCsvExportPath();
        }
        catch {
            var archiveDirectory = Path.GetDirectoryName( ArchiveFullPath );
            return string.IsNullOrWhiteSpace( archiveDirectory )
                ? $"{Path.GetFileNameWithoutExtension( ArchiveFullPath )}.csv"
                : Path.Combine( archiveDirectory, $"{Path.GetFileNameWithoutExtension( ArchiveFullPath )}.csv" );
        }
    }

    private string GetExportDirectoryPath() {
        var translateFileDir = appSettingsService.Settings.TranslateFileDir;

        if(TryBuildExportPath(
            appSettingsService.Settings.DcsWorldInstallDir,
            "DCSWorld",
            out var dcsWorldPath )) {
            return dcsWorldPath;
        }

        if(TryBuildExportPath(
            appSettingsService.Settings.SourceUserMissionDir,
            "UserMissions",
            out var userMissionPath )) {
            return userMissionPath;
        }

        throw new InvalidOperationException( "アーカイブが既知のルート配下に存在しません。" );

        bool TryBuildExportPath( string baseDirectory, string relativeRoot, out string exportDirectoryPath ) {
            exportDirectoryPath = string.Empty;
            if(string.IsNullOrWhiteSpace( baseDirectory )) {
                return false;
            }

            var normalizedBasePath = Path.GetFullPath( baseDirectory );
            var normalizedArchivePath = Path.GetFullPath( ArchiveFullPath );
            if(!IsPathWithinBaseDirectory( normalizedBasePath, normalizedArchivePath )) {
                return false;
            }

            var relativePath = Path.GetRelativePath( normalizedBasePath, normalizedArchivePath );
            exportDirectoryPath = Path.Combine( translateFileDir, relativeRoot, relativePath, "l10n", "JP" );
            return true;
        }
    }

    private static bool IsPathWithinBaseDirectory( string baseDirectory, string targetPath ) {
        var relativePath = Path.GetRelativePath( baseDirectory, targetPath );
        return !relativePath.StartsWith( "..", StringComparison.Ordinal )
            && !Path.IsPathRooted( relativePath );
    }

    private static (string Context, string Original) CreateTranslationPair( string context, string original ) => (
        NormalizeTranslationPairValue( context ),
        NormalizeTranslationPairValue( original ));

    private static string NormalizeTranslationPairValue( string value ) => value
        .Replace( "\r\n", "\n", StringComparison.Ordinal )
        .Replace( '\r', '\n' );

    private string GetJapaneseDictionaryEmbeddedMessage() =>
        string.Format(
            GetArchiveTypeSpecificJapaneseDictionaryEmbeddedMessage(),
            ArchiveFullPath );

    private string GetArchiveTypeSpecificJapaneseDictionaryEmbeddedMessage() =>
        Path.GetExtension( ArchiveFullPath ).Equals( ".trk", StringComparison.OrdinalIgnoreCase )
            ? Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryTrkConfirmationMessage
            : Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryMizConfirmationMessage;

    private void SetDictionaryLoadFailedState() {
        _loadedDictionaryItems = [];
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadFailedMessage;
        DictionaryItems = [];
        SelectedDictionaryItem = null;
        NotifyDictionaryAvailabilityChanged();
    }

    private void NotifyDictionaryAvailabilityChanged() {
        NotifyOfPropertyChange( nameof( CanExport ) );
        NotifyOfPropertyChange( nameof( CanImport ) );
        NotifyOfPropertyChange( nameof( CanImportDictionary ) );
        NotifyOfPropertyChange( nameof( CanImportCsv ) );
        NotifyOfPropertyChange( nameof( CanImportPo ) );
    }

    private void ShowExportSucceededSnackbar( string exportPath ) {
        var exportDirectoryPath = Path.GetDirectoryName( exportPath );
        if(string.IsNullOrWhiteSpace( exportDirectoryPath )) {
            logger.Warn( $"dictionary 書き出し成功後の保存先ディレクトリ解決に失敗した。Path={exportPath}" );
            return;
        }

        MessageQueue.Enqueue(
            "書き出しが完了しました",
            "開く",
            new Action<object?>( OpenExportDirectory ),
            (object?)exportDirectoryPath,
            false,
            false,
            null );
    }

    private void ShowCompletedSnackbar( string message ) =>
        MessageQueue.Enqueue(
            message,
            (string?)null,
            null,
            null,
            false,
            false,
            null );

    private void OpenExportDirectory( object? exportDirectoryPathObject ) {
        if(exportDirectoryPathObject is not string exportDirectoryPath || string.IsNullOrWhiteSpace( exportDirectoryPath )) {
            logger.Warn( "Snackbar から渡された保存先ディレクトリが不正なため開く処理を中断する。" );
            return;
        }

        logger.Info( $"dictionary 保存先ディレクトリを開く。Directory={exportDirectoryPath}" );
        systemService.OpenDirectory( exportDirectoryPath );
    }

    private sealed record PoImportAnalysis( bool IsFullMatch, IReadOnlyList<PoImportMatch> Matches );

    private sealed record PoImportMatch( TranslationDictionaryItemRowViewModel Row, string Translated, bool IsEnabled );

    private sealed record DictionaryImportAnalysis( bool IsFullMatch, IReadOnlyList<DictionaryImportMatch> Matches );

    private sealed record DictionaryImportMatch( TranslationDictionaryItemRowViewModel Row, string Translated );

    private sealed record CsvImportAnalysis( bool IsFullMatch, IReadOnlyList<CsvImportMatch> Matches );

    private sealed record CsvImportMatch( TranslationDictionaryItemRowViewModel Row, string Translated, bool IsEnabled );

    private sealed record DictionaryLoadState(
        IReadOnlyList<TranslationDictionaryItem> LoadedItems,
        IReadOnlyList<TranslationDictionaryItemRowViewModel> RowItems );

    private enum TranslationImportFormat {
        Dictionary,
        Po,
        Csv
    }

    private enum TranslationExportFormat {
        Dictionary,
        Po,
        Csv
    }
}