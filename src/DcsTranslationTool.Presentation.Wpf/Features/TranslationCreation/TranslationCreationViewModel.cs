using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;

using FluentResults;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// 翻訳作成ウィンドウの状態を管理する ViewModel。
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
) : Screen, ITranslationCreationViewModel {
    #region Constants

    public const double DefaultWindowWidth = 900;
    public const double DefaultWindowHeight = 760;
    public const double MinWindowWidth = 900;
    public const double MinWindowHeight = 760;
    public const double DefaultDictionaryPaneRatio = 2;
    public const double MinDictionaryPaneRatio = 0.2;
    public const double MaxDictionaryPaneRatio = 8;
    private static readonly TimeSpan SelectedTranslatedCommitDelay = TimeSpan.FromMilliseconds( 250 );
    private const string DictionaryOpenFileFilter = "dictionary|dictionary|すべてのファイル|*.*";
    private const string DictionarySaveFileFilter = "dictionary|dictionary|すべてのファイル|*.*";
    private const string CsvFileFilter = "CSV files|*.csv|すべてのファイル|*.*";
    private const string PoSaveFileFilter = "PO files|*.po|すべてのファイル|*.*";
    #endregion

    #region Fields

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
    private bool _isDictionaryDetailsWrapEnabled = appSettingsService.Settings.TranslationCreationWrapDictionaryDetailsText;
    private bool _hasProcessedJapaneseDictionaryPrompt;
    private Task? _initializeAfterShownTask;
    private bool _hasPendingSelectedTranslatedEdit;
    private bool _hasJapaneseDictionary;
    private IReadOnlyList<TranslationDictionaryItem> _japaneseDictionaryItems = [];
    private IReadOnlyList<TranslationDictionaryItem> _loadedDictionaryItems = [];
    private int _dirtyItemCount;
    private DispatcherTimer? _selectedTranslatedCommitTimer;
    private TranslationCreationPathResolver? _pathResolver;
    private readonly TranslationCreationDictionaryLoader _dictionaryLoader = new( translationDictionaryService );
    private double _windowWidth = NormalizeWindowLength(
        appSettingsService.Settings.TranslationCreationWindowWidth,
        DefaultWindowWidth,
        MinWindowWidth );
    private double _windowHeight = NormalizeWindowLength(
        appSettingsService.Settings.TranslationCreationWindowHeight,
        DefaultWindowHeight,
        MinWindowHeight );
    private double _dictionaryPaneRatio = NormalizeDictionaryPaneRatio(
        appSettingsService.Settings.TranslationCreationDictionaryPaneRatio );
    #endregion

    #region Properties

    /// <summary>
    /// ウィンドウの表示名を取得する。
    /// </summary>
    public string WindowTitle { get; } = Strings_Translation.CreateTranslationWindowTitle;

    /// <summary>
    /// TranslationCreation Window 専用の Snackbar メッセージキューを取得する。
    /// </summary>
    public SnackbarMessageQueue MessageQueue => _messageQueue ??= new();

    /// <summary>
    /// ウィンドウ幅を取得または設定する。
    /// </summary>
    public double WindowWidth {
        get => _windowWidth;
        set {
            var normalizedValue = NormalizeWindowLength( value, DefaultWindowWidth, MinWindowWidth );
            if(!Set( ref _windowWidth, normalizedValue )) {
                return;
            }

            appSettingsService.Settings.TranslationCreationWindowWidth = normalizedValue;
        }
    }

    /// <summary>
    /// ウィンドウ高さを取得または設定する。
    /// </summary>
    public double WindowHeight {
        get => _windowHeight;
        set {
            var normalizedValue = NormalizeWindowLength( value, DefaultWindowHeight, MinWindowHeight );
            if(!Set( ref _windowHeight, normalizedValue )) {
                return;
            }

            appSettingsService.Settings.TranslationCreationWindowHeight = normalizedValue;
        }
    }

    /// <summary>
    /// dictionary 領域比率を取得または設定する。
    /// </summary>
    public double DictionaryPaneRatio {
        get => _dictionaryPaneRatio;
        set {
            var normalizedValue = NormalizeDictionaryPaneRatio( value );
            if(!Set( ref _dictionaryPaneRatio, normalizedValue )) {
                return;
            }

            appSettingsService.Settings.TranslationCreationDictionaryPaneRatio = normalizedValue;
        }
    }

    /// <summary>
    /// 選択中アーカイブの絶対パスを取得する。
    /// </summary>
    public string ArchiveFullPath { get; } = string.IsNullOrWhiteSpace( archiveFullPath )
        ? throw new ArgumentException( "アーカイブ絶対パスは必須です。", nameof( archiveFullPath ) )
        : archiveFullPath;

    private TranslationCreationPathResolver PathResolver => _pathResolver ??= new( appSettingsService.Settings, ArchiveFullPath );

    private DispatcherTimer SelectedTranslatedCommitTimer => _selectedTranslatedCommitTimer ??= CreateSelectedTranslatedCommitTimer();

    /// <summary>
    /// dictionary 項目一覧を取得または設定する。
    /// </summary>
    public ObservableCollection<TranslationDictionaryItemRowViewModel> DictionaryItems {
        get => _dictionaryItems;
        private set {
            var previousItems = _dictionaryItems;
            if(!Set( ref _dictionaryItems, value )) {
                return;
            }

            UnsubscribeDictionaryItems( previousItems );
            SubscribeDictionaryItems( value );
            FilteredDictionaryItemsView = CollectionViewSource.GetDefaultView( value );
            FilteredDictionaryItemsView.Filter = FilterDictionaryItem;
            FilteredDictionaryItemsView.Refresh();
            NotifyOfPropertyChange( nameof( FilteredDictionaryItemsView ) );
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
    /// dictionary 詳細テキストを右端で折り返すかどうかを取得または設定する。
    /// </summary>
    public bool IsDictionaryDetailsWrapEnabled {
        get => _isDictionaryDetailsWrapEnabled;
        set {
            if(!Set( ref _isDictionaryDetailsWrapEnabled, value )) {
                return;
            }

            appSettingsService.Settings.TranslationCreationWrapDictionaryDetailsText = value;
            NotifyOfPropertyChange( nameof( DictionaryDetailsTextWrapping ) );
        }
    }

    /// <summary>
    /// dictionary 詳細テキストの折り返し方法を取得する。
    /// </summary>
    public TextWrapping DictionaryDetailsTextWrapping => IsDictionaryDetailsWrapEnabled
        ? TextWrapping.Wrap
        : TextWrapping.NoWrap;

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

    #endregion

    #region Lifecycle

    /// <summary>
    /// アクティブ化完了時に表示準備のみを行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    protected override async Task OnActivatedAsync( CancellationToken cancellationToken ) {
        await base.OnActivatedAsync( cancellationToken );
    }

    /// <summary>
    /// ウィンドウ表示後の初期化を一度だけ実行する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    internal Task InitializeAfterShownAsync( CancellationToken cancellationToken = default ) =>
        _initializeAfterShownTask ??= LoadDictionaryAsync( cancellationToken );

    /// <summary>
    /// ウィンドウ表示後に dictionary 読込と埋め込みJP dictionary の確認を行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    public async Task HandleWindowLoadedAsync( CancellationToken cancellationToken = default ) {
        await InitializeAfterShownAsync( cancellationToken );
        if(_hasProcessedJapaneseDictionaryPrompt) {
            return;
        }

        _hasProcessedJapaneseDictionaryPrompt = true;
        cancellationToken.ThrowIfCancellationRequested();

        if(!_hasJapaneseDictionary || _loadedDictionaryItems.Count == 0) {
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

        if(_japaneseDictionaryItems.Count == 0) {
            logger.Warn( $"JP dictionary の読込に失敗したため DEFAULT dictionary のみで継続する。Archive={ArchiveFullPath}" );
            return;
        }

        var japaneseSourceItems = TranslationCreationDictionaryLoader.CreateJapaneseImportSourceItems( _japaneseDictionaryItems );
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

    /// <summary>
    /// アーカイブから dictionary を読み込んで画面状態へ反映する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    private async Task LoadDictionaryAsync( CancellationToken cancellationToken ) {
        cancellationToken.ThrowIfCancellationRequested();

        logger.Info( $"TranslationCreationViewModel の dictionary 読込を開始する。Archive={ArchiveFullPath}" );
        IsLoading = true;
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadingMessage;

        try {
            var result = await Task.Run(
                () => _dictionaryLoader.LoadArchiveDictionaryState( ArchiveFullPath ),
                cancellationToken ).ConfigureAwait( false );

            if(result.IsFailed) {
                await Execute.OnUIThreadAsync( () => {
                    SetDictionaryLoadFailedState();
                    return Task.CompletedTask;
                } ).ConfigureAwait( false );
                return;
            }

            await Execute.OnUIThreadAsync( () => {
                _hasJapaneseDictionary = result.Value.HasJapaneseDictionary;
                _japaneseDictionaryItems = result.Value.JapaneseDictionaryItems;
                ApplyDictionaryLoadState( result.Value.LoadState );
                SelectedDictionaryItem = null;
                StatusMessage = DictionaryItems.Count == 0
                    ? Strings_Translation.CreateTranslationDictionaryEmptyMessage
                    : string.Empty;
                NotifyDictionaryAvailabilityChanged();
                return Task.CompletedTask;
            } ).ConfigureAwait( false );
            logger.Info( $"TranslationCreationViewModel の dictionary 読込詳細。Archive={ArchiveFullPath}" );
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
            logger.Info( $"TranslationCreationViewModel の dictionary 読込を終了する。Archive={ArchiveFullPath}, Count={DictionaryItems.Count}" );
        }
    }
    #endregion

    #region Action Guards

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
    /// 閉じる確認が必要な未反映変更が存在するかどうかを判定する。
    /// </summary>
    /// <returns>未反映変更が存在する場合は <see langword="true"/> を返す。</returns>
    internal bool HasPendingChangesForClose() =>
        HasPendingChangesForClose( SelectedDictionaryItem, _selectedTranslated );

    /// <summary>
    /// 読み込み基準と比較して dirty 行が存在するかどうかを判定する。
    /// </summary>
    /// <returns>dirty 行が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasDirtyRows() {
        if(_loadedDictionaryItems.Count != DictionaryItems.Count) {
            return true;
        }

        return _dirtyItemCount > 0;
    }

    /// <summary>
    /// 選択中詳細編集の保留値を加味して未反映変更が存在するかどうかを判定する。
    /// </summary>
    /// <param name="selectedRow">選択中行。</param>
    /// <param name="selectedTranslated">保留中の翻訳文。</param>
    /// <returns>未反映変更が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasPendingChangesForClose( TranslationDictionaryItemRowViewModel? selectedRow, string selectedTranslated ) {
        if(selectedRow is null || !_hasPendingSelectedTranslatedEdit || !selectedRow.IsEnabled) {
            return HasDirtyRows();
        }

        if(selectedRow.HasPendingChangesWithTranslatedOverride( selectedTranslated )) {
            return true;
        }

        return HasDirtyRows() && !selectedRow.HasPendingChanges;
    }

    /// <summary>
    /// 1 件以上の翻訳文が入力済みかどうかを判定する。
    /// </summary>
    /// <returns>翻訳文が存在する場合は <see langword="true"/> を返す。</returns>
    private bool HasTranslatedText() => DictionaryItems.Any( item => !string.IsNullOrWhiteSpace( item.Translated ) );

    /// <summary>
    /// 有効状態変更時にフィルタ再適用が必要かどうかを判定する。
    /// </summary>
    /// <returns>再適用が必要な場合は <see langword="true"/> を返す。</returns>
    private bool ShouldRefreshFilterForIsEnabledChange() =>
        !ShowEnabledItems || !ShowDisabledItems;

    #endregion

    #region Actions

    /// <summary>
    /// dictionary 詳細テキストの折り返し状態を設定する。
    /// </summary>
    /// <param name="isEnabled">右端で折り返すかどうか。</param>
    public void SetDictionaryDetailsWrapEnabled( bool isEnabled ) =>
        IsDictionaryDetailsWrapEnabled = isEnabled;

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
    /// ウィンドウを閉じてもよいかどうかを確認する。
    /// </summary>
    /// <returns>ウィンドウを閉じてよい場合は <see langword="true"/> を返す。</returns>
    public async Task<bool> ConfirmCloseAsync() {
        if(!HasPendingChangesForClose()) {
            return true;
        }

        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = Strings_Translation.CreateTranslationCloseConfirmationTitle,
                Message = Strings_Translation.CreateTranslationCloseConfirmationMessage,
                ConfirmButtonText = Strings_Translation.CreateTranslationCloseConfirmationConfirmButtonText,
                CancelButtonText = Strings_Translation.CreateTranslationCloseConfirmationCancelButtonText,
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
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
    public Task ImportPoAsync() {
        if(!CanImportPo) {
            logger.Warn( "PO を読み込めない状態のため処理を中断する。" );
            return Task.CompletedTask;
        }

        return ImportFileAsync(
            initialPath: GetPoImportInitialPath(),
            openFileFilter: PoSaveFileFilter,
            importingMessage: Strings_Translation.CreateTranslationPoImportingMessage,
            failedMessage: Strings_Translation.CreateTranslationPoImportFailedMessage,
            logTargetName: "PO",
            confirmOverwriteAsync: ConfirmPoOverwriteAsync,
            loadEntries: translationDictionaryService.LoadPo,
            analyzeImport: AnalyzePoImport,
            confirmPartialImportAsync: ConfirmPoPartialImportAsync,
            applyMatch: static match => {
                match.Row.Translated = match.Translated;
                match.Row.IsEnabled = match.IsEnabled;
            },
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationPoImportSucceededMessage, path ),
            partialSuccessMessageFactory: ( matchedCount, path ) => string.Format( Strings_Translation.CreateTranslationPoImportPartialSucceededMessage, matchedCount, path ) );
    }

    /// <summary>
    /// dictionary ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public Task ImportDictionaryAsync() {
        if(!CanImportDictionary) {
            logger.Warn( "dictionary を読み込めない状態のため処理を中断する。" );
            return Task.CompletedTask;
        }

        return ImportFileAsync(
            initialPath: GetDictionaryImportInitialPath(),
            openFileFilter: DictionaryOpenFileFilter,
            importingMessage: Strings_Translation.CreateTranslationDictionaryImportingMessage,
            failedMessage: Strings_Translation.CreateTranslationDictionaryImportFailedMessage,
            logTargetName: "dictionary",
            confirmOverwriteAsync: ConfirmDictionaryOverwriteAsync,
            loadEntries: translationDictionaryService.LoadDictionaryFile,
            analyzeImport: AnalyzeDictionaryImport,
            confirmPartialImportAsync: ConfirmDictionaryPartialImportAsync,
            applyMatch: static match => match.Row.Translated = match.Translated,
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationDictionaryImportSucceededMessage, path ),
            partialSuccessMessageFactory: ( matchedCount, path ) => string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialSucceededMessage, matchedCount, path ) );
    }

    /// <summary>
    /// CSV ファイルを読み込んで Translated へ反映する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public Task ImportCsvAsync() {
        if(!CanImportCsv) {
            logger.Warn( "CSV を読み込めない状態のため処理を中断する。" );
            return Task.CompletedTask;
        }

        return ImportFileAsync(
            initialPath: GetCsvImportInitialPath(),
            openFileFilter: CsvFileFilter,
            importingMessage: Strings_Translation.CreateTranslationCsvImportingMessage,
            failedMessage: Strings_Translation.CreateTranslationCsvImportFailedMessage,
            logTargetName: "CSV",
            confirmOverwriteAsync: ConfirmCsvOverwriteAsync,
            loadEntries: translationDictionaryService.LoadCsv,
            analyzeImport: AnalyzeCsvImport,
            confirmPartialImportAsync: ConfirmCsvPartialImportAsync,
            applyMatch: static match => {
                match.Row.Translated = match.Translated;
                match.Row.IsEnabled = match.IsEnabled;
            },
            successMessageFactory: path => string.Format( Strings_Translation.CreateTranslationCsvImportSucceededMessage, path ),
            partialSuccessMessageFactory: ( matchedCount, path ) => string.Format( Strings_Translation.CreateTranslationCsvImportPartialSucceededMessage, matchedCount, path ) );
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// 現在のフィルタ条件に基づいて行の表示可否を判定する。
    /// </summary>
    /// <param name="item">判定対象の行。</param>
    /// <returns>表示対象の場合は <see langword="true"/> を返す。</returns>
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

        if(HidePossibleNonTranslationTargets && row.IsPossibleNonTranslationTarget) {
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

    /// <summary>
    /// 読み込み済み dictionary 状態を ViewModel へ適用する。
    /// </summary>
    /// <param name="state">適用対象の状態。</param>
    private void ApplyDictionaryLoadState( TranslationCreationDictionaryLoadState state ) {
        _loadedDictionaryItems = state.LoadedItems;
        DictionaryItems = [.. state.RowItems];
        ResetDirtyState();
    }

    /// <summary>
    /// dictionary 一覧ビューへフィルタを再適用する。
    /// </summary>
    private void RefreshFilter() => FilteredDictionaryItemsView.Refresh();

    /// <summary>
    /// dictionary 行コレクションの変更監視を開始する。
    /// </summary>
    /// <param name="dictionaryItems">監視対象の行コレクション。</param>
    private void SubscribeDictionaryItems( ObservableCollection<TranslationDictionaryItemRowViewModel> dictionaryItems ) {
        dictionaryItems.CollectionChanged += OnDictionaryItemsCollectionChanged;
        foreach(var item in dictionaryItems) {
            item.PropertyChanged += OnDictionaryItemPropertyChanged;
        }
    }

    /// <summary>
    /// dictionary 行コレクションの変更監視を解除する。
    /// </summary>
    /// <param name="dictionaryItems">解除対象の行コレクション。</param>
    private void UnsubscribeDictionaryItems( ObservableCollection<TranslationDictionaryItemRowViewModel> dictionaryItems ) {
        dictionaryItems.CollectionChanged -= OnDictionaryItemsCollectionChanged;
        foreach(var item in dictionaryItems) {
            item.PropertyChanged -= OnDictionaryItemPropertyChanged;
        }
    }

    /// <summary>
    /// dictionary 行コレクション変更時に行監視とフィルタ再適用を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
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

    /// <summary>
    /// dictionary 行の状態変更に応じて dirty 状態とフィルタを更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnDictionaryItemPropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(sender is not TranslationDictionaryItemRowViewModel row) {
            return;
        }

        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.Translated )) {
            UpdateDirtyState( row );
            if(ReferenceEquals( row, SelectedDictionaryItem ) && !_hasPendingSelectedTranslatedEdit) {
                SyncSelectedTranslatedFromSelection();
            }

            if(ShowOnlyUntranslated) {
                RefreshFilter();
            }
        }

        if(e.PropertyName == nameof( TranslationDictionaryItemRowViewModel.IsEnabled )) {
            UpdateDirtyState( row );
            if(ReferenceEquals( row, SelectedDictionaryItem )) {
                if(SelectedDictionaryItem?.IsEnabled != true) {
                    CancelSelectedTranslatedCommit();
                    SyncSelectedTranslatedFromSelection();
                }

                NotifyOfPropertyChange( nameof( CanEditSelectedTranslated ) );
            }

            if(ShouldRefreshFilterForIsEnabledChange()) {
                RefreshFilter();
            }
        }
    }

    /// <summary>
    /// 選択中翻訳文の保留中編集を現在の行へ反映する。
    /// </summary>
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

    /// <summary>
    /// 選択中翻訳文の遅延反映用タイマーを生成する。
    /// </summary>
    /// <returns>生成したタイマーを返す。</returns>
    private DispatcherTimer CreateSelectedTranslatedCommitTimer() {
        var timer = new DispatcherTimer( DispatcherPriority.Background )
        {
            Interval = SelectedTranslatedCommitDelay
        };
        timer.Tick += OnSelectedTranslatedCommitTimerTick;
        return timer;
    }

    /// <summary>
    /// 遅延反映タイマー満了時に保留中の編集を確定する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnSelectedTranslatedCommitTimerTick( object? sender, EventArgs e ) {
        FlushPendingSelectedTranslatedEdit();
    }

    /// <summary>
    /// 選択中翻訳文の遅延反映を予約する。
    /// </summary>
    private void ScheduleSelectedTranslatedCommit() {
        _hasPendingSelectedTranslatedEdit = true;
        SelectedTranslatedCommitTimer.Stop();
        SelectedTranslatedCommitTimer.Start();
    }

    /// <summary>
    /// 選択中翻訳文の遅延反映を取り消す。
    /// </summary>
    private void CancelSelectedTranslatedCommit() {
        _hasPendingSelectedTranslatedEdit = false;
        _selectedTranslatedCommitTimer?.Stop();
    }

    /// <summary>
    /// 選択中行の翻訳文を詳細編集欄へ同期する。
    /// </summary>
    private void SyncSelectedTranslatedFromSelection() {
        var nextValue = SelectedDictionaryItem?.Translated ?? string.Empty;
        if(string.Equals( _selectedTranslated, nextValue, StringComparison.Ordinal )) {
            return;
        }

        _selectedTranslated = nextValue;
        NotifyOfPropertyChange( nameof( SelectedTranslated ) );
    }

    /// <summary>
    /// PO 項目一覧と画面上の dictionary 行との一致結果を解析する。
    /// </summary>
    /// <param name="entries">解析対象の PO 項目一覧。</param>
    /// <returns>一致解析結果を返す。</returns>
    private TranslationCreationImportAnalysis<PoImportMatch> AnalyzePoImport( IReadOnlyList<TranslationPoEntry> entries ) =>
        TranslationCreationImportMatcher.MatchByTranslationPair(
            DictionaryItems,
            entries,
            static row => (row.Key, row.Original),
            static entry => (entry.Context, entry.Original),
            static ( row, entry ) => new PoImportMatch( row, entry.Translated, entry.IsEnabled ) );

    /// <summary>
    /// CSV 項目一覧と画面上の dictionary 行との一致結果を解析する。
    /// </summary>
    /// <param name="entries">解析対象の CSV 項目一覧。</param>
    /// <returns>一致解析結果を返す。</returns>
    private TranslationCreationImportAnalysis<CsvImportMatch> AnalyzeCsvImport( IReadOnlyList<TranslationCsvEntry> entries ) =>
        TranslationCreationImportMatcher.MatchByTranslationPair(
            DictionaryItems,
            entries,
            static row => (row.Key, row.Original),
            static entry => (entry.Key, entry.Original),
            static ( row, entry ) => new CsvImportMatch( row, entry.Translated, entry.IsEnabled ) );

    /// <summary>
    /// dictionary 項目一覧と画面上の dictionary 行との一致結果を解析する。
    /// </summary>
    /// <param name="items">解析対象の dictionary 項目一覧。</param>
    /// <returns>一致解析結果を返す。</returns>
    private TranslationCreationImportAnalysis<DictionaryImportMatch> AnalyzeDictionaryImport( IReadOnlyList<TranslationDictionaryItem> items ) =>
        TranslationCreationImportMatcher.MatchByNormalizedKey(
            DictionaryItems,
            items,
            static row => row.Key,
            static item => item.Key,
            static ( row, item ) => new DictionaryImportMatch( row, item.Translated ) );

    /// <summary>
    /// 表示中行の選択位置を指定オフセットだけ移動する。
    /// </summary>
    /// <param name="offset">移動量。</param>
    /// <returns>選択項目が変化した場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// 選択中の読み込み形式を更新する。
    /// </summary>
    /// <param name="format">設定する形式。</param>
    private void SetSelectedImportFormat( TranslationImportFormat format ) {
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
    private void SetSelectedExportFormat( TranslationExportFormat format ) {
        if(_selectedExportFormat == format) {
            return;
        }

        _selectedExportFormat = format;
        NotifyOfPropertyChange( nameof( ExportSplitButtonContent ) );
    }

    /// <summary>
    /// 指定形式の取り込み処理共通フローを実行する。
    /// </summary>
    /// <typeparam name="TEntry">取り込み元項目型。</typeparam>
    /// <typeparam name="TMatch">一致結果型。</typeparam>
    /// <param name="initialPath">ファイル選択初期パス。</param>
    /// <param name="openFileFilter">ファイル選択フィルタ。</param>
    /// <param name="importingMessage">取り込み中メッセージ。</param>
    /// <param name="failedMessage">失敗時メッセージ。</param>
    /// <param name="logTargetName">ログ出力用対象名。</param>
    /// <param name="confirmOverwriteAsync">上書き確認処理。</param>
    /// <param name="loadEntries">項目一覧読込処理。</param>
    /// <param name="analyzeImport">一致解析処理。</param>
    /// <param name="confirmPartialImportAsync">部分一致確認処理。</param>
    /// <param name="applyMatch">一致結果適用処理。</param>
    /// <param name="successMessageFactory">全件一致時メッセージ生成処理。</param>
    /// <param name="partialSuccessMessageFactory">部分一致時メッセージ生成処理。</param>
    /// <returns>非同期タスク。</returns>
    private async Task ImportFileAsync<TEntry, TMatch>(
        string initialPath,
        string openFileFilter,
        string importingMessage,
        string failedMessage,
        string logTargetName,
        Func<Task<bool>> confirmOverwriteAsync,
        Func<string, Result<IReadOnlyList<TEntry>>> loadEntries,
        Func<IReadOnlyList<TEntry>, TranslationCreationImportAnalysis<TMatch>> analyzeImport,
        Func<int, Task<bool>> confirmPartialImportAsync,
        Action<TMatch> applyMatch,
        Func<string, string> successMessageFactory,
        Func<int, string, string> partialSuccessMessageFactory ) {
        FlushPendingSelectedTranslatedEdit();

        if(!dialogProvider.ShowOpenFilePicker( initialPath, openFileFilter, out var selectedPath )) {
            logger.Info( $"{logTargetName} 読み込みファイル選択がキャンセルされた。Archive={ArchiveFullPath}, InitialPath={initialPath}" );
            return;
        }

        if(HasTranslatedText() && !await confirmOverwriteAsync()) {
            logger.Info( $"{logTargetName} 読み込みの上書き確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}" );
            return;
        }

        Result<IReadOnlyList<TEntry>> loadResult;
        try {
            IsLoading = true;
            StatusMessage = importingMessage;
            loadResult = loadEntries( selectedPath );
        }
        catch(Exception ex) {
            logger.Error( $"{logTargetName} 読み込み中に例外が発生した。Archive={ArchiveFullPath}, Path={selectedPath}", ex );
            StatusMessage = failedMessage;
            return;
        }
        finally {
            IsLoading = false;
        }

        if(loadResult.IsFailed) {
            StatusMessage = failedMessage;
            return;
        }

        var importAnalysis = analyzeImport( loadResult.Value );
        if(!importAnalysis.IsFullMatch && !await confirmPartialImportAsync( importAnalysis.Matches.Count )) {
            logger.Info( $"{logTargetName} 読み込みの部分取り込み確認がキャンセルされた。Archive={ArchiveFullPath}, Path={selectedPath}, MatchCount={importAnalysis.Matches.Count}" );
            return;
        }

        foreach(var match in importAnalysis.Matches) {
            applyMatch( match );
        }

        StatusMessage = importAnalysis.IsFullMatch
            ? successMessageFactory( selectedPath )
            : partialSuccessMessageFactory( importAnalysis.Matches.Count, selectedPath );
        ShowCompletedSnackbar( StatusMessage );
        logger.Info( $"{logTargetName} 読み込みが完了した。Archive={ArchiveFullPath}, Path={selectedPath}, FullMatch={importAnalysis.IsFullMatch}, AppliedCount={importAnalysis.Matches.Count}" );
    }

    /// <summary>
    /// 指定形式の書き出し処理共通フローを実行する。
    /// </summary>
    /// <param name="exportPathFactory">既定出力先生成処理。</param>
    /// <param name="exportingMessage">書き出し中メッセージ。</param>
    /// <param name="failedMessage">失敗時メッセージ。</param>
    /// <param name="succeededMessageFactory">成功時メッセージ生成処理。</param>
    /// <param name="saveFileFilter">保存ダイアログフィルタ。</param>
    /// <param name="saveAsync">保存処理。</param>
    /// <param name="logTargetName">ログ出力用対象名。</param>
    /// <returns>非同期タスク。</returns>
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

    /// <summary>
    /// 書き出し先パスを確認し、必要に応じて上書きまたは別名保存を選択させる。
    /// </summary>
    /// <param name="exportPath">既定の書き出し先パス。</param>
    /// <param name="saveFileFilter">保存ダイアログフィルタ。</param>
    /// <param name="logTargetName">ログ出力用対象名。</param>
    /// <returns>確定した保存先パス。キャンセル時は <see langword="null"/> を返す。</returns>
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

    /// <summary>
    /// PO 取り込み時の上書き確認を行う。
    /// </summary>
    /// <returns>上書きする場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// dictionary 取り込み時の上書き確認を行う。
    /// </summary>
    /// <returns>上書きする場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// CSV 取り込み時の上書き確認を行う。
    /// </summary>
    /// <returns>上書きする場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// dictionary の部分一致取り込み確認を行う。
    /// </summary>
    /// <param name="matchedCount">一致件数。</param>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// アーカイブ内の JP dictionary 存在警告を表示する。
    /// </summary>
    /// <returns>継続する場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// 埋め込み JP dictionary の初期取り込み確認を行う。
    /// </summary>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// PO の部分一致取り込み確認を行う。
    /// </summary>
    /// <param name="matchedCount">一致件数。</param>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// CSV の部分一致取り込み確認を行う。
    /// </summary>
    /// <param name="matchedCount">一致件数。</param>
    /// <returns>取り込む場合は <see langword="true"/> を返す。</returns>
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

    /// <summary>
    /// 現在の画面状態から書き出し用 dictionary 項目一覧を生成する。
    /// </summary>
    /// <returns>生成した項目一覧を返す。</returns>
    private IReadOnlyList<TranslationDictionaryItem> CreateCurrentDictionaryItems() =>
        [.. DictionaryItems.Select( item => new TranslationDictionaryItem( item.Key, item.Original ) {
            Translated = item.Translated,
            IsEnabled = item.IsEnabled
        } )];

    /// <summary>
    /// PO 出力用の Project-Id-Version 値を生成する。
    /// </summary>
    /// <returns>生成した値を返す。</returns>
    private string GetProjectIdVersion() => $"DCS Translation Japanese {applicationInfoService.GetVersion()}";

    /// <summary>
    /// PO ヘッダー用タイムスタンプ文字列へ変換する。
    /// </summary>
    /// <param name="value">変換対象の日時。</param>
    /// <returns>変換後文字列を返す。</returns>
    private static string FormatPoTimestamp( DateTimeOffset value ) => value.ToString( "yyyy-MM-dd HH:mmzzz", CultureInfo.InvariantCulture );

    /// <summary>
    /// PO 出力用の X-Generator 値を生成する。
    /// </summary>
    /// <returns>生成した値を返す。</returns>
    private string GetXGenerator() => $"{Strings_Shared.AppDisplayName} {applicationInfoService.GetVersion()}";

    /// <summary>
    /// dictionary の既定書き出し先パスを取得する。
    /// </summary>
    /// <returns>書き出し先パスを返す。</returns>
    private string GetDictionaryExportPath() => PathResolver.GetDictionaryExportPath();

    /// <summary>
    /// PO の既定書き出し先パスを取得する。
    /// </summary>
    /// <returns>書き出し先パスを返す。</returns>
    private string GetPoExportPath() => PathResolver.GetPoExportPath();

    /// <summary>
    /// CSV の既定書き出し先パスを取得する。
    /// </summary>
    /// <returns>書き出し先パスを返す。</returns>
    private string GetCsvExportPath() => PathResolver.GetCsvExportPath();

    /// <summary>
    /// PO 取り込みダイアログの初期パスを取得する。
    /// </summary>
    /// <returns>初期パスを返す。</returns>
    private string GetPoImportInitialPath() => PathResolver.GetPoImportInitialPath();

    /// <summary>
    /// dictionary 取り込みダイアログの初期パスを取得する。
    /// </summary>
    /// <returns>初期パスを返す。</returns>
    private string GetDictionaryImportInitialPath() => PathResolver.GetDictionaryImportInitialPath();

    /// <summary>
    /// CSV 取り込みダイアログの初期パスを取得する。
    /// </summary>
    /// <returns>初期パスを返す。</returns>
    private string GetCsvImportInitialPath() => PathResolver.GetCsvImportInitialPath();

    /// <summary>
    /// dictionary 領域比率を有効範囲へ正規化する。
    /// </summary>
    /// <param name="ratio">検証対象の比率。</param>
    /// <returns>有効範囲内へ補正した比率。</returns>
    public static double NormalizeDictionaryPaneRatio( double ratio ) {
        if(double.IsNaN( ratio ) || double.IsInfinity( ratio ) || ratio <= 0) {
            return DefaultDictionaryPaneRatio;
        }

        return Math.Clamp( ratio, MinDictionaryPaneRatio, MaxDictionaryPaneRatio );
    }

    /// <summary>
    /// ウィンドウサイズを有効範囲へ正規化する。
    /// </summary>
    /// <param name="value">検証対象のサイズ。</param>
    /// <param name="fallback">不正値時の既定サイズ。</param>
    /// <param name="minimum">許容する最小サイズ。</param>
    /// <returns>有効範囲内へ補正したサイズ。</returns>
    public static double NormalizeWindowLength( double value, double fallback, double minimum ) {
        if(double.IsNaN( value ) || double.IsInfinity( value ) || value <= 0) {
            return fallback;
        }

        return Math.Max( minimum, value );
    }

    /// <summary>
    /// 現在の dictionary 行状態を dirty 判定基準として再設定する。
    /// </summary>
    private void ResetDirtyState() {
        _dirtyItemCount = 0;
        foreach(var row in DictionaryItems) {
            row.ResetPendingChangesBaseline();
        }
    }

    /// <summary>
    /// 指定行の dirty 状態変化を集計へ反映する。
    /// </summary>
    /// <param name="row">更新対象の行。</param>
    private void UpdateDirtyState( TranslationDictionaryItemRowViewModel row ) {
        var wasDirty = row.HasPendingChanges;
        if(!row.UpdatePendingChanges()) {
            return;
        }

        if(row.HasPendingChanges) {
            if(!wasDirty) {
                _dirtyItemCount++;
            }
            return;
        }

        _dirtyItemCount = Math.Max( 0, _dirtyItemCount - 1 );
    }

    /// <summary>
    /// 埋め込み JP dictionary 警告メッセージを生成する。
    /// </summary>
    /// <returns>生成したメッセージを返す。</returns>
    private string GetJapaneseDictionaryEmbeddedMessage() =>
        string.Format(
            GetArchiveTypeSpecificJapaneseDictionaryEmbeddedMessage(),
            ArchiveFullPath );

    /// <summary>
    /// アーカイブ種別に応じた埋め込み JP dictionary 警告メッセージを取得する。
    /// </summary>
    /// <returns>メッセージテンプレートを返す。</returns>
    private string GetArchiveTypeSpecificJapaneseDictionaryEmbeddedMessage() =>
        Path.GetExtension( ArchiveFullPath ).Equals( ".trk", StringComparison.OrdinalIgnoreCase )
            ? Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryTrkConfirmationMessage
            : Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryMizConfirmationMessage;

    /// <summary>
    /// dictionary 読込失敗時の画面状態へ切り替える。
    /// </summary>
    private void SetDictionaryLoadFailedState() {
        _hasJapaneseDictionary = false;
        _japaneseDictionaryItems = [];
        _loadedDictionaryItems = [];
        StatusMessage = Strings_Translation.CreateTranslationDictionaryLoadFailedMessage;
        DictionaryItems = [];
        SelectedDictionaryItem = null;
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
    /// 書き出し成功時の Snackbar を表示する。
    /// </summary>
    /// <param name="exportPath">書き出し先パス。</param>
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

    /// <summary>
    /// 操作完了メッセージを Snackbar へ表示する。
    /// </summary>
    /// <param name="message">表示メッセージ。</param>
    private void ShowCompletedSnackbar( string message ) =>
        MessageQueue.Enqueue( message, (string?)null, null, null, false, false, null );

    /// <summary>
    /// Snackbar から保存先ディレクトリを開く。
    /// </summary>
    /// <param name="exportDirectoryPathObject">保存先ディレクトリパス。</param>
    private void OpenExportDirectory( object? exportDirectoryPathObject ) {
        if(exportDirectoryPathObject is not string exportDirectoryPath || string.IsNullOrWhiteSpace( exportDirectoryPath )) {
            logger.Warn( "Snackbar から渡された保存先ディレクトリが不正なため開く処理を中断する。" );
            return;
        }

        logger.Info( $"dictionary 保存先ディレクトリを開く。Directory={exportDirectoryPath}" );
        systemService.OpenDirectory( exportDirectoryPath );
    }

    #endregion

    #region Nested Types

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

    /// <summary>
    /// 読み込み形式の選択肢を表す。
    /// </summary>
    private enum TranslationImportFormat {
        /// <summary>dictionary 形式を表す。</summary>
        Dictionary,
        /// <summary>PO 形式を表す。</summary>
        Po,
        /// <summary>CSV 形式を表す。</summary>
        Csv,
    }

    /// <summary>
    /// 書き出し形式の選択肢を表す。
    /// </summary>
    private enum TranslationExportFormat {
        /// <summary>dictionary 形式を表す。</summary>
        Dictionary,
        /// <summary>PO 形式を表す。</summary>
        Po,
        /// <summary>CSV 形式を表す。</summary>
        Csv,
    }

    #endregion
}