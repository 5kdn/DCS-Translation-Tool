using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Main;

namespace DcsTranslationTool.Presentation.Wpf.Shell;

public class ShellViewModel(
    IEventAggregator eventAggregator,
    ILoggingService logger,
    INavigationService navigationService
) : Conductor<IScreen>.Collection.OneActive, IHandle<string>, IActivate {

    #region Fields

    private bool _initialized = false;

    #endregion

    #region Properties

    public bool CanGoBack => navigationService.CanGoBack;
    public bool CanGoForward => navigationService.CanGoForward;

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
    }

    #endregion
}