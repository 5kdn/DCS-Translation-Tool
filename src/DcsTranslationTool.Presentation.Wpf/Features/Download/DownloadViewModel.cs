using System.Collections.ObjectModel;
using System.IO;

using Caliburn.Micro;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Helpers;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.Download;

/// <summary>
/// ダウンロードページの状態を管理する ViewModel
/// </summary>
public class DownloadViewModel(
    IApiService apiService,
    IAppSettingsService appSettingsService,
    IDownloadWorkflowService downloadWorkflowService,
    IApplyWorkflowService applyWorkflowService,
    IDispatcherService dispatcherService,
    IFileEntryService fileEntryService,
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService
) : Screen, IActivate {

    #region Fields

    /// <summary>ローカル側のエントリ一覧</summary>
    private IReadOnlyList<FileEntry> _localEntries = [];

    /// <summary>リポジトリ側のエントリ一覧</summary>
    private IReadOnlyList<FileEntry> _repoEntries = [];

    ///<summary>全てのタブ情報を取得する。</summary>
    private ObservableCollection<TabItemViewModel> _tabs = [];

    ///<summary>現在選択されているタブ。</summary>
    private int _selectedTabIndex;

    /// <summary>ファイルのフィルタ状態を取得するプロパティ。</summary>
    private IFilterViewModel _filter  = new FilterViewModel( logger );

    private bool _isFetching;
    private bool _isDownloading;
    private double _downloadedProgress = 0.0;
    private bool _isApplying;
    private double _appliedProgress = 0.0;
    private bool _suppressEntriesChanged;

    // イベント
    private Func<IReadOnlyList<FileEntry>, Task>? _entriesChangedHandler;
    private EventHandler? _filtersChangedHandler;


    #endregion

    #region Properties

    /// <summary>
    /// ローカル側のエントリ一覧
    /// </summary>
    public IReadOnlyList<FileEntry> LocalEntries {
        get => _localEntries;
        private set => Set( ref _localEntries, value );
    }

    /// <summary>
    /// リポジトリ側のエントリ一覧
    /// </summary>
    public IReadOnlyList<FileEntry> RepoEntries {
        get => _repoEntries;
        private set => Set( ref _repoEntries, value );
    }

    /// <summary>
    /// 全てのタブ
    /// </summary>
    public ObservableCollection<TabItemViewModel> Tabs {
        get => _tabs;
        private set => Set( ref _tabs, value );
    }

    /// <summary>
    /// 選択中のタブインデックス
    /// </summary>
    public int SelectedTabIndex {
        get => _selectedTabIndex;
        set {
            if(!Set( ref _selectedTabIndex, value )) return;
            NotifyOfPropertyChange( nameof( CanDownload ) );
            NotifyOfPropertyChange( nameof( CanApply ) );
        }
    }

    /// <summary>
    /// ファイルのフィルタ状態
    /// </summary>
    public IFilterViewModel Filter {
        get => _filter;
        set => Set( ref _filter, value );
    }

    public bool IsFetching {
        get => _isFetching;
        set {
            if(!Set( ref _isFetching, value )) return;
            NotifyOfPropertyChange( nameof( IsTreeInteractionEnabled ) );
        }
    }

    public bool IsDownloading {
        get => _isDownloading;
        set {
            if(!Set( ref _isDownloading, value )) return;
            NotifyOfPropertyChange( nameof( IsTreeInteractionEnabled ) );
        }
    }

    public double DownloadedProgress {
        get => _downloadedProgress;
        set => Set( ref _downloadedProgress, Math.Clamp( value, 0, 100 ) );
    }

    public bool IsApplying {
        get => _isApplying;
        set {
            if(!Set( ref _isApplying, value )) return;
            NotifyOfPropertyChange( nameof( IsTreeInteractionEnabled ) );
        }
    }

    public double AppliedProgress {
        get => _appliedProgress;
        set => Set( ref _appliedProgress, Math.Clamp( value, 0, 100 ) );
    }

    #endregion

    #region Action Guards

    /// <summary>
    /// ダウンロード可能か
    /// </summary>
    public bool CanDownload => !IsDownloading && HasChecked();

    /// <summary>
    /// 適用可能か
    /// </summary>
    public bool CanApply => !IsApplying && HasChecked();

    /// <summary>
    /// ツリー操作が許可されているか。
    /// </summary>
    public bool IsTreeInteractionEnabled => !IsFetching && !IsDownloading && !IsApplying;

    #endregion

    #region Lifecycle

    /// <summary>
    /// 画面アクティブ時に初期化を行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン</param>
    /// <returns>非同期タスク</returns>
    /// <exception cref="ObjectDisposedException">監視開始に失敗した場合</exception>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( "DownloadViewModel をアクティブ化する。" );
        // 既存購読を解除してから再購読する
        _ = OnDeactivateAsync( close: false, cancellationToken );

        _entriesChangedHandler = entries => {
            if(_suppressEntriesChanged) {
                logger.Info( "ダウンロード中のため EntriesChanged を即時破棄する。" );
                return Task.CompletedTask;
            }
            return dispatcherService.InvokeAsync( () => {
                logger.Info( $"EntriesChanged を受信した。件数={entries.Count}" );
                LocalEntries = entries;
                RefreshTabs();
                return Task.CompletedTask;
            } );
        };
        fileEntryService.EntriesChanged += _entriesChangedHandler!;

        _filtersChangedHandler = ( _, _ ) => ApplyFilter();
        Filter.FiltersChanged += _filtersChangedHandler;

        fileEntryService.Watch( appSettingsService.Settings.TranslateFileDir );
        logger.Info( $"ファイル監視を開始した。Directory={appSettingsService.Settings.TranslateFileDir}" );

        // 起動時取得は待たずに開始する
        await Fetch();

        await base.OnActivatedAsync( cancellationToken );
        logger.Info( "DownloadViewModel のアクティブ化が完了した。" );
    }

    /// <summary>
    /// 画面非アクティブ時に購読解除とクリーンアップを行う。
    /// </summary>
    /// <param name="close">閉じるかどうか</param>
    /// <param name="cancellationToken">キャンセル トークン</param>
    /// <returns>非同期タスク</returns>
    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"DownloadViewModel を非アクティブ化する。Close={close}" );
        if(_entriesChangedHandler is not null) {
            fileEntryService.EntriesChanged -= _entriesChangedHandler;
            _entriesChangedHandler = null;
            logger.Info( "ファイルエントリの購読を解除した。" );
        }

        if(_filtersChangedHandler is not null) {
            Filter.FiltersChanged -= _filtersChangedHandler;
            _filtersChangedHandler = null;
            logger.Info( "フィルタ変更イベントの購読を解除した。" );
        }

        fileEntryService.Dispose();
        snackbarService.Clear();
        logger.Info( "リソースを解放した。" );

        await base.OnDeactivateAsync( close, cancellationToken );
    }

    #endregion

    #region Actions

    /// <summary>
    /// リポジトリからツリーを取得する
    /// </summary>
    /// <returns>非同期タスク</returns>
    /// <exception cref="InvalidOperationException">取得失敗時</exception>
    public async Task Fetch() {
        logger.Info( "ファイル一覧の取得を開始する。" );
        IsFetching = true;
        try {
            var repoResult = await apiService.GetTreeAsync();
            if(repoResult.IsFailed) {
                var reason = repoResult.Errors.Count > 0 ? repoResult.Errors[0].Message : null;
                var message = ResultNotificationPolicy.GetTreeFetchFailureMessage( repoResult.GetFirstErrorKind() );
                logger.Error( $"リポジトリのファイル一覧取得が失敗した。Reason={reason}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( message );
                    return Task.CompletedTask;
                } );
                return;
            }
            RepoEntries = [.. repoResult.Value];
            logger.Info( $"ファイル一覧を取得した。件数={RepoEntries.Count}" );
            RefreshTabs();
            await dispatcherService.InvokeAsync( () => {
                snackbarService.Show( "ファイル一覧の取得が完了しました" );
                return Task.CompletedTask;
            } );
        }
        catch(Exception ex) {
            logger.Error( "ファイル一覧取得処理で例外が発生した。", ex );
            await dispatcherService.InvokeAsync( () => {
                snackbarService.Show( "取得処理で例外が発生しました" );
                return Task.CompletedTask;
            } );
        }
        finally {
            IsFetching = false;
            logger.Info( "ファイル一覧取得処理を終了した。" );
        }
    }

    /// <summary>
    /// チェック状態のファイルをダウンロードする
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Download() {
        logger.Info( "ダウンロード処理を開始する。" );
        if(!CanDownload) {
            logger.Warn( "ダウンロードは現在許可されていないため処理を中断する。" );
            return;
        }

        var saveRootPath = appSettingsService.Settings.TranslateFileDir;
        if(string.IsNullOrWhiteSpace( saveRootPath )) {
            logger.Warn( "保存先ディレクトリが設定されていないため保存を中断する。" );
            await ShowSnackbarAsync( "保存先フォルダーが設定されていません" );
            return;
        }

        _suppressEntriesChanged = true;
        IsDownloading = true;
        var entriesChangedHandler = _entriesChangedHandler;
        if(entriesChangedHandler is not null) {
            fileEntryService.EntriesChanged -= entriesChangedHandler;
        }
        await UpdateDownloadProgressAsync( 0.0 );
        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );

        try {
            if(Tabs.Count == 0 || SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                logger.Warn( "タブが選択されていないためダウンロードを中断する。" );
                await ShowSnackbarAsync( "タブが選択されていません" );
                return;
            }

            var tab = Tabs[SelectedTabIndex];
            var checkedEntries = tab.GetCheckedEntries();
            if(checkedEntries is null) {
                logger.Warn( "チェックされたエントリが存在しない。" );
                await ShowSnackbarAsync( "ダウンロード対象が有りません" );
                return;
            }

            var targetEntries = checkedEntries.Where( e => !e.IsDirectory ).ToList();
            if(targetEntries.Count == 0) {
                logger.Warn( "ダウンロード対象のファイルが存在しない。" );
                await ShowSnackbarAsync( "ダウンロード対象が有りません" );
                return;
            }
            logger.Info( $"ダウンロード対象を特定した。件数={targetEntries.Count}" );

            IReadOnlyList<string> paths = targetEntries.ConvertAll( e => e.Path );
            var pathResult = await apiService.DownloadFilePathsAsync(
                new ApiDownloadFilePathsRequest( paths, null )
            );
            if(pathResult.IsFailed) {
                var reason = pathResult.Errors.Count > 0 ? pathResult.Errors[0].Message : null;
                var message = ResultNotificationPolicy.GetDownloadPathFailureMessage( pathResult.GetFirstErrorKind() );
                logger.Error( $"ダウンロードURLの取得に失敗した。Reason={reason}" );
                await ShowSnackbarAsync( message );
                return;
            }

            var downloadItems = pathResult.Value.Items.ToArray();
            logger.Info( $"ダウンロードURLを取得した。{downloadItems.Length}件" );

            if(downloadItems.Length == 0) {
                logger.Info( "ダウンロード対象が最新のため保存をスキップする。" );
                await ShowSnackbarAsync( "対象ファイルは最新です" );
                return;
            }

            var workflowProgress = CreateWorkflowProgress();
            var workflowResult = await downloadWorkflowService.DownloadFilesAsync( downloadItems, saveRootPath, progress: workflowProgress );
            await ApplyWorkflowEventsAsync( workflowResult.Events, skipProgressEvents: true );
        }
        finally {
            IsDownloading = false;
            _suppressEntriesChanged = false;
            if(entriesChangedHandler is not null) {
                fileEntryService.EntriesChanged += entriesChangedHandler;
            }
            await RefreshLocalEntriesAsync();
            NotifyOfPropertyChange( nameof( CanDownload ) );
            NotifyOfPropertyChange( nameof( CanApply ) );
            logger.Info( "ダウンロード処理を終了した。" );
        }
    }

    /// <summary>
    /// 選択されたファイルを miz/trk に適用する
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Apply() {
        logger.Info( "適用処理を開始する。" );
        if(!CanApply) {
            logger.Warn( "適用は現在許可されていないため処理を中断する。" );
            return;
        }

        _suppressEntriesChanged = true;
        IsApplying = true;
        var entriesChangedHandler = _entriesChangedHandler;
        if(entriesChangedHandler is not null) {
            fileEntryService.EntriesChanged -= entriesChangedHandler;
        }
        await UpdateApplyProgressAsync( 0.0 );
        NotifyOfPropertyChange( nameof( CanApply ) );
        NotifyOfPropertyChange( nameof( CanDownload ) );

        try {
            if(Tabs.Count == 0 || SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                logger.Warn( "タブが選択されていないため適用処理を中断する。" );
                await ShowSnackbarAsync( "タブが選択されていません" );
                return;
            }

            var tab = Tabs[SelectedTabIndex];
            var targetEntries = GetTargetFileNodes().Where( e => !e.IsDirectory ).ToList();

            if(targetEntries.Count == 0) {
                logger.Warn( "適用対象のファイルが存在しない。" );
                await ShowSnackbarAsync( "対象が有りません" );
                return;
            }
            logger.Info( $"適用対象を特定した。件数={targetEntries.Count}" );

            string rootPath = tab.TabType switch
            {
                CategoryType.Aircraft => appSettingsService.Settings.SourceAircraftDir,
                CategoryType.DlcCampaigns => appSettingsService.Settings.SourceDlcCampaignDir,
                CategoryType.UserMissions => appSettingsService.Settings.SourceUserMissionDir,
                _ => throw new InvalidOperationException( $"未対応のタブ種別: {tab.TabType}" ),
            };

            if(string.IsNullOrWhiteSpace( rootPath )) {
                logger.Warn( "適用先ディレクトリが設定されていないため処理を中断する。" );
                await ShowSnackbarAsync( "適用先ディレクトリを設定してください" );
                return;
            }

            var rootFullPath = Path.GetFullPath( rootPath );
            if(!Directory.Exists( rootFullPath )) {
                logger.Warn( $"適用先ディレクトリが存在しない。Directory={rootFullPath}" );
                await ShowSnackbarAsync( "適用先ディレクトリが存在しません" );
                return;
            }
            var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? rootFullPath
                : rootFullPath + Path.DirectorySeparatorChar;

            var translateRoot = appSettingsService.Settings.TranslateFileDir;
            if(string.IsNullOrWhiteSpace( translateRoot )) {
                logger.Warn( "翻訳ディレクトリが未設定のため処理を中断する。" );
                await ShowSnackbarAsync( "翻訳ディレクトリを設定してください" );
                return;
            }

            var translateFullPath = Path.GetFullPath( translateRoot );
            Directory.CreateDirectory( translateFullPath );
            var translateRootWithSeparator = translateFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? translateFullPath
                : translateFullPath + Path.DirectorySeparatorChar;

            var workflowProgress = CreateWorkflowProgress();
            var applyResult = await Task.Run( () => applyWorkflowService.ApplyAsync(
                targetEntries,
                rootFullPath,
                rootWithSeparator,
                translateFullPath,
                translateRootWithSeparator,
                progress: workflowProgress
            ) );
            await ApplyWorkflowEventsAsync( applyResult.Events, skipProgressEvents: true );
            if(!applyResult.IsSuccess) {
                return;
            }
        }
        finally {
            IsApplying = false;
            _suppressEntriesChanged = false;
            if(entriesChangedHandler is not null) {
                fileEntryService.EntriesChanged += entriesChangedHandler;
            }
            await RefreshLocalEntriesAsync();
            NotifyOfPropertyChange( nameof( CanApply ) );
            NotifyOfPropertyChange( nameof( CanDownload ) );
            logger.Info( "適用処理を終了した。" );
        }
    }

    private async Task RefreshLocalEntriesAsync() {
        var entries = await fileEntryService.GetEntriesAsync();
        if(entries is null) {
            logger.Warn( "ダウンロード完了後のエントリ取得結果が null のため処理を中断する。" );
            return;
        }
        if(entries.IsFailed) {
            logger.Warn( "ダウンロード完了後のエントリ再取得に失敗した。" );
            return;
        }

        await dispatcherService.InvokeAsync( () => {
            LocalEntries = entries.Value;
            RefreshTabs();
            return Task.CompletedTask;
        } );
    }

    /// <summary>
    /// 翻訳ファイルの管理ディレクトリをエクスプローラーで開く
    /// </summary>
    public void OpenDirectory() {
        logger.Info( $"翻訳ファイルディレクトリを開く。Directory={appSettingsService.Settings.TranslateFileDir}" );
        systemService.OpenDirectory( appSettingsService.Settings.TranslateFileDir );
    }

    /// <summary>
    /// ワークフローイベントを UI へ反映する。
    /// </summary>
    /// <param name="events">反映対象イベント。</param>
    /// <param name="skipProgressEvents">進捗イベントを除外するか。</param>
    private async Task ApplyWorkflowEventsAsync( IReadOnlyList<WorkflowEvent> events, bool skipProgressEvents = false ) {
        foreach(var workflowEvent in events) {
            if(skipProgressEvents && workflowEvent.Kind is WorkflowEventKind.DownloadProgress or WorkflowEventKind.ApplyProgress) {
                continue;
            }

            switch(workflowEvent.Kind) {
                case WorkflowEventKind.Notification:
                    if(!string.IsNullOrWhiteSpace( workflowEvent.Message )) {
                        await ShowSnackbarAsync( workflowEvent.Message );
                    }
                    break;
                case WorkflowEventKind.DownloadProgress:
                    await UpdateDownloadProgressAsync( workflowEvent.Progress ?? 0 );
                    break;
                case WorkflowEventKind.ApplyProgress:
                    await UpdateApplyProgressAsync( workflowEvent.Progress ?? 0 );
                    break;
                default:
                    break;
            }
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// ワークフロー進捗通知を生成する。
    /// </summary>
    private IProgress<WorkflowEvent> CreateWorkflowProgress() =>
        new Progress<WorkflowEvent>( ApplyWorkflowEvent );

    /// <summary>
    /// 単一のワークフローイベントを反映する。
    /// </summary>
    /// <param name="workflowEvent">反映対象イベント。</param>
    private void ApplyWorkflowEvent( WorkflowEvent workflowEvent ) {
        switch(workflowEvent.Kind) {
            case WorkflowEventKind.Notification:
                if(!string.IsNullOrWhiteSpace( workflowEvent.Message )) {
                    snackbarService.Show( workflowEvent.Message );
                }
                break;
            case WorkflowEventKind.DownloadProgress:
                DownloadedProgress = workflowEvent.Progress ?? 0;
                break;
            case WorkflowEventKind.ApplyProgress:
                AppliedProgress = workflowEvent.Progress ?? 0;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// ダウンロード進捗を更新する。
    /// </summary>
    /// <param name="value">進捗率。</param>
    private Task UpdateDownloadProgressAsync( double value ) =>
        dispatcherService.InvokeAsync( () => {
            DownloadedProgress = value;
            return Task.CompletedTask;
        } );

    /// <summary>
    /// 適用進捗を更新する。
    /// </summary>
    /// <param name="value">進捗率。</param>
    private Task UpdateApplyProgressAsync( double value ) =>
        dispatcherService.InvokeAsync( () => {
            AppliedProgress = value;
            return Task.CompletedTask;
        } );

    /// <summary>
    /// スナックバーにメッセージを表示する。
    /// </summary>
    /// <param name="message">表示メッセージ。</param>
    private Task ShowSnackbarAsync( string message ) =>
        dispatcherService.InvokeAsync( () => {
            snackbarService.Show( message );
            return Task.CompletedTask;
        } );


    /// <summary>
    /// 現在のタブでチェックありかを判定する
    /// </summary>
    /// <returns>チェックが1つ以上なら true</returns>
    private bool HasChecked() =>
        Tabs.Count > 0 &&
        SelectedTabIndex >= 0 &&
        SelectedTabIndex < Tabs.Count &&
        Tabs[SelectedTabIndex].Root.CheckState != false;

    /// <summary>
    /// リポジトリとローカルのエントリをマージしてタブを再構築する
    /// </summary>
    private void RefreshTabs() {
        var tabIndex = SelectedTabIndex;
        logger.Info( $"タブを再構築する。LocalCount={LocalEntries.Count}, RepoCount={RepoEntries.Count}, SelectedIndex={tabIndex}" );

        var entries = FileEntryComparisonHelper.Merge(LocalEntries, RepoEntries);

        IFileEntryViewModel rootVm = new FileEntryViewModel(new FileEntry(string.Empty, string.Empty, true), ChangeTypeMode.Download, logger);
        foreach(var entry in entries) AddFileEntryToFileEntryViewModel( rootVm, entry, logger );

        var tabs = Enum.GetValues<CategoryType>().Select(tabType =>
        {
            IFileEntryViewModel? target = rootVm;
            foreach (var name in tabType.GetRepoDirRoot())
            {
                target = target?.Children.FirstOrDefault(c => c?.Name == name);
                if (target is null) break;
            }
            return new TabItemViewModel(
                tabType,
                logger,
                target ?? new FileEntryViewModel(new FileEntry("null", string.Empty, false), ChangeTypeMode.Download, logger)
            );
        }).ToList();

        foreach(var t in Tabs) t.Root.CheckStateChanged -= OnRootCheckStateChanged;
        Tabs.Clear();
        foreach(var t in tabs) Tabs.Add( t );
        foreach(var t in Tabs) t.Root.CheckStateChanged += OnRootCheckStateChanged;

        SelectedTabIndex = Tabs.Count == 0 ? 0 : Math.Clamp( tabIndex, 0, Tabs.Count - 1 );

        ApplyFilter();
        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );
        logger.Info( $"タブの再構築が完了した。TabCount={Tabs.Count}, SelectedIndex={SelectedTabIndex}" );
    }

    /// <summary>
    /// ルートノードのチェック状態変化時にガードを更新する
    /// </summary>
    /// <param name="sender">送信元</param>
    /// <param name="e">チェック状態</param>
    private void OnRootCheckStateChanged( object? sender, bool? e ) {
        _ = sender;
        _ = e;
        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );
        logger.Info( $"ルートチェック状態が変化した。SelectedIndex={SelectedTabIndex}, NewState={e}" );
    }

    /// <summary>
    /// 適用対象ノード列挙を取得する
    /// </summary>
    /// <returns>対象ノード列挙</returns>
    private IEnumerable<IFileEntryViewModel> GetTargetFileNodes() {
        var tab = Tabs[SelectedTabIndex];
        return tab
            .GetCheckedViewModels()
            .Where( e => e.ChangeType is FileChangeType.Modified or FileChangeType.LocalOnly or FileChangeType.RepoOnly );
    }

    /// <summary>
    /// 現在のフィルタ条件を適用する
    /// </summary>
    private void ApplyFilter() {
        var types = Filter.GetActiveTypes().ToHashSet();
        var activeTypes = string.Join( ",", types.Select( t => t?.ToString() ?? "null" ) );
        logger.Info( $"フィルタを適用する。ActiveTypes={activeTypes}" );
        foreach(var tab in Tabs) {
            ApplyFilterRecursive( tab.Root, types );
        }
        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );
        logger.Info( "フィルタ適用が完了した。" );
    }

    /// <summary>
    /// 指定ノードにフィルタを再帰適用する。
    /// </summary>
    /// <param name="node">対象ノード</param>
    /// <param name="types">可視とする種別集合</param>
    /// <returns>可視かどうか</returns>
    private static bool ApplyFilterRecursive( IFileEntryViewModel node, HashSet<FileChangeType?> types ) {
        var visible = types.Contains(node.ChangeType);
        if(node.IsDirectory) {
            var childVisible = false;
            foreach(var child in node.Children) {
                if(ApplyFilterRecursive( child, types )) childVisible = true;
            }
            visible |= childVisible;
        }
        node.IsVisible = visible;
        return visible;
    }

    /// <summary>
    /// <see cref="FileEntry"/> を <see cref="FileEntryViewModel"/> ツリーに追加する
    /// </summary>
    /// <param name="root">ルート</param>
    /// <param name="entry">エントリ</param>
    private static void AddFileEntryToFileEntryViewModel( IFileEntryViewModel root, FileEntry entry, ILoggingService loggingService ) {
        string[] parts = entry.Path.Split("/", StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length == 0) return;

        IFileEntryViewModel current = root;
        var absolutePath = string.Empty;

        // ディレクトリを順次作成する
        foreach(var part in parts[..^1]) {
            absolutePath += absolutePath.Length == 0 ? part : "/" + part;
            var next = current.Children.FirstOrDefault(c => c?.Name == part && c.IsDirectory);
            if(next is null) {
                next = new FileEntryViewModel( new FileEntry( part, absolutePath, true ), ChangeTypeMode.Download, loggingService );
                current.Children.Add( next );
            }
            current = next;
        }

        var last = parts[^1];
        if(!current.Children.Any( c => c?.Name == last )) {
            current.Children.Add(
                new FileEntryViewModel(
                    new FileEntry( last, entry.Path, entry.IsDirectory, entry.LocalSha, entry.RepoSha ),
                    ChangeTypeMode.Download,
                    loggingService ) );
        }
    }

    #endregion
}