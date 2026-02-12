using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Common;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;

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
            var request = new DownloadExecutionRequest(
                GetSelectedTab(),
                saveRootPath
            );
            var result = await downloadWorkflowService.ExecuteDownloadAsync(
                request,
                UiAdapter.UpdateDownloadProgressAsync
            );
            await HandleWorkflowEventsAsync( result.Events );
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
            var request = new ApplyExecutionRequest(
                GetSelectedTab(),
                appSettingsService.Settings.SourceAircraftDir,
                appSettingsService.Settings.SourceDlcCampaignDir,
                appSettingsService.Settings.SourceUserMissionDir,
                appSettingsService.Settings.TranslateFileDir
            );
            var result = await downloadWorkflowService.ExecuteApplyAsync(
                request,
                UiAdapter.ShowSnackbarAsync,
                UiAdapter.UpdateApplyProgressAsync
            );
            await HandleWorkflowEventsAsync( result.Events );
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
    /// 選択中タブを取得する。
    /// </summary>
    /// <returns>選択中タブ。未選択時は <see langword="null"/>。</returns>
    private TabItemViewModel? GetSelectedTab() =>
        Tabs.Count > 0 && SelectedTabIndex >= 0 && SelectedTabIndex < Tabs.Count
            ? Tabs[SelectedTabIndex]
            : null;

    /// <summary>
    /// ワークフローイベントを UI へ反映する。
    /// </summary>
    /// <param name="events">反映対象イベント。</param>
    /// <returns>非同期タスク。</returns>
    private async Task HandleWorkflowEventsAsync( IReadOnlyList<WorkflowEvent> events ) {
        foreach(var workflowEvent in events) {
            switch(workflowEvent.Kind) {
                case WorkflowEventKind.Notification when !string.IsNullOrWhiteSpace( workflowEvent.Message ):
                    await UiAdapter.ShowSnackbarAsync( workflowEvent.Message );
                    break;
                case WorkflowEventKind.DownloadProgress when workflowEvent.Progress.HasValue:
                    await UiAdapter.UpdateDownloadProgressAsync( workflowEvent.Progress.Value );
                    break;
                case WorkflowEventKind.ApplyProgress when workflowEvent.Progress.HasValue:
                    await UiAdapter.UpdateApplyProgressAsync( workflowEvent.Progress.Value );
                    break;
            }
        }
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