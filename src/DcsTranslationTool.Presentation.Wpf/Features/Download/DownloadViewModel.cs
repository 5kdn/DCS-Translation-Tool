using System.Buffers;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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

    private const string ManifestFileName = "manifest.json";
    private const int StreamCopyBufferSize = 128 * 1024;
    private static readonly string[] ZipLikeExtensions = [".miz", ".trk"];
    /// <summary>manifest.jsonを逆シリアル化するときのオプションを提供する。</summary>
    private static readonly JsonSerializerOptions ManifestSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

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

        var saveRootPath = appSettingsService.Settings.TranslateFileDir;
        if(string.IsNullOrWhiteSpace( saveRootPath )) {
            logger.Warn( "保存先ディレクトリが設定されていないため保存を中断する。" );
            await ShowSnackbarAsync( "保存先フォルダーが設定されていません" );
            return;
        }

        IsDownloading = true;
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
            var result = await apiService.DownloadFilesAsync( new ApiDownloadFilesRequest( paths, null ) );
            if(result.IsFailed) {
                var reason = result.Errors.Count > 0 ? result.Errors[0].Message : null;
                logger.Error( $"リポジトリからファイルの一括取得に失敗した。Reason={reason}" );
                await ShowSnackbarAsync( "リポジトリからの一括取得に失敗しました" );
                return;
            }

            var newFiles = result.Value;

            if(newFiles.IsNotModified) {
                logger.Info( "ダウンロード対象が最新のため保存をスキップする。" );
                await ShowSnackbarAsync( "対象ファイルは最新です" );
                return;
            }

            if(newFiles.Content.Length == 0) {
                logger.Warn( "APIから空のZIPが返却されたため保存を中断する。" );
                await ShowSnackbarAsync( "保存対象が見つかりませんでした" );
                return;
            }

            var processed = await Task.Run( () => ProcessDownloadArchiveAsync( newFiles, saveRootPath ) );
            if(!processed) return;
        }
        finally {
            IsDownloading = false;
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

        IsApplying = true;
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

            var applyCompleted = await Task.Run( () => ProcessApplyAsync(
                targetEntries,
                rootFullPath,
                rootWithSeparator,
                translateFullPath,
                translateRootWithSeparator
            ) );
            if(!applyCompleted) return;
        }
        finally {
            IsApplying = false;
            NotifyOfPropertyChange( nameof( CanApply ) );
            NotifyOfPropertyChange( nameof( CanDownload ) );
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

    /// <summary>
    /// ダウンロードした ZIP アーカイブを検証して保存する。
    /// </summary>
    /// <param name="newFiles">API から取得したアーカイブ。</param>
    /// <param name="saveRootPath">保存先ルートディレクトリ。</param>
    /// <returns>処理が成功した場合は true。</returns>
    private async Task<bool> ProcessDownloadArchiveAsync( ApiDownloadFilesResult newFiles, string saveRootPath ) {
        var rootFullPath = Path.GetFullPath( saveRootPath );
        Directory.CreateDirectory( rootFullPath );
        var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;

        await using MemoryStream zipStream = new( newFiles.Content, writable: false );
        using ZipArchive archive = new( zipStream, ZipArchiveMode.Read, leaveOpen: false );

        async Task<bool> FailManifestAsync() {
            await ShowSnackbarAsync( "マニフェストの検証に失敗しました" );
            return false;
        }

        var manifestEntry = archive.Entries.FirstOrDefault( entry => string.Equals( entry.Name, ManifestFileName, StringComparison.OrdinalIgnoreCase ) );
        if(manifestEntry is null) {
            logger.Error( "ZIPアーカイブに manifest.json が含まれていない。" );
            return await FailManifestAsync();
        }

        DownloadManifest? manifest;
        try {
            using StreamReader reader = new( manifestEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true );
            string manifestJson = await reader.ReadToEndAsync().ConfigureAwait( false );
            manifest = JsonSerializer.Deserialize<DownloadManifest>( manifestJson, ManifestSerializerOptions );
        }
        catch(Exception ex) {
            logger.Error( "manifest.json の解析に失敗した。", ex );
            return await FailManifestAsync();
        }

        if(manifest is null) {
            logger.Error( "manifest.json の解析結果が null だった。" );
            return await FailManifestAsync();
        }

        if(manifest.Version != 1) {
            logger.Warn( $"マニフェストのバージョンが想定外だった。Version={manifest.Version}" );
            return await FailManifestAsync();
        }

        if(string.IsNullOrWhiteSpace( manifest.GeneratedAt ) || !DateTimeOffset.TryParse( manifest.GeneratedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _ )) {
            logger.Warn( $"マニフェストの生成日時が不正だった。GeneratedAt={manifest.GeneratedAt}" );
            return await FailManifestAsync();
        }

        if(manifest.Files is null || manifest.Files.Count == 0) {
            logger.Warn( "マニフェストにファイル情報が含まれていなかった。" );
            return await FailManifestAsync();
        }

        var manifestFiles = new Dictionary<string, DownloadManifestFile>( StringComparer.OrdinalIgnoreCase );
        foreach(var file in manifest.Files) {
            if(file is null) {
                logger.Warn( "マニフェストに null のファイル情報が含まれていた。" );
                return await FailManifestAsync();
            }

            if(string.IsNullOrWhiteSpace( file.Path )) {
                logger.Warn( "マニフェストのファイルパスが空だった。" );
                return await FailManifestAsync();
            }

            if(file.Size < 0) {
                logger.Warn( $"マニフェストのファイルサイズが不正だった。Path={file.Path}, Size={file.Size}" );
                return await FailManifestAsync();
            }

            var normalizedManifestPath = NormalizeArchivePath( file.Path );
            if(string.IsNullOrWhiteSpace( normalizedManifestPath )) {
                logger.Warn( $"マニフェストのファイルパスが不正だった。Path={file.Path}" );
                return await FailManifestAsync();
            }

            if(!IsValidSha256( file.Sha256 )) {
                logger.Warn( $"マニフェストのハッシュ値が不正だった。Path={file.Path}, Sha256={file.Sha256}" );
                return await FailManifestAsync();
            }

            if(manifestFiles.ContainsKey( normalizedManifestPath )) {
                logger.Warn( $"マニフェストに重複するファイルパスが含まれていた。Path={normalizedManifestPath}" );
                return await FailManifestAsync();
            }

            manifestFiles.Add( normalizedManifestPath, file );
        }

        var fileEntries = archive.Entries
            .Where( entry => !string.IsNullOrEmpty( entry?.Name ) && !string.Equals( entry.Name, ManifestFileName, StringComparison.OrdinalIgnoreCase ) )
            .ToList();
        if(fileEntries.Count == 0) {
            logger.Warn( "ZIPアーカイブに保存対象のファイルエントリが含まれていなかった。" );
            return await FailManifestAsync();
        }

        var entryPathSet = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        var duplicatePaths = new List<string>();
        foreach(var entry in fileEntries) {
            var normalizedPath = NormalizeArchivePath( entry.FullName );
            if(!entryPathSet.Add( normalizedPath )) duplicatePaths.Add( normalizedPath );
        }

        if(duplicatePaths.Count > 0) {
            var duplicates = string.Join( ",", duplicatePaths );
            logger.Error( $"ZIPアーカイブに重複するファイルエントリが含まれている。Duplicates={duplicates}" );
            return await FailManifestAsync();
        }

        var missingManifestFiles = manifestFiles.Keys.Where( path => !entryPathSet.Contains( path ) ).ToList();
        if(missingManifestFiles.Count > 0) {
            var missing = string.Join( ",", missingManifestFiles );
            logger.Error( $"マニフェストに記載されたファイルが ZIP に存在しない。Missing={missing}" );
            return await FailManifestAsync();
        }

        var unexpectedEntries = entryPathSet.Where( path => !manifestFiles.ContainsKey( path ) ).ToList();
        if(unexpectedEntries.Count > 0) {
            var unexpected = string.Join( ",", unexpectedEntries );
            logger.Error( $"マニフェストに記載されていないファイルが ZIP に含まれている。Unexpected={unexpected}" );
            return await FailManifestAsync();
        }

        var totalFiles = manifestFiles.Count;
        if(totalFiles == 0) {
            logger.Warn( "マニフェストに保存対象のファイルが存在しなかった。" );
            return await FailManifestAsync();
        }

        var processed = 0;
        var success = 0;
        var failed = 0;
        await UpdateDownloadProgressAsync( 0 );

        foreach(var entry in archive.Entries) {
            var normalizedEntry = NormalizeArchivePath( entry.FullName );
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

            if(string.Equals( entry.Name, ManifestFileName, StringComparison.OrdinalIgnoreCase )) {
                logger.Info( $"manifest.json を保存対象から除外する。Entry={entry.FullName}" );
                continue;
            }

            if(!manifestFiles.TryGetValue( normalizedEntry, out var manifestFile )) {
                logger.Error( $"マニフェストに存在しないファイルを検出した。Entry={entry.FullName}" );
                failed++;
                processed++;
                await UpdateDownloadProgressAsync( Math.Min( 100, (double)processed / totalFiles * 100 ) );
                continue;
            }

            var destinationPath = Path.GetFullPath(
                Path.Combine( rootFullPath, normalizedEntry.Replace( '/', Path.DirectorySeparatorChar ) )
            );
            if(!destinationPath.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
                logger.Warn( $"ZIPエントリが保存先の外を指しているためスキップする。Entry={entry.FullName}" );
                failed++;
                processed++;
                await UpdateDownloadProgressAsync( Math.Min( 100, (double)processed / totalFiles * 100 ) );
                continue;
            }

            var shouldDelete = false;
            try {
                var directoryName = Path.GetDirectoryName( destinationPath );
                if(!string.IsNullOrEmpty( directoryName )) Directory.CreateDirectory( directoryName );

                await using Stream entryStream = entry.Open();
                await using FileStream destinationStream = new(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    StreamCopyBufferSize,
                    useAsync: true
                );
                using var hash = IncrementalHash.CreateHash( HashAlgorithmName.SHA256 );
                var buffer = ArrayPool<byte>.Shared.Rent( StreamCopyBufferSize );
                long totalBytes = 0;
                try {
                    int read;
                    while((read = await entryStream.ReadAsync( buffer.AsMemory( 0, buffer.Length ) ).ConfigureAwait( false )) > 0) {
                        hash.AppendData( buffer, 0, read );
                        await destinationStream.WriteAsync( buffer.AsMemory( 0, read ) ).ConfigureAwait( false );
                        totalBytes += read;
                    }
                }
                finally {
                    ArrayPool<byte>.Shared.Return( buffer );
                }

                if(totalBytes != manifestFile.Size) {
                    failed++;
                    shouldDelete = true;
                    logger.Warn( $"マニフェストで定義されたサイズと一致しないため保存を中止する。Entry={entry.FullName}, DeclaredSize={manifestFile.Size}, ActualSize={totalBytes}" );
                }
                else {
                    var computedHash = Convert.ToHexString( hash.GetHashAndReset() );
                    if(!string.Equals( computedHash, manifestFile.Sha256, StringComparison.OrdinalIgnoreCase )) {
                        failed++;
                        shouldDelete = true;
                        logger.Warn( $"マニフェストで定義されたハッシュと一致しないため保存を中止する。Entry={entry.FullName}, DeclaredHash={manifestFile.Sha256}, ActualHash={computedHash}" );
                    }
                    else {
                        success++;
                        logger.Info( $"ファイルを保存した。Path={destinationPath}" );
                    }
                }
            }
            catch(Exception ex) {
                failed++;
                shouldDelete = true;
                logger.Error( $"ZIPエントリの展開に失敗した。Entry={entry.FullName}, Path={destinationPath}", ex );
            }
            finally {
                processed++;
                await UpdateDownloadProgressAsync( Math.Min( 100, (double)processed / totalFiles * 100 ) );
                if(shouldDelete && File.Exists( destinationPath )) {
                    try {
                        File.Delete( destinationPath );
                    }
                    catch(Exception deleteEx) {
                        logger.Warn( $"検証失敗によりファイル削除に失敗した。Path={destinationPath}", deleteEx );
                    }
                }
            }
        }

        await UpdateDownloadProgressAsync( 100 );
        if(failed > 0) {
            logger.Warn( $"ZIP展開で一部のファイル保存に失敗した。Success={success}, Failed={failed}, Total={totalFiles}" );
            await ShowSnackbarAsync( $"一部のファイルの保存に失敗しました ({failed}/{totalFiles})" );
        }
        else {
            logger.Info( $"ZIP展開が完了した。Success={success}, Total={totalFiles}" );
            await ShowSnackbarAsync( "ダウンロード完了" );
        }

        logger.Info( "ダウンロード処理が完了した。" );
        return true;
    }

    /// <summary>
    /// 翻訳ファイルを対象の miz/trk へ適用する。
    /// </summary>
    /// <param name="targetEntries">適用対象のファイル一覧。</param>
    /// <param name="rootFullPath">圧縮ファイル配置ルートの絶対パス。</param>
    /// <param name="rootWithSeparator">区切り文字付きのルート絶対パス。</param>
    /// <param name="translateFullPath">翻訳ディレクトリの絶対パス。</param>
    /// <param name="translateRootWithSeparator">区切り文字付き翻訳ルート。</param>
    /// <returns>処理が完了した場合は true。</returns>
    private async Task<bool> ProcessApplyAsync(
        List<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator
    ) {
        var repoOnlyEntries = targetEntries
            .Where( entry => entry.ChangeType == FileChangeType.RepoOnly )
            .ToList();

        if(repoOnlyEntries.Count > 0) {
            logger.Info( $"リポジトリのみのファイルを取得する。Count={repoOnlyEntries.Count}" );
            var downloadResult = await apiService.DownloadFilesAsync(
                new ApiDownloadFilesRequest( repoOnlyEntries.ConvertAll( e => e.Path ), null )
            ).ConfigureAwait( false );
            if(downloadResult.IsFailed) {
                var reason = downloadResult.Errors.Count > 0 ? downloadResult.Errors[0].Message : null;
                logger.Error( $"リポジトリからの取得に失敗した。Reason={reason}" );
                await ShowSnackbarAsync( "リポジトリからの取得に失敗しました" );
                return false;
            }

            var archive = downloadResult.Value;
            if(!archive.IsNotModified) {
                if(archive.Content.Length == 0) {
                    logger.Warn( "取得した ZIP が空のため適用を中断する。" );
                    await ShowSnackbarAsync( "取得ファイルが見つかりませんでした" );
                    return false;
                }

                var extracted = await ExtractDownloadArchiveAsync( archive, translateFullPath ).ConfigureAwait( false );
                if(!extracted) {
                    await ShowSnackbarAsync( "リポジトリファイルの保存に失敗しました" );
                    return false;
                }
            }

            foreach(var repoEntry in repoOnlyEntries) {
                if(!TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, repoEntry.Path, out var repoPath ) || !File.Exists( repoPath )) {
                    logger.Warn( $"取得後もファイルが存在しない。Path={repoEntry.Path}, Resolved={repoPath}" );
                    await ShowSnackbarAsync( $"取得失敗: {repoEntry.Path}" );
                    return false;
                }
            }
        }

        var progressed = 0;
        var totalApplied = targetEntries.Count;
        var success = 0;
        var failed = 0;

        if(totalApplied == 0) {
            await UpdateApplyProgressAsync( 100 );
            await ShowSnackbarAsync( "適用完了 成功:0 件 失敗:0 件" );
            logger.Info( "適用処理が完了した。成功=0, 失敗=0" );
            return true;
        }

        foreach(var entry in targetEntries) {
            try {
                string? snackbarMessage = null;

                if(!TryResolvePathWithinRoot( translateFullPath, translateRootWithSeparator, entry.Path, out var sourceFilePath )) {
                    failed++;
                    snackbarMessage = $"不正な翻訳ファイル: {entry.Path}";
                    logger.Warn( $"翻訳ディレクトリ外のファイルが指定されたためスキップする。Path={entry.Path}" );
                }
                else if(!File.Exists( sourceFilePath )) {
                    failed++;
                    snackbarMessage = $"翻訳ファイルが見つかりません: {entry.Path}";
                    logger.Warn( $"翻訳ファイルが存在しないため適用できない。Path={sourceFilePath}" );
                }
                else {
                    var parts = entry.Path.Split( '/', StringSplitOptions.RemoveEmptyEntries );
                    var archiveIndex = Array.FindIndex( parts, IsZipLikeEntrySegment );
                    var rootSkipCount = GetRootSegmentSkipCount( parts );

                    if(archiveIndex == -1) {
                        var relativeSegments = parts.Skip( rootSkipCount ).ToArray();
                        if(relativeSegments.Length == 0) {
                            failed++;
                            snackbarMessage = $"不正なパス構造: {entry.Path}";
                            logger.Warn( $"ZIP対象拡張子を含まないエントリの相対パスが空のため適用できない。Path={entry.Path}" );
                        }
                        else {
                            var relativePath = string.Join( '/', relativeSegments );
                            if(!TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, relativePath, out var destinationPath )) {
                                failed++;
                                snackbarMessage = $"不正な適用先: {entry.Path}";
                                logger.Warn( $"ZIP対象拡張子を含まないエントリがルート外を指しているため拒否した。Entry={entry.Path}, Relative={relativePath}" );
                            }
                            else {
                                try {
                                    var directoryName = Path.GetDirectoryName( destinationPath );
                                    if(!string.IsNullOrEmpty( directoryName )) Directory.CreateDirectory( directoryName );
                                    File.Copy( sourceFilePath, destinationPath, overwrite: true );
                                    logger.Info( $"ZIP対象拡張子を含まないエントリを直接保存した。Destination={destinationPath}" );
                                    success++;
                                }
                                catch(Exception copyEx) {
                                    failed++;
                                    snackbarMessage = $"適用失敗: {entry.Path}";
                                    logger.Error( $"ZIP対象拡張子を含まないエントリの保存に失敗した。Entry={entry.Path}, Destination={destinationPath}", copyEx );
                                }
                            }
                        }
                    }
                    else {
                        var archiveSegments = parts.Take( archiveIndex + 1 ).Skip( rootSkipCount ).ToArray();
                        if(archiveSegments.Length == 0) {
                            failed++;
                            snackbarMessage = $"不正なパス構造: {entry.Path}";
                            logger.Warn( $"パス構造が不正のため適用に失敗した。Path={entry.Path}" );
                        }
                        else {
                            var archiveRelativePath = string.Join( '/', archiveSegments );
                            if(!TryResolvePathWithinRoot( rootFullPath, rootWithSeparator, archiveRelativePath, out var archivePath )) {
                                failed++;
                                snackbarMessage = $"不正な適用先: {entry.Path}";
                                logger.Warn( $"適用先がルート外を指しているため拒否した。Entry={entry.Path}, ArchiveRelative={archiveRelativePath}" );
                            }
                            else if(!File.Exists( archivePath )) {
                                failed++;
                                snackbarMessage = $"圧縮ファイルが存在しません: {entry.Path}";
                                logger.Warn( $"適用先の圧縮ファイルが存在しない。ArchivePath={archivePath}" );
                            }
                            else {
                                var entryPathSegments = parts.Skip( archiveIndex + 1 ).ToArray();
                                if(entryPathSegments.Length == 0) {
                                    failed++;
                                    snackbarMessage = $"圧縮ファイル内パスが不正です: {entry.Path}";
                                    logger.Warn( $"miz/trk 内のパスが空のため適用できない。Path={entry.Path}" );
                                }
                                else {
                                    var entryPath = string.Join( '/', entryPathSegments );
                                    var addResult = zipService.AddEntry( archivePath, entryPath, sourceFilePath );
                                    if(addResult.IsFailed) {
                                        failed++;
                                        snackbarMessage = $"適用失敗: {entry.Path}";
                                        var reason = string.Join( ", ", addResult.Errors.Select( e => e.Message ) );
                                        logger.Warn( $"圧縮ファイルへの適用に失敗した。ArchivePath={archivePath}, EntryPath={entryPath}, Reason={reason}" );
                                    }
                                    else {
                                        logger.Info( $"圧縮ファイルへ適用した。ArchivePath={archivePath}, EntryPath={entryPath}" );
                                        success++;
                                    }
                                }
                            }
                        }
                    }
                }

                if(snackbarMessage is not null) {
                    await ShowSnackbarAsync( snackbarMessage );
                }
            }
            catch(Exception ex) {
                failed++;
                logger.Error( $"適用処理で例外が発生した。Path={entry.Path}", ex );
                await ShowSnackbarAsync( $"適用失敗: {entry.Path}" );
            }

            progressed++;
            await UpdateApplyProgressAsync( Math.Min( 100, (double)progressed / totalApplied * 100 ) );
        }

        await UpdateApplyProgressAsync( 100 );
        await ShowSnackbarAsync( $"適用完了 成功:{success} 件 失敗:{failed} 件" );
        logger.Info( $"適用処理が完了した。成功={success}, 失敗={failed}" );
        return true;
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

    /// <summary>ZIPアーカイブを指定ディレクトリへ展開する。</summary>
    private async Task<bool> ExtractDownloadArchiveAsync( ApiDownloadFilesResult archive, string destinationRootFullPath ) {
        logger.Debug( $"ZIPアーカイブを展開する。Destination={destinationRootFullPath}" );
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

            if(string.Equals( entry.Name, ManifestFileName, StringComparison.OrdinalIgnoreCase )) {
                logger.Info( $"manifest.json を展開対象から除外する。Entry={entry.FullName}" );
                continue;
            }

            if(!TryResolvePathWithinRoot( destinationRootFullPath, rootWithSeparator, normalizedEntry, out var destinationPath )) {
                hasFailure = true;
                logger.Warn( $"ZIPエントリが保存先の外を指しているためスキップする。Entry={entry.FullName}" );
                continue;
            }

            var shouldDelete = false;
            try {
                var directoryName = Path.GetDirectoryName( destinationPath );
                if(!string.IsNullOrEmpty( directoryName )) Directory.CreateDirectory( directoryName );

                await using Stream entryStream = entry.Open();
                await using FileStream destinationStream = new(
                    destinationPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    StreamCopyBufferSize,
                    useAsync: true
                );
                var buffer = ArrayPool<byte>.Shared.Rent( StreamCopyBufferSize );
                try {
                    int read;
                    while((read = await entryStream.ReadAsync( buffer.AsMemory( 0, buffer.Length ) ).ConfigureAwait( false )) > 0) {
                        await destinationStream.WriteAsync( buffer.AsMemory( 0, read ) ).ConfigureAwait( false );
                    }
                }
                finally {
                    ArrayPool<byte>.Shared.Return( buffer );
                }

                logger.Info( $"ZIPエントリを展開した。Path={destinationPath}" );
            }
            catch(Exception ex) {
                hasFailure = true;
                shouldDelete = true;
                logger.Error( $"ZIPエントリの展開に失敗した。Entry={entry.FullName}, Path={destinationPath}", ex );
            }
            finally {
                if(shouldDelete && File.Exists( destinationPath )) {
                    try {
                        File.Delete( destinationPath );
                    }
                    catch(Exception deleteEx) {
                        logger.Warn( $"ZIPエントリ展開失敗後のクリーンアップに失敗した。Path={destinationPath}", deleteEx );
                    }
                }
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

    /// <summary>パス先頭のルートセグメントを何個スキップするかを取得する。</summary>
    private static int GetRootSegmentSkipCount( string[] segments ) {
        if(segments.Length == 0) return 0;

        if(string.Equals( segments[0], "DCSWorld", StringComparison.OrdinalIgnoreCase )) {
            if(segments.Length >= 3 && string.Equals( segments[1], "Mods", StringComparison.OrdinalIgnoreCase )) {
                return 3;
            }
        }

        if(string.Equals( segments[0], "UserMissions", StringComparison.OrdinalIgnoreCase )) {
            return 1;
        }

        return 0;
    }

    /// <summary>ZIPとして扱う拡張子を含むかを判定する。</summary>
    /// <param name="segment">判定対象のパスセグメント。</param>
    /// <returns>ZIPとして扱う拡張子なら true。</returns>
    private static bool IsZipLikeEntrySegment( string segment ) =>
        ZipLikeExtensions.Any( ext => segment.EndsWith( ext, StringComparison.OrdinalIgnoreCase ) );

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

    /// <summary>
    /// ZIP 内のパスを正規化する。
    /// </summary>
    /// <param name="path">正規化対象のパス</param>
    /// <returns>正規化後のパス</returns>
    private static string NormalizeArchivePath( string path ) {
        if(string.IsNullOrEmpty( path )) return string.Empty;
        return path.Replace( '\\', '/' ).TrimStart( '/' );
    }

    /// <summary>
    /// SHA256 ハッシュ文字列の妥当性を検証する。
    /// </summary>
    /// <param name="value">検証対象の文字列</param>
    /// <returns>妥当であるかどうか</returns>
    private static bool IsValidSha256( string value ) {
        if(string.IsNullOrWhiteSpace( value ) || value.Length != 64) return false;
        foreach(char c in value) {
            var isHexDigit = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if(!isHexDigit) return false;
        }
        return true;
    }

    /// <summary>
    /// ダウンロードマニフェスト情報を表現する。
    /// </summary>
    private sealed record DownloadManifest(
        [property: JsonPropertyName( "version" )] int Version,
        [property: JsonPropertyName( "generatedAt" )] string GeneratedAt,
        [property: JsonPropertyName( "files" )] IReadOnlyList<DownloadManifestFile>? Files
    );

    /// <summary>
    /// マニフェスト内のファイル情報を表現する。
    /// </summary>
    private sealed record DownloadManifestFile(
        [property: JsonPropertyName( "path" )] string Path,
        [property: JsonPropertyName( "size" )] long Size,
        [property: JsonPropertyName( "sha256" )] string Sha256
    );

    #endregion
}