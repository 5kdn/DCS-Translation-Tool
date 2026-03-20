using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Resources;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// 翻訳作成ウィンドウの状態を管理する ViewModel。
/// </summary>
/// <param name="archiveFullPath">翻訳対象のアーカイブ絶対パス。</param>
/// <param name="appSettingsService">アプリケーション設定サービス。</param>
/// <param name="systemService">システム連携サービス。</param>
/// <param name="logger">ロギングサービス。</param>
/// <param name="session">翻訳作成編集セッション。</param>
/// <param name="workflowService">TranslationCreation 用ワークフローサービス。</param>
/// <param name="layoutStateService">レイアウト状態サービス。</param>
/// <param name="dialogService">TranslationCreation 用ダイアログサービス。</param>
/// <param name="filterService">一覧フィルター判定サービス。</param>
/// <param name="notificationService">通知表示サービス。</param>
/// <exception cref="ArgumentException"><paramref name="archiveFullPath"/> が空白の場合に送出する。</exception>
public sealed class TranslationCreationViewModel(
    string archiveFullPath,
    IAppSettingsService appSettingsService,
    ISystemService systemService,
    ILoggingService logger,
    ITranslationCreationSession session,
    ITranslationCreationWorkflowService workflowService,
    ITranslationCreationLayoutStateService layoutStateService,
    ITranslationCreationDialogService dialogService,
    ITranslationCreationFilterService filterService,
    ITranslationCreationNotificationService notificationService ) : Screen, ITranslationCreationViewModel {
    #region Constants
    /// <summary>
    /// 既定のウィンドウ幅を表す。
    /// </summary>
    public const double DefaultWindowWidth = TranslationCreationLayoutDefaults.DefaultWindowWidth;

    /// <summary>
    /// 既定のウィンドウ高さを表す。
    /// </summary>
    public const double DefaultWindowHeight = TranslationCreationLayoutDefaults.DefaultWindowHeight;

    /// <summary>
    /// ウィンドウ幅の最小値を表す。
    /// </summary>
    public const double MinWindowWidth = TranslationCreationLayoutDefaults.MinWindowWidth;

    /// <summary>
    /// ウィンドウ高さの最小値を表す。
    /// </summary>
    public const double MinWindowHeight = TranslationCreationLayoutDefaults.MinWindowHeight;

    /// <summary>
    /// dictionary 領域比率の既定値を表す。
    /// </summary>
    public const double DefaultDictionaryPaneRatio = TranslationCreationLayoutDefaults.DefaultDictionaryPaneRatio;

    /// <summary>
    /// dictionary 領域比率の最小値を表す。
    /// </summary>
    public const double MinDictionaryPaneRatio = TranslationCreationLayoutDefaults.MinDictionaryPaneRatio;

    /// <summary>
    /// dictionary 領域比率の最大値を表す。
    /// </summary>
    public const double MaxDictionaryPaneRatio = TranslationCreationLayoutDefaults.MaxDictionaryPaneRatio;

    #endregion

    #region Fields

    private TranslationCreationImportFormat _selectedImportFormat = TranslationCreationImportFormat.Dictionary;
    private TranslationCreationExportFormat _selectedExportFormat = TranslationCreationExportFormat.Dictionary;
    private ObservableCollection<TranslationDictionaryItemRowViewModel> _dictionaryItems = [];
    private bool _isLoading;
    private string _statusMessage = string.Empty;
    private bool _showEnabledItems = true;
    private bool _showDisabledItems = true;
    private bool _showOnlyUntranslated;
    private bool _hidePossibleNonTranslationTargets = true;
    private bool _hideEmptyOriginal = true;
    private bool _hasProcessedJapaneseDictionaryPrompt;
    private Task? _initializeAfterShownTask;
    private bool _hasJapaneseDictionary;
    private IReadOnlyList<TranslationDictionaryItem> _japaneseDictionaryItems = [];
    private double _windowWidth = layoutStateService.Load().WindowWidth;
    private double _windowHeight = layoutStateService.Load().WindowHeight;
    private double _dictionaryPaneRatio = layoutStateService.Load().DictionaryPaneRatio;
    private bool _isDictionaryDetailsWrapEnabled = layoutStateService.Load().IsDictionaryDetailsWrapEnabled;
    private bool _sessionEventsSubscribed;
    private DispatcherTimer? _selectedTranslatedCommitTimer;
    private bool _suppressRowViewModelSync;
    private int _visibleDictionaryItemsVersion;
    private readonly Dictionary<TranslationCreationRowState, TranslationDictionaryItemRowViewModel> _rowViewModelsByState = [];
    private readonly Dictionary<TranslationDictionaryItemRowViewModel, TranslationCreationRowState> _rowStatesByViewModel = [];
    #endregion

    #region Properties

    /// <summary>
    /// ウィンドウの表示名を取得する。
    /// </summary>
    public string WindowTitle { get; } = Strings_Translation.CreateTranslationWindowTitle;

    /// <summary>
    /// TranslationCreation Window 専用の Snackbar メッセージキューを取得する。
    /// </summary>
    public SnackbarMessageQueue MessageQueue => notificationService.MessageQueue;

    /// <summary>
    /// 上方向へ選択移動するコマンドを取得する。
    /// </summary>
    public ICommand MoveSelectionUpCommand => new RelayCommand( ExecuteMoveSelectionUp );

    /// <summary>
    /// 下方向へ選択移動するコマンドを取得する。
    /// </summary>
    public ICommand MoveSelectionDownCommand => new RelayCommand( ExecuteMoveSelectionDown );

    /// <summary>
    /// ウィンドウ幅を取得または設定する。
    /// </summary>
    public double WindowWidth {
        get => _windowWidth;
        set {
            var normalizedValue = TranslationCreationLayoutDefaults.NormalizeWindowLength(
                value,
                TranslationCreationLayoutDefaults.DefaultWindowWidth,
                TranslationCreationLayoutDefaults.MinWindowWidth );
            if(!Set( ref _windowWidth, normalizedValue )) {
                return;
            }

            SaveLayoutState();
        }
    }

    /// <summary>
    /// ウィンドウ高さを取得または設定する。
    /// </summary>
    public double WindowHeight {
        get => _windowHeight;
        set {
            var normalizedValue = TranslationCreationLayoutDefaults.NormalizeWindowLength(
                value,
                TranslationCreationLayoutDefaults.DefaultWindowHeight,
                TranslationCreationLayoutDefaults.MinWindowHeight );
            if(!Set( ref _windowHeight, normalizedValue )) {
                return;
            }

            SaveLayoutState();
        }
    }

    /// <summary>
    /// dictionary 領域比率を取得または設定する。
    /// </summary>
    public double DictionaryPaneRatio {
        get => _dictionaryPaneRatio;
        set {
            var normalizedValue = TranslationCreationLayoutDefaults.NormalizeDictionaryPaneRatio( value );
            if(!Set( ref _dictionaryPaneRatio, normalizedValue )) {
                return;
            }

            SaveLayoutState();
        }
    }

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

            NotifyOfPropertyChange( nameof( HasDictionaryItems ) );
            NotifyVisibleDictionaryItemsChanged();
        }
    }

    /// <summary>
    /// 選択中の dictionary 項目を取得または設定する。
    /// </summary>
    public TranslationDictionaryItemRowViewModel? SelectedDictionaryItem {
        get => session.SelectedRow is { } row && _rowViewModelsByState.TryGetValue( row, out var viewModel )
            ? viewModel
            : null;
        set {
            CancelSelectedTranslatedCommit();
            session.SelectedRow = value is not null && _rowStatesByViewModel.TryGetValue( value, out var row )
                ? row
                : null;
        }
    }

    /// <summary>
    /// 選択中項目の Original を取得する。
    /// </summary>
    public string SelectedOriginal => session.SelectedOriginal;

    /// <summary>
    /// 選択中項目の Translated を取得または設定する。
    /// </summary>
    public string SelectedTranslated {
        get => session.SelectedTranslatedDraft;
        set {
            session.SelectedTranslatedDraft = value;
            ScheduleSelectedTranslatedCommit();
        }
    }

    /// <summary>
    /// 選択中項目の翻訳文を編集可能かどうかを取得する。
    /// </summary>
    public bool CanEditSelectedTranslated => session.CanEditSelectedTranslated;

    /// <summary>
    /// dictionary 詳細テキストを右端で折り返すかどうかを取得または設定する。
    /// </summary>
    public bool IsDictionaryDetailsWrapEnabled {
        get => _isDictionaryDetailsWrapEnabled;
        set {
            if(!Set( ref _isDictionaryDetailsWrapEnabled, value )) {
                return;
            }

            SaveLayoutState();
        }
    }

    /// <summary>
    /// 読み込み中かどうかを取得または設定する。
    /// </summary>
    public bool IsLoading {
        get => _isLoading;
        private set {
            if(!Set( ref _isLoading, value )) {
                return;
            }

            NotifyDictionaryAvailabilityChanged();
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
    /// 可視 dictionary 項目の再評価版数を取得する。
    /// </summary>
    public int VisibleDictionaryItemsVersion => _visibleDictionaryItemsVersion;

    /// <summary>
    /// 現在の読み込み主動作用表示文言を取得する。
    /// </summary>
    public string ImportSplitButtonContent => _selectedImportFormat switch
    {
        TranslationCreationImportFormat.Dictionary => Strings_Translation.CreateTranslationImportDictionaryButtonContent,
        TranslationCreationImportFormat.Po => Strings_Translation.CreateTranslationImportPoSplitButtonContent,
        TranslationCreationImportFormat.Csv => Strings_Translation.CreateTranslationImportCsvButtonContent,
        _ => Strings_Translation.CreateTranslationImportPoSplitButtonContent
    };

    /// <summary>
    /// 現在の書き出し主動作用表示文言を取得する。
    /// </summary>
    public string ExportSplitButtonContent => _selectedExportFormat switch
    {
        TranslationCreationExportFormat.Dictionary => Strings_Translation.CreateTranslationExportButtonContent,
        TranslationCreationExportFormat.Po => Strings_Translation.CreateTranslationExportPoSplitButtonContent,
        TranslationCreationExportFormat.Csv => Strings_Translation.CreateTranslationExportCsvButtonContent,
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

            NotifyVisibleDictionaryItemsChanged();
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

            NotifyVisibleDictionaryItemsChanged();
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

            NotifyVisibleDictionaryItemsChanged();
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

            NotifyVisibleDictionaryItemsChanged();
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

            NotifyVisibleDictionaryItemsChanged();
        }
    }
    #endregion

    #region Lifecycle

    /// <summary>
    /// アクティブ化完了時に表示準備のみを行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    protected override async Task OnActivatedAsync( CancellationToken cancellationToken ) {
        SubscribeSessionEvents();
        await base.OnActivatedAsync( cancellationToken );
    }

    /// <summary>
    /// ウィンドウ表示後の初期化を一度だけ実行する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    internal Task InitializeAfterShownAsync( CancellationToken cancellationToken = default ) =>
        _initializeAfterShownTask ??= LoadDictionaryAsync( cancellationToken );

    /// <summary>
    /// ウィンドウ表示後に dictionary 読込と埋め込みJP dictionary の確認を行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    public async Task HandleWindowLoadedAsync( CancellationToken cancellationToken = default ) {
        await InitializeAfterShownAsync( cancellationToken );
        if(_hasProcessedJapaneseDictionaryPrompt) {
            return;
        }

        _hasProcessedJapaneseDictionaryPrompt = true;
        cancellationToken.ThrowIfCancellationRequested();

        var promptResult = await workflowService.HandleEmbeddedJapaneseDictionaryAsync(
            ArchiveFullPath,
            CreateImportContext(),
            _hasJapaneseDictionary,
            _japaneseDictionaryItems,
            cancellationToken );
        if(promptResult.CommandResult is not null) {
            ApplyCommandResult( promptResult.CommandResult );
        }

        if(promptResult.ShouldCloseWindow) {
            await TryCloseAsync( false );
        }
    }
    #endregion

    #region ActionGuards

    /// <summary>
    /// dictionary を書き出し可能かどうかを取得する。
    /// </summary>
    public bool CanExport =>
        !IsLoading
        && session.HasLoadedItems
        && !string.IsNullOrWhiteSpace( appSettingsService.Settings.TranslateFileDir );

    /// <summary>
    /// dictionary ファイルを読み込み可能かどうかを取得する。
    /// </summary>
    public bool CanImportDictionary => !IsLoading && session.HasLoadedItems;

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
    /// 閉じる確認が必要な未反映変更が存在するかどうかを判定する。
    /// </summary>
    internal bool HasPendingChangesForClose() => session.HasPendingChangesForClose();
    #endregion

    #region Actions

    /// <summary>
    /// 表示中の dictionary 項目選択を 1 件上へ移動する。
    /// </summary>
    /// <returns>選択項目が変化した場合は <see langword="true"/> を返す。</returns>
    public bool MoveSelectionUp() => session.MoveSelection( GetVisibleRows(), -1 );

    /// <summary>
    /// 表示中の dictionary 項目選択を 1 件下へ移動する。
    /// </summary>
    /// <returns>選択項目が変化した場合は <see langword="true"/> を返す。</returns>
    public bool MoveSelectionDown() => session.MoveSelection( GetVisibleRows(), 1 );

    /// <summary>
    /// 上方向への選択移動コマンドを実行する。
    /// </summary>
    private void ExecuteMoveSelectionUp() => MoveSelectionUp();

    /// <summary>
    /// 下方向への選択移動コマンドを実行する。
    /// </summary>
    private void ExecuteMoveSelectionDown() => MoveSelectionDown();

    /// <summary>
    /// 現在選択中の読み込み形式を実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public Task ExecuteImportAsync() => _selectedImportFormat switch
    {
        TranslationCreationImportFormat.Dictionary => ImportDictionaryAsync(),
        TranslationCreationImportFormat.Csv => ImportCsvAsync(),
        _ => ImportPoAsync()
    };

    /// <summary>
    /// 現在選択中の書き出し形式を実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public Task ExecuteExportAsync() => _selectedExportFormat switch
    {
        TranslationCreationExportFormat.Po => ExportPoAsync(),
        TranslationCreationExportFormat.Csv => ExportCsvAsync(),
        _ => ExportAsync()
    };

    /// <summary>
    /// 読み込み形式を PO に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task SelectImportPoAsync() {
        SetSelectedImportFormat( TranslationCreationImportFormat.Po );
        await ImportPoAsync();
    }

    /// <summary>
    /// 読み込み形式を dictionary に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task SelectImportDictionaryAsync() {
        SetSelectedImportFormat( TranslationCreationImportFormat.Dictionary );
        await ImportDictionaryAsync();
    }

    /// <summary>
    /// 読み込み形式を CSV に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task SelectImportCsvAsync() {
        SetSelectedImportFormat( TranslationCreationImportFormat.Csv );
        await ImportCsvAsync();
    }

    /// <summary>
    /// 書き出し形式を dictionary に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task SelectExportDictionaryAsync() {
        SetSelectedExportFormat( TranslationCreationExportFormat.Dictionary );
        await ExportAsync();
    }

    /// <summary>
    /// 書き出し形式を PO に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task SelectExportPoAsync() {
        SetSelectedExportFormat( TranslationCreationExportFormat.Po );
        await ExportPoAsync();
    }

    /// <summary>
    /// 書き出し形式を CSV に切り替えて実行する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task SelectExportCsvAsync() {
        SetSelectedExportFormat( TranslationCreationExportFormat.Csv );
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
        notificationService.ShowCompleted( Strings_Translation.CreateTranslationOriginalCopiedMessage );
    }

    /// <summary>
    /// ウィンドウを閉じてもよいかどうかを確認する。
    /// </summary>
    /// <returns>ウィンドウを閉じてよい場合は <see langword="true"/> を返す。</returns>
    public async Task<bool> ConfirmCloseAsync() {
        if(!session.HasPendingChangesForClose()) {
            return true;
        }

        return await dialogService.ConfirmCloseAsync();
    }

    /// <summary>
    /// 編集結果を翻訳ファイルとして書き出す。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task ExportAsync() {
        if(!CanExport) {
            logger.Warn( "dictionary を書き出せない状態のため処理を中断する。" );
            return;
        }

        await ExecuteWorkflowCommandAsync( () => workflowService.ExportAsync(
            ArchiveFullPath,
            TranslationCreationExportFormat.Dictionary,
            CreateDocumentSnapshot() ) );
    }

    /// <summary>
    /// 編集結果を PO ファイルとして書き出す。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task ExportPoAsync() {
        if(!CanExport) {
            logger.Warn( "PO を書き出せない状態のため処理を中断する。" );
            return;
        }

        await ExecuteWorkflowCommandAsync( () => workflowService.ExportAsync(
            ArchiveFullPath,
            TranslationCreationExportFormat.Po,
            CreateDocumentSnapshot() ) );
    }

    /// <summary>
    /// 編集結果を CSV ファイルとして書き出す。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task ExportCsvAsync() {
        if(!CanExport) {
            logger.Warn( "CSV を書き出せない状態のため処理を中断する。" );
            return;
        }

        await ExecuteWorkflowCommandAsync( () => workflowService.ExportAsync(
            ArchiveFullPath,
            TranslationCreationExportFormat.Csv,
            CreateDocumentSnapshot() ) );
    }

    /// <summary>
    /// PO ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task ImportPoAsync() {
        if(!CanImportPo) {
            logger.Warn( "PO を読み込めない状態のため処理を中断する。" );
            return;
        }

        await ExecuteWorkflowCommandAsync( () => workflowService.ImportAsync(
            ArchiveFullPath,
            TranslationCreationImportFormat.Po,
            CreateImportContext() ) );
    }

    /// <summary>
    /// dictionary ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task ImportDictionaryAsync() {
        if(!CanImportDictionary) {
            logger.Warn( "dictionary を読み込めない状態のため処理を中断する。" );
            return;
        }

        await ExecuteWorkflowCommandAsync( () => workflowService.ImportAsync(
            ArchiveFullPath,
            TranslationCreationImportFormat.Dictionary,
            CreateImportContext() ) );
    }

    /// <summary>
    /// CSV ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスクを返す。</returns>
    public async Task ImportCsvAsync() {
        if(!CanImportCsv) {
            logger.Warn( "CSV を読み込めない状態のため処理を中断する。" );
            return;
        }

        await ExecuteWorkflowCommandAsync( () => workflowService.ImportAsync(
            ArchiveFullPath,
            TranslationCreationImportFormat.Csv,
            CreateImportContext() ) );
    }

    /// <summary>
    /// 選択中翻訳文の保留中編集を現在の行へ反映する。
    /// </summary>
    internal void FlushPendingSelectedTranslatedEdit() {
        CancelSelectedTranslatedCommit();
        session.FlushPendingSelectedTranslatedEdit();
    }

    /// <summary>
    /// dictionary 領域比率を有効範囲へ正規化する。
    /// </summary>
    /// <param name="ratio">検証対象の比率。</param>
    /// <returns>有効範囲内へ補正した比率を返す。</returns>
    public static double NormalizeDictionaryPaneRatio( double ratio ) =>
        TranslationCreationLayoutDefaults.NormalizeDictionaryPaneRatio( ratio );

    /// <summary>
    /// ウィンドウサイズを有効範囲へ正規化する。
    /// </summary>
    /// <param name="value">検証対象のサイズ。</param>
    /// <param name="fallback">不正値時の既定サイズ。</param>
    /// <param name="minimum">許容する最小サイズ。</param>
    /// <returns>有効範囲内へ補正したサイズを返す。</returns>
    public static double NormalizeWindowLength( double value, double fallback, double minimum ) =>
        TranslationCreationLayoutDefaults.NormalizeWindowLength( value, fallback, minimum );
    #endregion

    #region PrivateHelpers

    /// <summary>
    /// アーカイブから dictionary を読み込んで画面状態へ反映する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    private async Task LoadDictionaryAsync( CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        IsLoading = true;
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadingMessage;

        try {
            var result = await workflowService.LoadAsync( ArchiveFullPath, cancellationToken ).ConfigureAwait( false );

            await Execute.OnUIThreadAsync( () => {
                ApplyLoadResult( result );
                NotifyDictionaryAvailabilityChanged();
                return Task.CompletedTask;
            } ).ConfigureAwait( false );
        }
        catch(Exception ex) {
            logger.Error( $"TranslationCreationViewModel の dictionary 読込中に例外が発生した。Archive={ArchiveFullPath}", ex );
            await Execute.OnUIThreadAsync( () => {
                SetDictionaryLoadFailedState();
                return Task.CompletedTask;
            } ).ConfigureAwait( false );
        }
        finally {
            await Execute.OnUIThreadAsync( () => {
                IsLoading = false;
                return Task.CompletedTask;
            } ).ConfigureAwait( false );
        }
    }

    /// <summary>
    /// セッション変更通知の購読を開始する。
    /// </summary>
    private void SubscribeSessionEvents() {
        if(_sessionEventsSubscribed) {
            return;
        }

        session.PropertyChanged += OnSessionPropertyChanged;
        session.RowPropertyChanged += OnSessionRowPropertyChanged;
        _sessionEventsSubscribed = true;
    }

    /// <summary>
    /// セッションのプロパティ変更を ViewModel の通知へ橋渡しする。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSessionPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(string.IsNullOrWhiteSpace( e.PropertyName )) {
            return;
        }

        switch(e.PropertyName) {
            case nameof( ITranslationCreationSession.SelectedRow ):
                CancelSelectedTranslatedCommit();
                NotifyOfPropertyChange( nameof( SelectedDictionaryItem ) );
                break;
            case nameof( ITranslationCreationSession.SelectedOriginal ):
                NotifyOfPropertyChange( nameof( SelectedOriginal ) );
                break;
            case nameof( ITranslationCreationSession.SelectedTranslatedDraft ):
                NotifyOfPropertyChange( nameof( SelectedTranslated ) );
                break;
            case nameof( ITranslationCreationSession.CanEditSelectedTranslated ):
                NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
                break;
        }
    }

    /// <summary>
    /// 行変更に応じてフィルター再適用を行う。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSessionRowPropertyChanged( object? sender, TranslationCreationRowPropertyChangedEventArgs e ) {
        if(_rowViewModelsByState.TryGetValue( e.Row, out var rowViewModel )) {
            ApplyRowStateToViewModel( e.Row, rowViewModel );
        }

        if(e.PropertyName == nameof( TranslationCreationRowState.Translated ) && ShowOnlyUntranslated) {
            NotifyVisibleDictionaryItemsChanged();
        }

        if(e.PropertyName == nameof( TranslationCreationRowState.IsEnabled ) && (!ShowEnabledItems || !ShowDisabledItems)) {
            NotifyVisibleDictionaryItemsChanged();
        }
    }

    /// <summary>
    /// セッションの行一覧を ViewModel へ反映する。
    /// </summary>
    private void ApplySessionRows() {
        ResetRowViewModelMappings();

        var rowViewModels = session.Rows
            .Select( static row => new TranslationDictionaryItemRowViewModel( row.ToTranslationDictionaryItem(), row.IsPossibleNonTranslationTarget ) )
            .ToArray();
        for(var i = 0; i < rowViewModels.Length; i++) {
            var rowState = session.Rows[i];
            var rowViewModel = rowViewModels[i];
            _rowViewModelsByState[rowState] = rowViewModel;
            _rowStatesByViewModel[rowViewModel] = rowState;
            rowViewModel.PropertyChanged += OnRowViewModelPropertyChanged;
        }

        DictionaryItems = [.. rowViewModels];
        NotifyOfPropertyChange( nameof( SelectedDictionaryItem ) );
        NotifyOfPropertyChange( nameof( SelectedOriginal ) );
        NotifyOfPropertyChange( nameof( SelectedTranslated ) );
        NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
    }

    /// <summary>
    /// 現在のフィルタ条件に基づいて行の表示可否を判定する。
    /// </summary>
    /// <param name="row">判定対象の行。</param>
    /// <returns>表示対象の場合は <see langword="true"/> を返す。</returns>
    public bool ShouldIncludeRow( TranslationDictionaryItemRowViewModel row ) =>
        filterService.ShouldInclude( row, CreateFilterOptions() );

    /// <summary>
    /// 現在の一覧フィルター条件を生成する。
    /// </summary>
    /// <returns>生成したフィルター条件を返す。</returns>
    private TranslationCreationFilterOptions CreateFilterOptions() =>
        new( ShowEnabledItems, ShowDisabledItems, ShowOnlyUntranslated, HidePossibleNonTranslationTargets, HideEmptyOriginal );

    /// <summary>
    /// 現在の表示行に対応するセッション行一覧を取得する。
    /// </summary>
    /// <returns>現在表示中のセッション行一覧を返す。</returns>
    private IReadOnlyList<TranslationCreationRowState> GetVisibleRows() =>
        [.. DictionaryItems
            .Where( ShouldIncludeRow )
            .Select( rowViewModel => _rowStatesByViewModel[rowViewModel] )];

    /// <summary>
    /// 選択中の読み込み形式を更新する。
    /// </summary>
    /// <param name="format">設定する形式。</param>
    private void SetSelectedImportFormat( TranslationCreationImportFormat format ) {
        if(_selectedImportFormat == format) {
            return;
        }

        _selectedImportFormat = format;
        NotifyOfPropertyChange( nameof( ImportSplitButtonContent ) );
    }

    /// <summary>
    /// 選択中の書き出し形式を更新する。
    /// </summary>
    /// <param name="format">設定する形式。</param>
    private void SetSelectedExportFormat( TranslationCreationExportFormat format ) {
        if(_selectedExportFormat == format) {
            return;
        }

        _selectedExportFormat = format;
        NotifyOfPropertyChange( nameof( ExportSplitButtonContent ) );
    }

    /// <summary>
    /// 共通の操作実行フローを実行する。
    /// </summary>
    /// <param name="operation">実行対象の操作。</param>
    /// <returns>非同期タスクを返す。</returns>
    private async Task ExecuteWorkflowCommandAsync( Func<Task<TranslationCreationCommandResult>> operation ) {
        try {
            IsLoading = true;
            var result = await operation();
            ApplyCommandResult( result );
        }
        finally {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 操作結果を画面状態と通知へ反映する。
    /// </summary>
    /// <param name="result">反映対象の操作結果。</param>
    private void ApplyCommandResult( TranslationCreationCommandResult result ) {
        if(!string.IsNullOrWhiteSpace( result.StatusMessage )) {
            StatusMessage = result.StatusMessage;
        }

        if(result.WasCancelled) {
            return;
        }

        switch(result.NotificationKind) {
            case TranslationCreationNotificationKind.Completed when !string.IsNullOrWhiteSpace( result.StatusMessage ):
                notificationService.ShowCompleted( result.StatusMessage );
                break;
            case TranslationCreationNotificationKind.ExportSucceeded when !string.IsNullOrWhiteSpace( result.OutputPath ):
                notificationService.ShowExportSucceeded( result.OutputPath );
                break;
        }
    }

    /// <summary>
    /// 読込結果を画面状態へ反映する。
    /// </summary>
    /// <param name="result">反映対象の読込結果。</param>
    private void ApplyLoadResult( TranslationCreationLoadResult result ) {
        _hasJapaneseDictionary = result.HasJapaneseDictionary;
        _japaneseDictionaryItems = result.JapaneseDictionaryItems;
        StatusMessage = result.StatusMessage;

        if(result.IsSuccess) {
            session.Load( result.LoadState );
            ApplySessionRows();
            return;
        }

        session.Load( new TranslationCreationDictionaryLoadState( [], [] ) );
        ApplySessionRows();
    }

    /// <summary>
    /// dictionary 読込失敗時の画面状態へ切り替える。
    /// </summary>
    private void SetDictionaryLoadFailedState() {
        _hasJapaneseDictionary = false;
        _japaneseDictionaryItems = [];
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadFailedMessage;
        session.Load( new TranslationCreationDictionaryLoadState( [], [] ) );
        ApplySessionRows();
        NotifyDictionaryAvailabilityChanged();
    }

    /// <summary>
    /// dictionary 可用性に依存するプロパティ変更通知を発行する。
    /// </summary>
    private void NotifyDictionaryAvailabilityChanged() {
        NotifyOfPropertyChange( nameof( CanExport ) );
        NotifyOfPropertyChange( nameof( CanImport ) );
        NotifyOfPropertyChange( nameof( CanImportDictionary ) );
        NotifyOfPropertyChange( nameof( CanImportCsv ) );
        NotifyOfPropertyChange( nameof( CanImportPo ) );
    }

    /// <summary>
    /// 現在の編集状態から書き出し用スナップショットを生成する。
    /// </summary>
    /// <returns>生成したスナップショットを返す。</returns>
    private TranslationCreationDocumentSnapshot CreateDocumentSnapshot() {
        FlushPendingSelectedTranslatedEdit();
        return session.CreateDocumentSnapshot();
    }

    /// <summary>
    /// 現在の編集状態から取り込み用コンテキストを生成する。
    /// </summary>
    /// <returns>生成した取り込み用コンテキストを返す。</returns>
    private TranslationCreationImportContext CreateImportContext() {
        FlushPendingSelectedTranslatedEdit();
        return new TranslationCreationImportContext( DictionaryItems, session.HasAnyTranslatedText() );
    }

    /// <summary>
    /// ViewModel 側の遅延反映タイマーを取得する。
    /// </summary>
    private DispatcherTimer SelectedTranslatedCommitTimer => _selectedTranslatedCommitTimer ??= CreateSelectedTranslatedCommitTimer();

    /// <summary>
    /// 選択中翻訳文の遅延反映タイマーを生成する。
    /// </summary>
    /// <returns>生成したタイマーを返す。</returns>
    private DispatcherTimer CreateSelectedTranslatedCommitTimer() {
        var timer = new DispatcherTimer( DispatcherPriority.Background )
        {
            Interval = TimeSpan.FromMilliseconds( 250 )
        };
        timer.Tick += OnSelectedTranslatedCommitTimerTick;
        return timer;
    }

    /// <summary>
    /// 遅延反映タイマー満了時に保留中の編集を確定する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectedTranslatedCommitTimerTick( object? sender, EventArgs e ) => FlushPendingSelectedTranslatedEdit();

    /// <summary>
    /// 選択中翻訳文の遅延反映を予約する。
    /// </summary>
    private void ScheduleSelectedTranslatedCommit() {
        SelectedTranslatedCommitTimer.Stop();
        SelectedTranslatedCommitTimer.Start();
    }

    /// <summary>
    /// 選択中翻訳文の遅延反映を取り消す。
    /// </summary>
    private void CancelSelectedTranslatedCommit() => _selectedTranslatedCommitTimer?.Stop();

    /// <summary>
    /// セッション行状態を View 用行 ViewModel へ反映する。
    /// </summary>
    /// <param name="rowState">反映元の行状態。</param>
    /// <param name="rowViewModel">反映先の行 ViewModel。</param>
    private void ApplyRowStateToViewModel( TranslationCreationRowState rowState, TranslationDictionaryItemRowViewModel rowViewModel ) {
        if(_suppressRowViewModelSync) {
            return;
        }

        _suppressRowViewModelSync = true;
        try {
            rowViewModel.Translated = rowState.Translated;
            rowViewModel.IsEnabled = rowState.IsEnabled;
        }
        finally {
            _suppressRowViewModelSync = false;
        }
    }

    /// <summary>
    /// 行 ViewModel の変更をセッション行状態へ同期する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnRowViewModelPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(_suppressRowViewModelSync || sender is not TranslationDictionaryItemRowViewModel rowViewModel || string.IsNullOrWhiteSpace( e.PropertyName )) {
            return;
        }

        if(!_rowStatesByViewModel.TryGetValue( rowViewModel, out var rowState )) {
            return;
        }

        switch(e.PropertyName) {
            case nameof( TranslationDictionaryItemRowViewModel.Translated ):
                if(ReferenceEquals( rowViewModel, SelectedDictionaryItem ) && !IsSelectedTranslatedCommitPending) {
                    session.SelectedTranslatedDraft = rowViewModel.Translated;
                }

                rowState.Translated = rowViewModel.Translated;
                break;
            case nameof( TranslationDictionaryItemRowViewModel.IsEnabled ):
                rowState.IsEnabled = rowViewModel.IsEnabled;
                break;
        }
    }

    /// <summary>
    /// 行 ViewModel と行状態の対応を破棄する。
    /// </summary>
    private void ResetRowViewModelMappings() {
        foreach(var rowViewModel in _rowStatesByViewModel.Keys) {
            rowViewModel.PropertyChanged -= OnRowViewModelPropertyChanged;
        }

        _rowViewModelsByState.Clear();
        _rowStatesByViewModel.Clear();
    }

    /// <summary>
    /// 可視 dictionary 項目の再評価通知を発行する。
    /// </summary>
    private void NotifyVisibleDictionaryItemsChanged() {
        _visibleDictionaryItemsVersion++;
        NotifyOfPropertyChange( nameof( VisibleDictionaryItemsVersion ) );
    }

    /// <summary>
    /// 選択中翻訳文の遅延反映が保留中かどうかを取得する。
    /// </summary>
    private bool IsSelectedTranslatedCommitPending => _selectedTranslatedCommitTimer?.IsEnabled == true;

    /// <summary>
    /// 現在のレイアウト状態を永続化する。
    /// </summary>
    private void SaveLayoutState() =>
        layoutStateService.Save( new TranslationCreationLayoutState(
            _windowWidth,
            _windowHeight,
            _dictionaryPaneRatio,
            _isDictionaryDetailsWrapEnabled ) );
    #endregion
}