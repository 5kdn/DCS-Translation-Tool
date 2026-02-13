using System.Collections.ObjectModel;
using System.Diagnostics;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.Common;

/// <summary>
/// Upload/Download で共有するツリー表示の共通状態と同期処理を提供する。
/// </summary>
public abstract class FileEntryTabsViewModelBase(
    IApiService apiService,
    IDispatcherService dispatcherService,
    IFileEntryService fileEntryService,
    IFileEntryWatcherLifecycle fileEntryWatcherLifecycle,
    IFileEntryTreeService fileEntryTreeService,
    ILoggingService logger,
    ISnackbarService snackbarService
) : Screen, IActivate {
    private IReadOnlyList<FileEntry> _localEntries = [];
    private IReadOnlyList<FileEntry> _repoEntries = [];
    private ObservableCollection<TabItemViewModel> _tabs = [];
    private int _selectedTabIndex;
    private IFilterViewModel _filter = new FilterViewModel( logger );
    private bool _isFetching;
    private bool _isLocalRefreshRunning;
    private int _localEntriesSignature;
    private Func<IReadOnlyList<FileEntry>, Task>? _entriesChangedHandler;
    private EventHandler? _filtersChangedHandler;
    private readonly object _refreshTabsGate = new();
    private CancellationTokenSource? _refreshTabsCts;
    private int _refreshTabsVersion;

    /// <summary>
    /// ローカル側のエントリ一覧を取得または設定する。
    /// </summary>
    public IReadOnlyList<FileEntry> LocalEntries {
        get => _localEntries;
        protected set => Set( ref _localEntries, value );
    }

    /// <summary>
    /// リポジトリ側のエントリ一覧を取得または設定する。
    /// </summary>
    public IReadOnlyList<FileEntry> RepoEntries {
        get => _repoEntries;
        protected set => Set( ref _repoEntries, value );
    }

    /// <summary>
    /// タブ一覧を取得または設定する。
    /// </summary>
    public ObservableCollection<TabItemViewModel> Tabs {
        get => _tabs;
        protected set => Set( ref _tabs, value );
    }

    /// <summary>
    /// 選択中タブのインデックスを取得または設定する。
    /// </summary>
    public int SelectedTabIndex {
        get => _selectedTabIndex;
        set {
            if(!Set( ref _selectedTabIndex, value )) {
                return;
            }

            OnSelectedTabIndexChanged();
        }
    }

    /// <summary>
    /// 表示フィルタを取得または設定する。
    /// </summary>
    public IFilterViewModel Filter {
        get => _filter;
        set => Set( ref _filter, value );
    }

    /// <summary>
    /// 取得処理中であるかどうかを取得または設定する。
    /// </summary>
    public bool IsFetching {
        get => _isFetching;
        set {
            if(!Set( ref _isFetching, value )) {
                return;
            }

            NotifyOfPropertyChange( nameof( IsTreeInteractionEnabled ) );
            OnIsFetchingChanged();
        }
    }

    /// <summary>
    /// ツリー操作可否を取得する。
    /// </summary>
    public bool IsTreeInteractionEnabled => IsTreeInteractionEnabledCore();

    /// <summary>
    /// ログ出力サービスを取得する。
    /// </summary>
    protected ILoggingService Logger => logger;

    /// <summary>
    /// UI スレッドディスパッチサービスを取得する。
    /// </summary>
    protected IDispatcherService DispatcherService => dispatcherService;

    /// <summary>
    /// ファイルエントリサービスを取得する。
    /// </summary>
    protected IFileEntryService FileEntryService => fileEntryService;

    /// <summary>
    /// API サービスを取得する。
    /// </summary>
    protected IApiService ApiService => apiService;

    /// <summary>
    /// スナックバーサービスを取得する。
    /// </summary>
    protected ISnackbarService SnackbarService => snackbarService;

    /// <summary>
    /// ViewModel 表示名を取得する。
    /// </summary>
    protected abstract string ViewModelName { get; }

    /// <summary>
    /// タブ生成モードを取得する。
    /// </summary>
    protected abstract ChangeTypeMode TreeMode { get; }

    /// <summary>
    /// ガード関連プロパティの更新通知を行う。
    /// </summary>
    protected abstract void NotifyGuardProperties();

    /// <summary>
    /// 画面アクティブ時に初期化を行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( $"{ViewModelName} をアクティブ化する。" );
        _ = OnDeactivateAsync( close: false, cancellationToken );

        _entriesChangedHandler = entries => HandleEntriesChangedAsync( entries );
        fileEntryService.EntriesChanged += _entriesChangedHandler;

        _filtersChangedHandler = ( _, _ ) => ApplyFilter();
        Filter.FiltersChanged += _filtersChangedHandler;

        fileEntryWatcherLifecycle.StartWatching();

        await Fetch();

        await base.OnActivatedAsync( cancellationToken );
        logger.Info( $"{ViewModelName} のアクティブ化が完了した。" );
    }

    /// <summary>
    /// 画面非アクティブ時に購読解除とクリーンアップを行う。
    /// </summary>
    /// <param name="close">閉じるかどうか。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"{ViewModelName} を非アクティブ化する。Close={close}" );
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

        fileEntryWatcherLifecycle.StopWatching();
        CancelRefreshTabs();
        snackbarService.Clear();
        logger.Info( "リソースを解放した。" );

        await base.OnDeactivateAsync( close, cancellationToken );
    }

    /// <summary>
    /// リポジトリからツリーを取得する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    public async Task Fetch() {
        var fetchStopwatch = Stopwatch.StartNew();
        logger.Info( "ファイル一覧の取得を開始する。" );
        IsFetching = true;
        try {
            var repoResult = await apiService.GetTreeAsync();
            if(repoResult.IsFailed) {
                var reason = repoResult.Errors.Count > 0 ? repoResult.Errors[0].Message : null;
                var message = ResultNotificationPolicy.GetTreeFetchFailureMessage( repoResult.GetFirstErrorKind() );
                logger.Warn( $"リポジトリのファイル一覧取得が失敗した。Reason={reason}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( message );
                    return Task.CompletedTask;
                } );
                return;
            }

            RepoEntries = [.. repoResult.Value];
            logger.Info( $"ファイル一覧を取得した。件数={RepoEntries.Count}" );
            await RefreshTabsAsync();
            _ = RefreshLocalEntriesInBackgroundAsync();
            await dispatcherService.InvokeAsync( () => {
                snackbarService.Show( "リポジトリ一覧の取得が完了しました" );
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
            logger.Info( $"ファイル一覧取得処理を終了した。ElapsedMs={fetchStopwatch.ElapsedMilliseconds}" );
        }
    }

    /// <summary>
    /// 直近のローカルエントリを再取得して表示へ反映する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    protected async Task RefreshLocalEntriesAsync() {
        var entries = await fileEntryService.GetEntriesAsync();
        if(entries is null) {
            logger.Warn( "ローカルエントリの取得結果が null のため処理を中断する。" );
            return;
        }

        if(entries.IsFailed) {
            logger.Warn( "ローカルエントリの再取得に失敗した。" );
            return;
        }

        await dispatcherService.InvokeAsync( () => UpdateLocalEntriesAndRefreshAsync( entries.Value ) );
    }

    /// <summary>
    /// EntriesChanged の購読を一時停止する。
    /// </summary>
    protected void SuspendEntriesChangedSubscription() {
        if(_entriesChangedHandler is null) {
            return;
        }

        fileEntryService.EntriesChanged -= _entriesChangedHandler;
    }

    /// <summary>
    /// EntriesChanged の購読を再開する。
    /// </summary>
    protected void ResumeEntriesChangedSubscription() {
        if(_entriesChangedHandler is null) {
            return;
        }

        fileEntryService.EntriesChanged += _entriesChangedHandler;
    }

    /// <summary>
    /// 現在タブでチェック済み項目が存在するかを判定する。
    /// </summary>
    /// <returns>1件以上チェック済みの場合は <see langword="true"/>。</returns>
    protected bool HasCheckedEntries() =>
        Tabs.Count > 0 &&
        SelectedTabIndex >= 0 &&
        SelectedTabIndex < Tabs.Count &&
        Tabs[SelectedTabIndex].Root.CheckState != false;

    /// <summary>
    /// 現在のフィルタ条件を適用する。
    /// </summary>
    protected void ApplyFilter() {
        var filterStopwatch = Stopwatch.StartNew();
        var types = Filter.GetActiveTypes().ToHashSet();
        var activeTypes = string.Join( ",", types.Select( type => type?.ToString() ?? "null" ) );
        logger.Info( $"フィルタを適用する。ActiveTypes={activeTypes}" );
        fileEntryTreeService.ApplyFilter( Tabs, types );
        NotifyGuardProperties();
        logger.Info( $"フィルタ適用が完了した。ElapsedMs={filterStopwatch.ElapsedMilliseconds}" );
    }

    /// <summary>
    /// リポジトリとローカルのエントリをマージしてタブを再構築する。
    /// </summary>
    protected async Task RefreshTabsAsync() {
        var refreshStopwatch = Stopwatch.StartNew();
        var tabIndex = SelectedTabIndex;
        logger.Info( $"タブを再構築する。LocalCount={LocalEntries.Count}, RepoCount={RepoEntries.Count}, SelectedIndex={tabIndex}" );
        var token = CreateRefreshToken( out var version );
        IReadOnlyList<TabItemViewModel> tabs;
        var buildStopwatch = Stopwatch.StartNew();
        try {
            tabs = await Task.Run( () => fileEntryTreeService.BuildTabs( LocalEntries, RepoEntries, TreeMode ), token );
        }
        catch(OperationCanceledException) {
            logger.Debug( $"タブ再構築をキャンセルした。Version={version}" );
            return;
        }

        logger.Info( $"タブ構築が完了した。Version={version}, BuildTabsElapsedMs={buildStopwatch.ElapsedMilliseconds}" );
        await dispatcherService.InvokeAsync( () => {
            if(token.IsCancellationRequested || !IsLatestRefreshVersion( version )) {
                logger.Debug( $"最新のタブ再構築ではないため反映をスキップする。Version={version}" );
                return Task.CompletedTask;
            }

            var applyStopwatch = Stopwatch.StartNew();
            foreach(var tab in Tabs) {
                tab.Root.CheckStateChanged -= OnRootCheckStateChanged;
            }

            Tabs.Clear();
            foreach(var tab in tabs) {
                Tabs.Add( tab );
            }

            foreach(var tab in Tabs) {
                tab.Root.CheckStateChanged += OnRootCheckStateChanged;
            }

            SelectedTabIndex = Tabs.Count == 0 ? 0 : Math.Clamp( tabIndex, 0, Tabs.Count - 1 );
            ApplyFilter();
            logger.Info( $"タブ反映が完了した。Version={version}, ApplyTabsElapsedMs={applyStopwatch.ElapsedMilliseconds}" );
            logger.Info( $"タブの再構築が完了した。TabCount={Tabs.Count}, SelectedIndex={SelectedTabIndex}, ElapsedMs={refreshStopwatch.ElapsedMilliseconds}" );
            return Task.CompletedTask;
        } );
    }

    /// <summary>
    /// ツリー操作可否の判定を提供する。
    /// </summary>
    /// <returns>操作可能の場合は <see langword="true"/>。</returns>
    protected virtual bool IsTreeInteractionEnabledCore() => !IsFetching;

    /// <summary>
    /// EntriesChanged イベントを無視するかどうかを判定する。
    /// </summary>
    /// <returns>無視する場合は <see langword="true"/>。</returns>
    protected virtual bool ShouldIgnoreEntriesChanged() => false;

    /// <summary>
    /// SelectedTabIndex の変更時処理を提供する。
    /// </summary>
    protected virtual void OnSelectedTabIndexChanged() {
    }

    /// <summary>
    /// IsFetching の変更時処理を提供する。
    /// </summary>
    protected virtual void OnIsFetchingChanged() {
    }

    private Task HandleEntriesChangedAsync( IReadOnlyList<FileEntry> entries ) {
        if(ShouldIgnoreEntriesChanged()) {
            return Task.CompletedTask;
        }

        return dispatcherService.InvokeAsync( () => {
            logger.Info( $"EntriesChanged を受信した。件数={entries.Count}" );
            return UpdateLocalEntriesAndRefreshAsync( entries );
        } );
    }

    /// <summary>
    /// ローカルエントリをバックグラウンドで更新する。
    /// </summary>
    /// <returns>非同期タスク。</returns>
    private async Task RefreshLocalEntriesInBackgroundAsync() {
        if(_isLocalRefreshRunning) {
            return;
        }

        _isLocalRefreshRunning = true;
        try {
            await RefreshLocalEntriesAsync();
        }
        catch(Exception ex) {
            logger.Warn( "バックグラウンドのローカル一覧更新に失敗した。", ex );
        }
        finally {
            _isLocalRefreshRunning = false;
        }
    }

    /// <summary>
    /// ローカルエントリを更新して必要時のみタブを再構築する。
    /// </summary>
    /// <param name="entries">更新対象エントリ。</param>
    private async Task UpdateLocalEntriesAndRefreshAsync( IReadOnlyList<FileEntry> entries ) {
        var signature = ComputeLocalEntriesSignature( entries );
        if(signature == _localEntriesSignature && Tabs.Count > 0) {
            logger.Debug( "ローカル一覧に差分がないためタブ再構築を省略する。" );
            return;
        }

        _localEntriesSignature = signature;
        LocalEntries = entries;
        await RefreshTabsAsync();
    }

    /// <summary>
    /// ローカルエントリの内容署名を算出する。
    /// </summary>
    /// <param name="entries">対象エントリ。</param>
    /// <returns>署名値。</returns>
    private static int ComputeLocalEntriesSignature( IReadOnlyList<FileEntry> entries ) {
        var hash = new HashCode();
        hash.Add( entries.Count );
        foreach(var entry in entries) {
            hash.Add( entry.Path, StringComparer.Ordinal );
            hash.Add( entry.LocalSha, StringComparer.Ordinal );
        }

        return hash.ToHashCode();
    }

    private void OnRootCheckStateChanged( object? sender, bool? e ) {
        _ = sender;
        NotifyGuardProperties();
        logger.Info( $"ルートチェック状態が変化した。SelectedIndex={SelectedTabIndex}, NewState={e}" );
    }

    /// <summary>
    /// タブ再構築の世代を更新し、直前の再構築を取り消す。
    /// </summary>
    /// <param name="version">発行した世代番号。</param>
    /// <returns>新しいキャンセルトークン。</returns>
    private CancellationToken CreateRefreshToken( out int version ) {
        lock(_refreshTabsGate) {
            _refreshTabsCts?.Cancel();
            _refreshTabsCts?.Dispose();
            _refreshTabsCts = new CancellationTokenSource();
            version = ++_refreshTabsVersion;
            return _refreshTabsCts.Token;
        }
    }

    /// <summary>
    /// 指定した世代が最新の再構築であるかを判定する。
    /// </summary>
    /// <param name="version">判定対象世代。</param>
    /// <returns>最新世代の場合は <see langword="true"/>。</returns>
    private bool IsLatestRefreshVersion( int version ) {
        lock(_refreshTabsGate) {
            return version == _refreshTabsVersion;
        }
    }

    /// <summary>
    /// タブ再構築の進行を取り消して状態を解放する。
    /// </summary>
    private void CancelRefreshTabs() {
        lock(_refreshTabsGate) {
            _refreshTabsCts?.Cancel();
            _refreshTabsCts?.Dispose();
            _refreshTabsCts = null;
        }
    }
}