using System.Collections.ObjectModel;
using System.IO;

using Caliburn.Micro;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Helpers;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Features.Upload;

/// <summary>
/// アップロードページの状態を管理する ViewModel
/// </summary>
public sealed class UploadViewModel(
    IApiService apiService,
    IAppSettingsService appSettingsService,
    IDialogService dialogService,
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

    /// <summary>Fetch状態の取得</summary>
    private bool _isFetching = false;

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
    /// 全てのタブ情報を取得する
    /// </summary>
    public ObservableCollection<TabItemViewModel> Tabs {
        get => _tabs;
        set => Set( ref _tabs, value );
    }

    /// <summary>
    /// 選択中のタブインデックス
    /// </summary>
    public int SelectedTabIndex {
        get => _selectedTabIndex;
        set => Set( ref _selectedTabIndex, value );
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
            NotifyOfPropertyChange( nameof( CanShowCreatePullRequestDialog ) );
        }
    }

    #endregion

    #region Action Guards

    /// <summary>
    /// CreatePullRequestDialog を表示可能か
    /// </summary>
    public bool CanShowCreatePullRequestDialog => !_isFetching && HasChecked();

    /// <summary>
    /// ツリー操作が許可されているか。
    /// </summary>
    public bool IsTreeInteractionEnabled => !IsFetching;

    #endregion

    #region Lifecycle

    /// <summary>
    /// 画面アクティブ時に初期化を行う。
    /// </summary>
    /// <param name="cancellationToken">キャンセル トークン</param>
    /// <returns>非同期タスク</returns>
    /// <exception cref="ObjectDisposedException">監視開始に失敗した場合</exception>
    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( "UploadViewModel をアクティブ化する。" );
        // 既存購読を解除してから再購読する
        _ = OnDeactivateAsync( close: false, cancellationToken );

        _entriesChangedHandler = entries =>
        dispatcherService.InvokeAsync( () => {
            logger.Info( $"EntriesChanged を受信した。件数={entries.Count}" );
            LocalEntries = entries;
            RefreshTabs();
            return Task.CompletedTask;
        } );
        fileEntryService.EntriesChanged += _entriesChangedHandler!;

        _filtersChangedHandler = ( _, _ ) => ApplyFilter();
        Filter.FiltersChanged += _filtersChangedHandler;

        fileEntryService.Watch( appSettingsService.Settings.TranslateFileDir );
        logger.Info( $"ファイル監視を開始した。Directory={appSettingsService.Settings.TranslateFileDir}" );

        // 起動時取得は待たずに開始する
        await Fetch();

        await OnActivatedAsync( cancellationToken );
        logger.Info( "UploadViewModel のアクティブ化が完了した。" );
    }

    /// <summary>
    /// 画面非アクティブ時に購読解除とクリーンアップを行う。
    /// </summary>
    /// <param name="close">閉じるかどうか</param>
    /// <param name="cancellationToken">キャンセル トークン</param>
    /// <returns>非同期タスク</returns>
    protected override async Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"UploadViewModel を非アクティブ化する。Close={close}" );
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
                logger.Warn(
                    $"リポジトリのファイル一覧取得が失敗した。Reason={reason}" );
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
            logger.Error( ex.Message, ex );
            logger.Warn( "ファイル一覧取得処理で例外が発生した。" );
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
    /// 翻訳ファイルの管理ディレクトリをエクスプローラーで開く。
    /// </summary>
    public void OpenDirectory() {
        logger.Info( $"翻訳ファイルディレクトリを開く。Directory={appSettingsService.Settings.TranslateFileDir}" );
        systemService.OpenDirectory( appSettingsService.Settings.TranslateFileDir );
    }

    /// <summary>
    /// CreatePullRequestDialog を表示する。
    /// </summary>
    public async Task ShowCreatePullRequestDialog() {
        logger.Info( "Pull Request ダイアログ表示を開始する。" );
        var dialogParameters = new CreatePullRequestDialogParameters()
        {
            Category = Tabs[SelectedTabIndex].Title,
            SubCategory = GetSubCategory(),
            CommitFiles = GetCommitFiles,
        };
        logger.Info( $"ダイアログ引数を構築した。Category={dialogParameters.Category}, SubCategory={dialogParameters.SubCategory}, FileCount={dialogParameters.CommitFiles.Count()}" );

        // 削除するファイルが含まれる場合確認ダイアログを表示し、Yesでない場合即座に中止する。
        if(dialogParameters.CommitFiles.Any( cf => cf.Operation == CommitOperationType.Delete )) {
            logger.Warn( "削除予定のファイルが含まれているため確認ダイアログを表示する。" );
            var confirmed = await dialogService.ContinueCancelDialogShowAsync(
                new ConfirmationDialogParameters
                {
                    Title = "削除確認",
                    Message = "削除予定のファイルが含まれます。続行しますか？",
                    ConfirmButtonText = "続行",
                    CancelButtonText = "中止",
                } );

            if(!confirmed) {
                logger.Warn( "削除確認でキャンセルが選択されたため処理を終了する。" );
                return;
            }
        }


        async Task ShowAsync( string message, string? action = null, System.Action? handler = null ) =>
            await dispatcherService.InvokeAsync( () => {
                snackbarService.Show( message, action, handler );
                return Task.CompletedTask;
            } );

        try {
            var result = await dialogService.CreatePullRequestDialogShowAsync( dialogParameters );
            logger.Info( $"Pull Request ダイアログが完了した。IsOk={result.IsOk}" );

            var (message, actionContent, actionHandler) = result switch
            {
                {
                    IsOk: true,
                    PrUrl: string { Length: > 0 } prUrl
                } => ("Pull Request の作成に成功しました", "開く", (System.Action)(() => systemService.OpenInWebBrowser( prUrl ))),

                { IsOk: true }
                    => ("Pull Request の作成に成功しました", null, null),

                { Errors: not null } when result.Errors.Any( e => e is OperationCanceledException )
                    => ("Pull Request の作成をキャンセルしました", null, null),

                { Errors: [{ Message: var m }] } when !string.IsNullOrWhiteSpace( m )
                    => ($"Pull Request の作成に失敗しました: {m}", null, null),

                _ => ("Pull Request の作成に失敗しました", null, null)
            };

            await ShowAsync( message, actionContent, actionHandler );
        }
        catch(OperationCanceledException) {
            logger.Warn( "Pull Request ダイアログがキャンセルされた。" );
            await ShowAsync( "Pull Request の作成をキャンセルしました" );
        }
        catch(Exception ex) {
            logger.Error( ex.Message, ex );
            logger.Warn( "Pull Request ダイアログ処理で例外が発生した。" );
            await ShowAsync( $"Pull Request ダイアログで例外が発生しました: {ex.Message}" );
        }
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// 現在のタブでチェックありかを判定する。
    /// </summary>
    /// <returns>チェックが1つ以上なら <see langword="true"/></returns>
    private bool HasChecked() =>
        Tabs.Count > 0 &&
        SelectedTabIndex >= 0 &&
        SelectedTabIndex < Tabs.Count &&
        Tabs[SelectedTabIndex].Root.CheckState != false;

    /// <summary>
    /// リポジトリとローカルのエントリをマージしてタブを再構築する。
    /// </summary>
    private void RefreshTabs() {
        var tabIndex = SelectedTabIndex;
        logger.Info( $"タブを再構築する。LocalCount={LocalEntries.Count}, RepoCount={RepoEntries.Count}, SelectedIndex={tabIndex}" );

        var entries = FileEntryComparisonHelper.Merge(LocalEntries, RepoEntries);

        IFileEntryViewModel rootVm = new FileEntryViewModel(new FileEntry(string.Empty, string.Empty, true), ChangeTypeMode.Upload, logger);
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
                target ?? new FileEntryViewModel(new FileEntry("null", string.Empty, false), ChangeTypeMode.Upload, logger)
            );
        }).ToList();

        foreach(var t in Tabs) t.Root.CheckStateChanged -= OnRootCheckStateChanged;
        Tabs.Clear();
        foreach(var t in tabs) Tabs.Add( t );
        foreach(var t in Tabs) t.Root.CheckStateChanged += OnRootCheckStateChanged;

        SelectedTabIndex = Tabs.Count == 0 ? 0 : Math.Clamp( tabIndex, 0, Tabs.Count - 1 );

        ApplyFilter();
        logger.Info( $"タブの再構築が完了した。TabCount={Tabs.Count}, SelectedIndex={SelectedTabIndex}" );
    }

    /// <summary>
    /// ルートノードのチェック状態変化時にガードを更新する。
    /// </summary>
    /// <param name="sender">送信元</param>
    /// <param name="e">チェック状態</param>
    private void OnRootCheckStateChanged( object? sender, bool? e ) {
        _ = sender;
        _ = e;
        NotifyOfPropertyChange( nameof( CanShowCreatePullRequestDialog ) );
        logger.Info( $"ルートチェック状態が変化した。SelectedIndex={SelectedTabIndex}, NewState={e}" );
    }

    /// <summary>
    /// 現在のフィルタ条件を適用する。
    /// </summary>
    private void ApplyFilter() {
        var types = Filter.GetActiveTypes().ToHashSet();
        var activeTypes = string.Join( ",", types.Select( t => t?.ToString() ?? "null" ) );
        logger.Info( $"フィルタを適用する。ActiveTypes={activeTypes}" );
        foreach(var tab in Tabs) {
            ApplyFilterRecursive( tab.Root, types );
        }
        NotifyOfPropertyChange( nameof( CanShowCreatePullRequestDialog ) );
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
                next = new FileEntryViewModel( new FileEntry( part, absolutePath, true ), ChangeTypeMode.Upload, loggingService );
                current.Children.Add( next );
            }
            current = next;
        }

        var last = parts[^1];
        if(!current.Children.Any( c => c?.Name == last )) {
            current.Children.Add(
                new FileEntryViewModel(
                    new FileEntry( last, entry.Path, entry.IsDirectory, entry.LocalSha, entry.RepoSha ),
                    ChangeTypeMode.Upload,
                    loggingService ) );
        }
    }

    private string GetSubCategory() {
        var cur = Tabs[_selectedTabIndex].Root;
        List<FileChangeType?> typeFilter = [FileChangeType.LocalOnly, FileChangeType.Modified];

        if(cur.Children.Count( c => typeFilter.Contains( c.ChangeType ) && c.CheckState != false ) != 1) throw new Exception();
        cur = cur.Children.First( c => typeFilter.Contains( c.ChangeType ) && c.CheckState != false );

        var name = cur.Name;
        logger.Info( $"サブカテゴリーを算出した。Name={name}" );
        return name;
    }

    private IEnumerable<CommitFile> GetCommitFiles {
        get {
            var files = Tabs[SelectedTabIndex]
            .GetCheckedEntries()
            .Where( e => !e.IsDirectory && e.RepoSha != e.LocalSha )
            .Select( entry => new CommitFile()
            {
                Operation = (entry.RepoSha, entry.LocalSha) switch
                {
                    (string _, null ) => CommitOperationType.Delete,
                    _ => CommitOperationType.Upsert,
                },
                LocalPath = Path.Combine( appSettingsService.Settings.TranslateFileDir, entry.Path ),
                RepoPath = entry.Path,
            } )
            .ToList();
            logger.Info( $"コミット対象ファイルを収集した。件数={files.Count}" );
            return files;
        }
    }
    #endregion

}
