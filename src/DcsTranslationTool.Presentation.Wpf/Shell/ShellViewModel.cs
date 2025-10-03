using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Shared.Constants;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Shell;

public class ShellViewModel(
    IAppSettingsService appSettingsService,
    IEventAggregator eventAggregator,
    ILoggingService logger,
    INavigationService navigationService,
    ISnackbarService snackbarService,
    ISystemService systemService
) : Conductor<IScreen>.Collection.OneActive, IHandle<string>, IActivate {

    #region Fields

    private bool _initialized = false;
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
        if(string.IsNullOrEmpty( appSettingsService.Settings.SourceAircraftDir ) ||
            string.IsNullOrEmpty( appSettingsService.Settings.SourceDlcCampaignDir ) ||
            string.IsNullOrEmpty( appSettingsService.Settings.TranslateFileDir )) {
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
}