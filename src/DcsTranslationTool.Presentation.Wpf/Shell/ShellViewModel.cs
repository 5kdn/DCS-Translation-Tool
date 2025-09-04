using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.Features.Main;

namespace DcsTranslationTool.Presentation.Wpf.Shell;

public class ShellViewModel(
    IEventAggregator eventAggregator,
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
    }

    public Task HandleAsync( string message, CancellationToken cancellationToken ) {
        return Task.CompletedTask;
    }

    public async Task ActivateAsync( CancellationToken cancellationToken ) {
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
        eventAggregator.Unsubscribe( this );
        navigationService.Navigating -= OnNavigating;
        navigationService.Navigated -= OnNavigated;
        return base.OnDeactivateAsync( close, cancellationToken );
    }

    #endregion

    #region Action Messages

    public void GoBack() {
        navigationService.GoBack();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    public void GoForward() {
        navigationService.GoForward();
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    #endregion

    #region Navigation event handlers

    private void OnNavigating( object? s, NavigatingCancelEventArgs e ) {
        // 直前でも有効/無効が変わるケースに備えて更新
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    private void OnNavigated( object? s, NavigationEventArgs e ) {
        // スタック確定後に更新
        NotifyOfPropertyChange( nameof( CanGoBack ) );
        NotifyOfPropertyChange( nameof( CanGoForward ) );
    }

    #endregion
}