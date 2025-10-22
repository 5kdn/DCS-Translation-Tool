using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;

using Caliburn.Micro;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
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
    IDispatcherService dispatcherService,
    IFileEntryService fileEntryService,
    IFileService fileService,
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService,
    IZipService zipService
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
            NotifyOfPropertyChange( () => CanDownload );
            NotifyOfPropertyChange( () => CanApply );
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
        set => Set( ref _isFetching, value );
    }

    public bool IsDownloading {
        get => _isDownloading;
        set => Set( ref _isDownloading, value );
    }

    public double DownloadedProgress {
        get => _downloadedProgress;
        set => Set( ref _downloadedProgress, Math.Clamp( value, 0, 100 ) );
    }

    public bool IsApplying {
        get => _isApplying;
        set => Set( ref _isApplying, value );
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
                logger.Error( $"リポジトリのファイル一覧取得が失敗した。Reason={reason}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "リポジトリファイル一覧の取得に失敗しました" );
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

        IsDownloading = true;
        DownloadedProgress = 0.0;
        NotifyOfPropertyChange( () => CanDownload );
        NotifyOfPropertyChange( () => CanApply );

        try {
            if(Tabs.Count == 0 || SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                logger.Warn( "タブが選択されていないためダウンロードを中断する。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "タブが選択されていません" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var tab = Tabs[SelectedTabIndex];
            var checkedEntries = tab.GetCheckedEntries();
            if(checkedEntries is null) {
                logger.Warn( "チェックされたエントリが存在しない。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "ダウンロード対象が有りません" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var targetEntries = checkedEntries.Where(e => !e.IsDirectory).ToList();
            if(targetEntries.Count == 0) {
                logger.Warn( "ダウンロード対象のファイルが存在しない。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "ダウンロード対象が有りません" );
                    return Task.CompletedTask;
                } );
                return;
            }
            logger.Info( $"ダウンロード対象を特定した。件数={targetEntries.Count}" );

            IReadOnlyList<string> paths = targetEntries.ConvertAll(e => e.Path);
            var result = await apiService.DownloadFilesAsync(new ApiDownloadFilesRequest(paths, null));
            if(result.IsFailed) {
                var reason = result.Errors.Count > 0 ? result.Errors[0].Message : null;
                logger.Error( $"リポジトリからファイルの一括取得に失敗した。Reason={reason}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "リポジトリからの一括取得に失敗しました" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var newFiles = result.Value;
            var saveRootPath = appSettingsService.Settings.TranslateFileDir;
            if(string.IsNullOrWhiteSpace( saveRootPath )) {
                logger.Warn( "保存先ディレクトリが設定されていないため保存を中断する。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "保存先フォルダーが設定されていません" );
                    return Task.CompletedTask;
                } );
                return;
            }

            if(newFiles.IsNotModified) {
                logger.Info( "ダウンロード対象が最新のため保存をスキップする。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "対象ファイルは最新です" );
                    return Task.CompletedTask;
                } );
                return;
            }

            if(newFiles.Content.Length == 0) {
                logger.Warn( "APIから空のZIPが返却されたため保存を中断する。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "保存対象が見つかりませんでした" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var rootFullPath = Path.GetFullPath( saveRootPath );
            Directory.CreateDirectory( rootFullPath );
            var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? rootFullPath
                : rootFullPath + Path.DirectorySeparatorChar;

            await using MemoryStream zipStream = new( newFiles.Content, writable: false );
            using ZipArchive archive = new( zipStream, ZipArchiveMode.Read, leaveOpen: false );

            var fileEntries = archive.Entries.Where( entry => !string.IsNullOrEmpty( entry?.Name ) ).ToList();
            if(fileEntries.Count == 0) {
                logger.Warn( "ZIPアーカイブに保存対象のファイルエントリが含まれていなかった。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "保存対象が見つかりませんでした" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var processed = 0;
            var success = 0;
            var failed = 0;
            DownloadedProgress = 0;

            foreach(var entry in archive.Entries) {
                var normalizedEntry = entry.FullName.Replace( '\\', '/' ).TrimStart( '/' );
                if(string.IsNullOrWhiteSpace( normalizedEntry )) continue;

                if(string.IsNullOrEmpty( entry.Name )) {
                    var directoryPath = Path.GetFullPath(
                        Path.Combine( rootFullPath, normalizedEntry.Replace( '/', Path.DirectorySeparatorChar ) )
                    );
                    if(directoryPath.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
                        Directory.CreateDirectory( directoryPath );
                    }
                    else {
                        logger.Warn( $"ZIPエントリが保存先の外を指しているためスキップする。Entry={entry.FullName}" );
                    }
                    continue;
                }

                var destinationPath = Path.GetFullPath(
                    Path.Combine( rootFullPath, normalizedEntry.Replace( '/', Path.DirectorySeparatorChar ) )
                );
                if(!destinationPath.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
                    logger.Warn( $"ZIPエントリが保存先の外を指しているためスキップする。Entry={entry.FullName}" );
                    failed++;
                    processed++;
                    DownloadedProgress = Math.Min( 100, (double)processed / fileEntries.Count * 100 );
                    continue;
                }

                try {
                    var directoryName = Path.GetDirectoryName( destinationPath );
                    if(!string.IsNullOrEmpty( directoryName )) Directory.CreateDirectory( directoryName );

                    await using Stream entryStream = entry.Open();
                    await using MemoryStream buffer = new();
                    await entryStream.CopyToAsync( buffer );
                    await fileService.SaveAsync( destinationPath, buffer.ToArray() );
                    success++;
                    logger.Info( $"ファイルを保存した。Path={destinationPath}" );
                }
                catch(Exception ex) {
                    failed++;
                    logger.Error( $"ZIPエントリの展開に失敗した。Entry={entry.FullName}, Path={destinationPath}", ex );
                }
                finally {
                    if(!string.IsNullOrEmpty( entry.Name )) {
                        processed++;
                        DownloadedProgress = Math.Min( 100, (double)processed / fileEntries.Count * 100 );
                    }
                }
            }

            DownloadedProgress = 100;
            if(failed > 0) {
                logger.Warn( $"ZIP展開で一部のファイル保存に失敗した。Success={success}, Failed={failed}, Total={fileEntries.Count}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( $"一部のファイルの保存に失敗しました ({failed}/{fileEntries.Count})" );
                    return Task.CompletedTask;
                } );
            }
            else {
                logger.Info( $"ZIP展開が完了した。Success={success}, Total={fileEntries.Count}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "ダウンロード完了" );
                    return Task.CompletedTask;
                } );
            }

            logger.Info( "ダウンロード処理が完了した。" );
        }
        finally {
            IsDownloading = false;
            NotifyOfPropertyChange( () => CanDownload );
            NotifyOfPropertyChange( () => CanApply );
            logger.Info( "ダウンロード処理を終了した。" );
        }
    }

    /// <summary>
    /// 選択されたファイルを miz ファイルに適用する
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Apply() {
        logger.Info( "適用処理を開始する。" );
        if(!CanApply) {
            logger.Warn( "適用は現在許可されていないため処理を中断する。" );
            return;
        }

        IsApplying = true;
        AppliedProgress = 0.0;
        NotifyOfPropertyChange( () => CanApply );
        NotifyOfPropertyChange( () => CanDownload );

        try {
            if(Tabs.Count == 0 || SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                logger.Warn( "タブが選択されていないため適用処理を中断する。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "タブが選択されていません" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var tab = Tabs[SelectedTabIndex];
            var targetEntries = GetTargetFileNodes().Where(e => !e.IsDirectory).ToList();

            if(targetEntries.Count == 0) {
                logger.Warn( "適用対象のファイルが存在しない。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "対象が有りません" );
                    return Task.CompletedTask;
                } );
                return;
            }
            logger.Info( $"適用対象を特定した。件数={targetEntries.Count}" );

            string rootPath = tab.TabType switch
            {
                CategoryType.Aircraft => appSettingsService.Settings.SourceAircraftDir,
                CategoryType.DlcCampaigns => appSettingsService.Settings.SourceDlcCampaignDir,
                _ => throw new InvalidOperationException($"未対応のタブ種別: {tab.TabType}"),
            };

            if(string.IsNullOrWhiteSpace( rootPath )) {
                logger.Warn( "適用先ディレクトリが設定されていないため処理を中断する。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "適用先ディレクトリを設定してください" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var rootFullPath = Path.GetFullPath( rootPath );
            if(!Directory.Exists( rootFullPath )) {
                logger.Warn( $"適用先ディレクトリが存在しない。Directory={rootFullPath}" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "適用先ディレクトリが存在しません" );
                    return Task.CompletedTask;
                } );
                return;
            }
            var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? rootFullPath
                : rootFullPath + Path.DirectorySeparatorChar;

            var translateRoot = appSettingsService.Settings.TranslateFileDir;
            if(string.IsNullOrWhiteSpace( translateRoot )) {
                logger.Warn( "翻訳ディレクトリが未設定のため処理を中断する。" );
                await dispatcherService.InvokeAsync( () => {
                    snackbarService.Show( "翻訳ディレクトリを設定してください" );
                    return Task.CompletedTask;
                } );
                return;
            }

            var translateFullPath = Path.GetFullPath( translateRoot );
            Directory.CreateDirectory( translateFullPath );
            var translateRootWithSeparator = translateFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? translateFullPath
                : translateFullPath + Path.DirectorySeparatorChar;

            var repoOnlyEntries = targetEntries
                .Where( entry => entry.ChangeType == FileChangeType.RepoOnly )
                .ToList();

            if(repoOnlyEntries.Count > 0) {
                logger.Info( $"リポジトリのみのファイルを取得する。Count={repoOnlyEntries.Count}" );
                var downloadResult = await apiService.DownloadFilesAsync(
                    new ApiDownloadFilesRequest( repoOnlyEntries.ConvertAll( e => e.Path ), null ) );
                if(downloadResult.IsFailed) {
                    var reason = downloadResult.Errors.Count > 0 ? downloadResult.Errors[0].Message : null;
                    logger.Error( $"リポジトリからの取得に失敗した。Reason={reason}" );
                    await dispatcherService.InvokeAsync( () => {
                        snackbarService.Show( "リポジトリからの取得に失敗しました" );
                        return Task.CompletedTask;
                    } );
                    return;
                }

                var archive = downloadResult.Value;
                if(!archive.IsNotModified) {
                    if(archive.Content.Length == 0) {
                        logger.Warn( "取得した ZIP が空のため適用を中断する。" );
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( "取得ファイルが見つかりませんでした" );
                            return Task.CompletedTask;
                        } );
                        return;
                    }

                    var extracted = await ExtractDownloadArchiveAsync( archive, translateFullPath );
                    if(!extracted) {
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( "リポジトリファイルの保存に失敗しました" );
                            return Task.CompletedTask;
                        } );
                        return;
                    }
                }

                foreach(var repoEntry in repoOnlyEntries) {
                    if(!TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, repoEntry.Path, out var repoPath ) || !File.Exists( repoPath )) {
                        logger.Warn( $"取得後もファイルが存在しない。Path={repoEntry.Path}, Resolved={repoPath}" );
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"取得失敗: {repoEntry.Path}" );
                            return Task.CompletedTask;
                        } );
                        return;
                    }
                }
            }

            var progressed = 0;
            var totalApplied = targetEntries.Count;
            var success = 0;
            var failed = 0;

            foreach(var entry in targetEntries) {
                try {
                    if(!TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, entry.Path, out var sourceFilePath )) {
                        logger.Warn( $"翻訳ディレクトリ外のファイルが指定されたためスキップする。Path={entry.Path}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"不正な翻訳ファイル: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    if(!File.Exists( sourceFilePath )) {
                        logger.Warn( $"翻訳ファイルが存在しないため適用できない。Path={sourceFilePath}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"翻訳ファイルが見つかりません: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    var parts = entry.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var mizIndex = Array.FindIndex(parts, p => p.EndsWith(".miz", StringComparison.OrdinalIgnoreCase));
                    if(mizIndex == -1) {
                        logger.Warn( $".miz の位置を特定できず適用に失敗した。Path={entry.Path}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"不正なパス: .miz が見つかりません -> {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    var mizSegments = parts.Take(mizIndex + 1).Skip(3).ToArray();
                    if(mizSegments.Length == 0) {
                        logger.Warn( $"パス構造が不正のため適用に失敗した。Path={entry.Path}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"不正なパス構造: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    var mizRelativePath = string.Join('/', mizSegments);
                    if(!TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, mizRelativePath, out var mizPath )) {
                        logger.Warn( $"適用先がルート外を指しているため拒否した。Entry={entry.Path}, MizRelative={mizRelativePath}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"不正な適用先: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    if(!File.Exists( mizPath )) {
                        logger.Warn( $"適用先の miz ファイルが存在しない。MizPath={mizPath}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"miz ファイルが存在しません: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    var entryPathSegments = parts.Skip(mizIndex + 1).ToArray();
                    if(entryPathSegments.Length == 0) {
                        logger.Warn( $"miz 内のパスが空のため適用できない。Path={entry.Path}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"miz 内パスが不正です: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    var entryPath = string.Join('/', entryPathSegments);
                    var addResult = zipService.AddEntry( mizPath, entryPath, sourceFilePath );
                    if(addResult.IsFailed) {
                        var reason = string.Join( ", ", addResult.Errors.Select( e => e.Message ) );
                        logger.Warn( $"miz への適用に失敗した。MizPath={mizPath}, EntryPath={entryPath}, Reason={reason}" );
                        failed++;
                        await dispatcherService.InvokeAsync( () => {
                            snackbarService.Show( $"適用失敗: {entry.Path}" );
                            return Task.CompletedTask;
                        } );
                        continue;
                    }

                    logger.Info( $"miz ファイルへ適用した。MizPath={mizPath}, EntryPath={entryPath}" );
                    success++;
                }
                catch(Exception ex) {
                    failed++;
                    logger.Error( $"適用処理で例外が発生した。Path={entry.Path}", ex );
                    await dispatcherService.InvokeAsync( () => {
                        snackbarService.Show( $"適用失敗: {entry.Path}" );
                        return Task.CompletedTask;
                    } );
                }
                finally {
                    AppliedProgress = (double)++progressed / totalApplied * 100;
                }
            }

            await dispatcherService.InvokeAsync( () => {
                snackbarService.Show( $"適用完了 成功:{success} 件 失敗:{failed} 件" );
                return Task.CompletedTask;
            } );
            logger.Info( $"適用処理が完了した。成功={success}, 失敗={failed}" );
        }
        finally {
            IsApplying = false;
            NotifyOfPropertyChange( () => CanApply );
            NotifyOfPropertyChange( () => CanDownload );
            logger.Info( "適用処理を終了した。" );
        }
    }

    /// <summary>
    /// 翻訳ファイルの管理ディレクトリをエクスプローラーで開く
    /// </summary>
    public void OpenDirectory() {
        logger.Info( $"翻訳ファイルディレクトリを開く。Directory={appSettingsService.Settings.TranslateFileDir}" );
        systemService.OpenDirectory( appSettingsService.Settings.TranslateFileDir );
    }

    #endregion

    #region Private Helpers

    /// <summary>ZIPアーカイブを指定ディレクトリへ展開する。</summary>
    private async Task<bool> ExtractDownloadArchiveAsync( ApiDownloadFilesResult archive, string destinationRootFullPath ) {
        ArgumentNullException.ThrowIfNull( archive );
        ArgumentException.ThrowIfNullOrWhiteSpace( destinationRootFullPath );

        var rootWithSeparator = destinationRootFullPath.EndsWith( Path.DirectorySeparatorChar )
            ? destinationRootFullPath
            : destinationRootFullPath + Path.DirectorySeparatorChar;

        await using MemoryStream zipStream = new( archive.Content, writable: false );
        using ZipArchive zip = new( zipStream, ZipArchiveMode.Read, leaveOpen: false );

        var entries = zip.Entries.Where( entry => !string.IsNullOrEmpty( entry?.FullName ) ).ToList();
        if(entries.Count == 0) {
            logger.Warn( "ZIPアーカイブに展開対象のエントリが含まれていないため処理を中断する。" );
            return false;
        }

        var hasFailure = false;
        foreach(var entry in zip.Entries) {
            var normalizedEntry = entry.FullName.Replace( '\\', '/' ).TrimStart( '/' );
            if(string.IsNullOrWhiteSpace( normalizedEntry )) continue;

            if(string.IsNullOrEmpty( entry.Name )) {
                if(TryResolvePathWithinRoot( destinationRootFullPath, rootWithSeparator, normalizedEntry, out var directoryPath )) {
                    Directory.CreateDirectory( directoryPath );
                }
                else {
                    hasFailure = true;
                    logger.Warn( $"ZIPエントリが保存先の外を指しているためスキップする。Entry={entry.FullName}" );
                }
                continue;
            }

            if(!TryResolvePathWithinRoot( destinationRootFullPath, rootWithSeparator, normalizedEntry, out var destinationPath )) {
                hasFailure = true;
                logger.Warn( $"ZIPエントリが保存先の外を指しているためスキップする。Entry={entry.FullName}" );
                continue;
            }

            try {
                var directoryName = Path.GetDirectoryName( destinationPath );
                if(!string.IsNullOrEmpty( directoryName )) Directory.CreateDirectory( directoryName );

                await using Stream entryStream = entry.Open();
                await using MemoryStream buffer = new();
                await entryStream.CopyToAsync( buffer );
                await fileService.SaveAsync( destinationPath, buffer.ToArray() );
                logger.Info( $"ZIPエントリを展開した。Path={destinationPath}" );
            }
            catch(Exception ex) {
                hasFailure = true;
                logger.Error( $"ZIPエントリの展開に失敗した。Entry={entry.FullName}, Path={destinationPath}", ex );
            }
        }

        return !hasFailure;
    }

    /// <summary>ルートディレクトリ配下に収まるようにパスを解決する。</summary>
    private static bool TryResolvePathWithinRoot( string rootFullPath, string rootWithSeparator, string relativePath, out string resolvedPath ) {
        resolvedPath = string.Empty;
        if(string.IsNullOrWhiteSpace( relativePath )) return false;

        var candidate = Path.GetFullPath(
            Path.Combine( rootFullPath, relativePath.Replace( '/', Path.DirectorySeparatorChar ) )
        );

        if(!candidate.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase ))
            return false;

        resolvedPath = candidate;
        return true;
    }

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
        NotifyOfPropertyChange( () => CanDownload );
        NotifyOfPropertyChange( () => CanApply );
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
        NotifyOfPropertyChange( () => CanDownload );
        NotifyOfPropertyChange( () => CanApply );
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
        NotifyOfPropertyChange( () => CanDownload );
        NotifyOfPropertyChange( () => CanApply );
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