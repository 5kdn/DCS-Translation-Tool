using System.IO;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Common;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Features.Download;

/// <summary>
/// ダウンロードページの状態を管理する ViewModel
/// </summary>
public class DownloadViewModel(
    IApiService apiService,
    IAppSettingsService appSettingsService,
    IDownloadWorkflowService downloadWorkflowService,
    IDispatcherService dispatcherService,
    IFileEntryService fileEntryService,
    IFileEntryWatcherLifecycle fileEntryWatcherLifecycle,
    IFileEntryTreeService fileEntryTreeService,
    ILoggingService logger,
    ISnackbarService snackbarService,
    ISystemService systemService
) : FileEntryTabsViewModelBase(
    apiService,
    dispatcherService,
    fileEntryService,
    fileEntryWatcherLifecycle,
    fileEntryTreeService,
    logger,
    snackbarService
) {

    #region Fields

    private bool _isDownloading;
    private double _downloadedProgress = 0.0;
    private bool _isApplying;
    private double _appliedProgress = 0.0;
    private bool _suppressEntriesChanged;
    private DownloadWorkflowUiAdapter? _uiAdapter;

    /// <summary>
    /// Download 画面向けの UI 更新アダプタを取得する。
    /// </summary>
    private DownloadWorkflowUiAdapter UiAdapter =>
        _uiAdapter ??= new(
            DispatcherService,
            SnackbarService,
            value => DownloadedProgress = value,
            value => AppliedProgress = value
        );


    #endregion

    #region Properties

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
    public bool CanDownload => !IsDownloading && HasCheckedEntries();

    /// <summary>
    /// 適用可能か
    /// </summary>
    public bool CanApply => !IsApplying && HasCheckedEntries();

    #endregion

    #region Actions

    /// <summary>
    /// チェック状態のファイルをダウンロードする
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Download() {
        Logger.Info( "ダウンロード処理を開始する。" );
        if(!CanDownload) {
            Logger.Warn( "ダウンロードは現在許可されていないため処理を中断する。" );
            return;
        }

        var saveRootPath = appSettingsService.Settings.TranslateFileDir;
        if(string.IsNullOrWhiteSpace( saveRootPath )) {
            Logger.Warn( "保存先ディレクトリが設定されていないため保存を中断する。" );
            await UiAdapter.ShowSnackbarAsync( "保存先フォルダーが設定されていません" );
            return;
        }

        await ExecuteWithEntriesChangedSuppressedAsync( isDownload: true, async () => {
            if(Tabs.Count == 0 || SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                Logger.Warn( "タブが選択されていないためダウンロードを中断する。" );
                await UiAdapter.ShowSnackbarAsync( "タブが選択されていません" );
                return;
            }

            var tab = Tabs[SelectedTabIndex];
            var checkedEntries = tab.GetCheckedEntries();
            if(checkedEntries is null) {
                Logger.Warn( "チェックされたエントリが存在しない。" );
                await UiAdapter.ShowSnackbarAsync( "ダウンロード対象が有りません" );
                return;
            }

            var targetEntries = checkedEntries.Where( e => !e.IsDirectory ).ToList();
            if(targetEntries.Count == 0) {
                Logger.Warn( "ダウンロード対象のファイルが存在しない。" );
                await UiAdapter.ShowSnackbarAsync( "ダウンロード対象が有りません" );
                return;
            }
            Logger.Info( $"ダウンロード対象を特定した。件数={targetEntries.Count}" );

            IReadOnlyList<string> paths = targetEntries.ConvertAll( e => e.Path );
            var pathResult = await ApiService.DownloadFilePathsAsync(
                new ApiDownloadFilePathsRequest( paths, null )
            );
            if(pathResult.IsFailed) {
                var reason = pathResult.Errors.Count > 0 ? pathResult.Errors[0].Message : null;
                var message = ResultNotificationPolicy.GetDownloadPathFailureMessage( pathResult.GetFirstErrorKind() );
                Logger.Error( $"ダウンロードURLの取得に失敗した。Reason={reason}" );
                await UiAdapter.ShowSnackbarAsync( message );
                return;
            }

            var downloadItems = pathResult.Value.Items.ToArray();
            Logger.Info( $"ダウンロードURLを取得した。{downloadItems.Length}件" );

            if(downloadItems.Length == 0) {
                Logger.Info( "ダウンロード対象が最新のため保存をスキップする。" );
                await UiAdapter.ShowSnackbarAsync( "対象ファイルは最新です" );
                return;
            }

            await downloadWorkflowService.DownloadFilesAsync( downloadItems, saveRootPath, UiAdapter.UpdateDownloadProgressAsync );
        } );
    }

    /// <summary>
    /// 選択されたファイルを miz/trk に適用する
    /// </summary>
    /// <returns>非同期タスク</returns>
    public async Task Apply() {
        Logger.Info( "適用処理を開始する。" );
        if(!CanApply) {
            Logger.Warn( "適用は現在許可されていないため処理を中断する。" );
            return;
        }

        await ExecuteWithEntriesChangedSuppressedAsync( isDownload: false, async () => {
            if(Tabs.Count == 0 || SelectedTabIndex < 0 || SelectedTabIndex >= Tabs.Count) {
                Logger.Warn( "タブが選択されていないため適用処理を中断する。" );
                await UiAdapter.ShowSnackbarAsync( "タブが選択されていません" );
                return;
            }

            var tab = Tabs[SelectedTabIndex];
            var targetEntries = GetTargetFileNodes().Where( e => !e.IsDirectory ).ToList();

            if(targetEntries.Count == 0) {
                Logger.Warn( "適用対象のファイルが存在しない。" );
                await UiAdapter.ShowSnackbarAsync( "対象が有りません" );
                return;
            }
            Logger.Info( $"適用対象を特定した。件数={targetEntries.Count}" );

            string rootPath = tab.TabType switch
            {
                CategoryType.Aircraft => appSettingsService.Settings.SourceAircraftDir,
                CategoryType.DlcCampaigns => appSettingsService.Settings.SourceDlcCampaignDir,
                CategoryType.UserMissions => appSettingsService.Settings.SourceUserMissionDir,
                _ => throw new InvalidOperationException( $"未対応のタブ種別: {tab.TabType}" ),
            };

            if(string.IsNullOrWhiteSpace( rootPath )) {
                Logger.Warn( "適用先ディレクトリが設定されていないため処理を中断する。" );
                await UiAdapter.ShowSnackbarAsync( "適用先ディレクトリを設定してください" );
                return;
            }

            var rootFullPath = Path.GetFullPath( rootPath );
            if(!Directory.Exists( rootFullPath )) {
                Logger.Warn( $"適用先ディレクトリが存在しない。Directory={rootFullPath}" );
                await UiAdapter.ShowSnackbarAsync( "適用先ディレクトリが存在しません" );
                return;
            }
            var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? rootFullPath
                : rootFullPath + Path.DirectorySeparatorChar;

            var translateRoot = appSettingsService.Settings.TranslateFileDir;
            if(string.IsNullOrWhiteSpace( translateRoot )) {
                Logger.Warn( "翻訳ディレクトリが未設定のため処理を中断する。" );
                await UiAdapter.ShowSnackbarAsync( "翻訳ディレクトリを設定してください" );
                return;
            }

            var translateFullPath = Path.GetFullPath( translateRoot );
            Directory.CreateDirectory( translateFullPath );
            var translateRootWithSeparator = translateFullPath.EndsWith( Path.DirectorySeparatorChar )
                ? translateFullPath
                : translateFullPath + Path.DirectorySeparatorChar;

            var applyCompleted = await downloadWorkflowService.ApplyAsync(
                targetEntries,
                rootFullPath,
                rootWithSeparator,
                translateFullPath,
                translateRootWithSeparator,
                UiAdapter.ShowSnackbarAsync,
                UiAdapter.UpdateApplyProgressAsync
            );
            if(!applyCompleted) return;
        } );
    }

    /// <summary>
    /// 翻訳ファイルの管理ディレクトリをエクスプローラーで開く
    /// </summary>
    public void OpenDirectory() {
        Logger.Info( $"翻訳ファイルディレクトリを開く。Directory={appSettingsService.Settings.TranslateFileDir}" );
        systemService.OpenDirectory( appSettingsService.Settings.TranslateFileDir );
    }

    /// <summary>
    /// EntriesChanged を抑止してワークフロー処理を実行する。
    /// </summary>
    /// <param name="isDownload">ダウンロード処理かどうか。</param>
    /// <param name="action">実行処理。</param>
    /// <returns>非同期タスク。</returns>
    private async Task ExecuteWithEntriesChangedSuppressedAsync( bool isDownload, Func<Task> action ) {
        _suppressEntriesChanged = true;
        SuspendEntriesChangedSubscription();

        if(isDownload) {
            IsDownloading = true;
            await UiAdapter.UpdateDownloadProgressAsync( 0 );
        }
        else {
            IsApplying = true;
            await UiAdapter.UpdateApplyProgressAsync( 0 );
        }

        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );

        try {
            await action();
        }
        finally {
            if(isDownload) {
                IsDownloading = false;
            }
            else {
                IsApplying = false;
            }

            _suppressEntriesChanged = false;
            ResumeEntriesChangedSubscription();

            await RefreshLocalEntriesAsync();
            NotifyOfPropertyChange( nameof( CanDownload ) );
            NotifyOfPropertyChange( nameof( CanApply ) );
            Logger.Info( $"{(isDownload ? "ダウンロード" : "適用")}処理を終了した。" );
        }
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

    /// <inheritdoc />
    protected override string ViewModelName => nameof( DownloadViewModel );

    /// <inheritdoc />
    protected override ChangeTypeMode TreeMode => ChangeTypeMode.Download;

    /// <inheritdoc />
    protected override bool ShouldIgnoreEntriesChanged() {
        if(!_suppressEntriesChanged) {
            return false;
        }

        Logger.Info( "ダウンロード中のため EntriesChanged を即時破棄する。" );
        return true;
    }

    /// <inheritdoc />
    protected override void NotifyGuardProperties() {
        NotifyOfPropertyChange( nameof( CanDownload ) );
        NotifyOfPropertyChange( nameof( CanApply ) );
    }

    /// <inheritdoc />
    protected override void OnSelectedTabIndexChanged() =>
        NotifyGuardProperties();

    /// <inheritdoc />
    protected override bool IsTreeInteractionEnabledCore() =>
        !IsFetching && !IsDownloading && !IsApplying;

    #endregion
}