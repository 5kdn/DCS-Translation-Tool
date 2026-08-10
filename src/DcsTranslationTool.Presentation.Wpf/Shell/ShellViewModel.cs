using System.Windows.Navigation;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;
using DcsTranslationTool.Shared.Constants;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Shell;

public class ShellViewModel(
    IAppSettingsService appSettingsService,
    IEventAggregator eventAggregator,
    ILoggingService logger,
    INavigationService navigationService,
    ISnackbarService snackbarService,
    ISystemService systemService,
    IUpdateCheckService updateCheckService,
    IApplicationUpdateService? applicationUpdateService = null,
    IDialogService? dialogService = null,
    IApplicationRestartCoordinator? applicationRestartCoordinator = null
) : Conductor<IScreen>.Collection.OneActive, IHandle<string>, IActivate {

    #region Fields

    private bool _initialized = false;
    private bool _startupUpdateCheckStarted = false;
    private bool _applicationUpdateInProgress = false;
    private double _shellWidth = appSettingsService.Settings.ShellWidth;
    private double _shellHeight = appSettingsService.Settings.ShellHeight;

    #endregion

    #region Properties

    /// <summary>
    /// シェルウィンドウの幅
    /// </summary>
    public double ShellWidth {
        get => _shellWidth;
        set {
            _shellWidth = value;
            appSettingsService.Settings.ShellWidth = value;
            NotifyOfPropertyChange( nameof( ShellWidth ) );
        }
    }

    /// <summary>
    /// シェルウィンドウの高さ
    /// </summary>
    public double ShellHeight {
        get => _shellHeight;
        set {
            _shellHeight = value;
            appSettingsService.Settings.ShellHeight = value;
            NotifyOfPropertyChange( nameof( ShellHeight ) );
        }
    }

    public bool CanGoBack => navigationService.CanGoBack;

    public bool CanGoForward => navigationService.CanGoForward;

    /// <summary>Snackbar のメッセージキュー</summary>
    public ISnackbarMessageQueue MessageQueue => snackbarService.MessageQueue;

    #endregion

    #region Lifecycle

    protected override void OnViewLoaded( object view ) {
        base.OnViewLoaded( view );
        logger.Info( $"シェルビューを読み込んだ。ViewType={view.GetType().Name}" );
    }

    public Task HandleAsync( string message, CancellationToken cancellationToken ) {
        logger.Info( $"イベントを受信した。Message={message}" );
        return Task.CompletedTask;
    }

    public async Task ActivateAsync( CancellationToken cancellationToken ) {
        logger.Info( $"ShellViewModel をアクティブ化する。初期化済み={_initialized}" );
        eventAggregator.SubscribeOnUIThread( this );

        // Navigation 状態変化で CanGoBack/CanGoForward を更新
        navigationService.Navigating += OnNavigating;
        navigationService.Navigated += OnNavigated;

        if(!_initialized) {
            navigationService.NavigateToViewModel<MainViewModel>();
            _ = ExecuteStartupUpdateCheckAsync( CancellationToken.None );
            _initialized = true;
        }
        await Task.CompletedTask;
    }

    protected override Task OnDeactivateAsync( bool close, CancellationToken cancellationToken ) {
        logger.Info( $"ShellViewModel を非アクティブ化する。Close={close}" );
        eventAggregator.Unsubscribe( this );
        navigationService.Navigating -= OnNavigating;
        navigationService.Navigated -= OnNavigated;
        return base.OnDeactivateAsync( close, cancellationToken );
    }

    #endregion

    #region Action Messages

    public void GoBack() {
        logger.Info( $"前のページへ戻る。CanGoBack={navigationService.CanGoBack}" );
        navigationService.GoBack();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    public void GoForward() {
        logger.Info( $"次のページへ進む。CanGoForward={navigationService.CanGoForward}" );
        navigationService.GoForward();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    public void NavToSettings() {
        logger.Info( "SettingsViewModel へ遷移する。" );
        navigationService.For<SettingsViewModel>().Navigate();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    public void BrowseTranslationRepository() {
        logger.Info( $"翻訳リポジトリをブラウザで開く。Url={TargetRepository.Url}" );
        systemService.OpenInWebBrowser( TargetRepository.Url );
    }

    #endregion

    #region Navigation event handlers

    private void OnNavigating( object? s, NavigatingCancelEventArgs e ) {
        logger.Info( $"ナビゲーションを開始した。Uri={e.Uri}" );
        // 直前でも有効/無効が変わるケースに備えて更新
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    private void OnNavigated( object? s, NavigationEventArgs e ) {
        logger.Info( $"ナビゲーションが完了した。Content={e.Content?.GetType().Name ?? "null"}" );
        // スタック確定後に更新
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );

        ValidateAppSettingsAndNotify();
    }

    #endregion

    /// <summary>最低限のアプリ設定を検証し、不足時に通知する</summary>
    private void ValidateAppSettingsAndNotify() {
        var settings = appSettingsService.Settings;
        var hasMissingRequiredSetting =
            string.IsNullOrEmpty( settings.DcsWorldInstallDir ) ||
            string.IsNullOrEmpty( settings.TranslateFileDir ) ||
            (settings.UseExternalAircraftInjectionDir && string.IsNullOrEmpty( settings.ExternalAircraftInjectionDir )) ||
            (settings.UseExternalCampaignInjectionDir && string.IsNullOrEmpty( settings.ExternalCampaignInjectionDir ));

        if(hasMissingRequiredSetting) {
            logger.Warn( "アプリ設定に不足がある" );
            snackbarService.Show(
                "設定が不足しています。",
                "設定",
                NavToSettings,
                TimeSpan.FromSeconds( 3 ) );
        }
        else {
            logger.Info( "アプリ設定が検証済み。全ての必須ディレクトリが設定済み。" );
        }
    }

    /// <summary>
    /// 起動時に 1 回だけ更新確認を実行し、更新を検知したときに通知する。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知を受け取る。</param>
    private async Task ExecuteStartupUpdateCheckAsync( CancellationToken cancellationToken ) {
        if(this._startupUpdateCheckStarted) {
            return;
        }

        this._startupUpdateCheckStarted = true;

        try {
            if(applicationUpdateService is not null) {
                var pendingResult = applicationUpdateService.GetPendingUpdate();
                if(pendingResult.IsSuccess && pendingResult.Value is not null) {
                    await ShowPendingUpdateNotificationAsync( pendingResult.Value ).ConfigureAwait( false );
                    return;
                }
            }

            var updateCheckResult = await updateCheckService.CheckForUpdateAsync( cancellationToken ).ConfigureAwait( false );
            if(!updateCheckResult.IsUpdateAvailable ||
                string.IsNullOrWhiteSpace( updateCheckResult.LatestVersionLabel ) ||
                string.IsNullOrWhiteSpace( updateCheckResult.ReleaseUrl )) {
                return;
            }

            await Execute.OnUIThreadAsync( () => {
                snackbarService.Show(
                    string.Format( Strings_Shared.UpdateAvailableMessageFormat, updateCheckResult.LatestVersionLabel ),
                    applicationUpdateService is null || dialogService is null ? Strings_Shared.UpdateActionOpenRelease : GetResourceString( "ApplicationUpdateAction" ),
                    applicationUpdateService is null || dialogService is null
                        ? () => systemService.OpenInWebBrowser( updateCheckResult.ReleaseUrl )
                        : () => _ = ExecuteApplicationUpdateAsync( updateCheckResult ),
                    duration: TimeSpan.FromSeconds( 4 ) );
                return Task.CompletedTask;
            } ).ConfigureAwait( false );
        }
        catch(Exception ex) {
            logger.Warn( "起動時更新確認でエラーが発生したため通知を行わない。", ex );
        }
    }

    private async Task ExecuteApplicationUpdateAsync( UpdateCheckResult update ) {
        if(this._applicationUpdateInProgress || applicationUpdateService is null || dialogService is null || update.PreferredAsset is null) {
            return;
        }

        this._applicationUpdateInProgress = true;
        try {
            var asset = update.PreferredAsset;
            var assetKind = asset.Kind == ApplicationUpdateAssetKind.Delta
                ? GetResourceString( "ApplicationUpdateDeltaLabel" )
                : GetResourceString( "ApplicationUpdateFullLabel" );
            var confirmResult = await dialogService.ConfirmationDialogShowAsync( new ConfirmationDialogParameters
            {
                AutomationId = "ApplicationUpdateDownloadConfirmationDialog",
                Title = GetResourceString( "ApplicationUpdateDialogTitle" ),
                Message = string.Format( GetResourceString( "ApplicationUpdateDownloadMessageFormat" ), update.LatestVersionLabel, assetKind, FormatFileSize( asset.Size ) ),
                ConfirmButtonText = GetResourceString( "ApplicationUpdateDownloadButton" ),
                CancelButtonText = GetResourceString( "ApplicationUpdateCancelButton" )
            } );
            if(confirmResult != ConfirmationDialogResult.Confirm) {
                return;
            }

            var stageResult = await dialogService.ApplicationUpdateProgressDialogShowAsync(
                new ApplicationUpdateProgressDialogParameters(
                    GetResourceString( "ApplicationUpdateProgressTitle" ),
                    (progress, cancellationToken) => applicationUpdateService.DownloadAndStageAsync( update, progress, cancellationToken ),
                    logger ) );
            if(stageResult.IsFailed) {
                await HandleUpdateFailureAsync( update );
                return;
            }

            await ConfirmRestartAsync( stageResult.Value );
        }
        catch(Exception ex) {
            logger.Error( "アプリケーション更新の操作に失敗した。", ex );
            snackbarService.Show( GetResourceString( "ApplicationUpdatePrepareFailureMessage" ) );
        }
        finally {
            this._applicationUpdateInProgress = false;
        }
    }

    private async Task ConfirmRestartAsync( ApplicationUpdateStageResult stageResult ) {
        if(applicationUpdateService is null || dialogService is null) {
            return;
        }

        var restartResult = await dialogService.ConfirmationDialogShowAsync( new ConfirmationDialogParameters
        {
            AutomationId = "ApplicationUpdateRestartConfirmationDialog",
            Title = GetResourceString( "ApplicationUpdateReadyTitle" ),
            Message = string.Format( GetResourceString( "ApplicationUpdateRestartMessageFormat" ), stageResult.TargetVersion.ToString( 3 ) ),
            ConfirmButtonText = GetResourceString( "ApplicationUpdateRestartNowButton" ),
            SecondaryButtonText = GetResourceString( "ApplicationUpdateLaterButton" ),
            CancelButtonText = GetResourceString( "ApplicationUpdateCancelButton" )
        } );
        if(restartResult != ConfirmationDialogResult.Confirm) {
            return;
        }

        if(!await ((IGuardClose)this).CanCloseAsync( CancellationToken.None )) {
            logger.Warn( "シェルの終了確認により更新再起動を中止した。" );
            snackbarService.Show( GetResourceString( "ApplicationUpdateUnsavedMessage" ) );
            return;
        }

        if(applicationRestartCoordinator is not null &&
            !await applicationRestartCoordinator.PrepareForRestartAsync( CancellationToken.None )) {
            logger.Warn( "編集中ウィンドウの終了確認により更新再起動を中止した。" );
            snackbarService.Show( GetResourceString( "ApplicationUpdateUnsavedMessage" ) );
            return;
        }

        var launchResult = applicationUpdateService.LaunchApplyProcess( stageResult, Environment.ProcessId );
        if(launchResult.IsFailed) {
            logger.Error( $"更新適用プロセスを開始できなかった。Errors={string.Join( "; ", launchResult.Errors.Select( error => error.Message ) )}" );
            snackbarService.Show( GetResourceString( "ApplicationUpdateRestartFailureMessage" ) );
            return;
        }

        logger.Info( "更新適用プロセスを開始したためアプリケーションを終了する。" );
        _ = System.Windows.Application.Current.Dispatcher.BeginInvoke(
            System.Windows.Application.Current.Shutdown,
            DispatcherPriority.ApplicationIdle );
    }

    private async Task HandleUpdateFailureAsync( UpdateCheckResult update ) {
        if(dialogService is null || string.IsNullOrWhiteSpace( update.ReleaseUrl )) {
            return;
        }

        var failureResult = await dialogService.ConfirmationDialogShowAsync( new ConfirmationDialogParameters
        {
            AutomationId = "ApplicationUpdateFailureConfirmationDialog",
            Title = GetResourceString( "ApplicationUpdateFailureTitle" ),
            Message = GetResourceString( "ApplicationUpdateFailureMessage" ),
            ConfirmButtonText = GetResourceString( "ApplicationUpdateRetryButton" ),
            SecondaryButtonText = GetResourceString( "ApplicationUpdateReleasePageButton" ),
            CancelButtonText = GetResourceString( "ApplicationUpdateCloseButton" )
        } );
        if(failureResult == ConfirmationDialogResult.Confirm) {
            this._applicationUpdateInProgress = false;
            await ExecuteApplicationUpdateAsync( update );
        }
        else if(failureResult == ConfirmationDialogResult.Secondary) {
            systemService.OpenInWebBrowser( update.ReleaseUrl );
        }
    }

    private Task ShowPendingUpdateNotificationAsync( ApplicationUpdateStageResult stageResult ) =>
        Execute.OnUIThreadAsync( () => {
            snackbarService.Show(
                string.Format( GetResourceString( "ApplicationUpdatePendingMessageFormat" ), stageResult.TargetVersion.ToString( 3 ) ),
                GetResourceString( "ApplicationUpdateRestartAction" ),
                () => _ = ConfirmRestartAsync( stageResult ),
                duration: TimeSpan.FromSeconds( 6 ) );
            return Task.CompletedTask;
        } );

    private static string FormatFileSize( long bytes ) => bytes switch
    {
        >= 1024 * 1024 => $"{bytes / 1024d / 1024d:N1} MB",
        >= 1024 => $"{bytes / 1024d:N1} KB",
        _ => $"{bytes:N0} bytes"
    };

    private static string GetResourceString( string name ) =>
        Strings_Shared.ResourceManager.GetString( name ) ?? name;
}