using System.Reflection;
using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Shell;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Shell;

/// <summary>ShellViewModel のナビゲーション挙動を検証するテストを提供する。</summary>
public sealed class ShellViewModelTests {
    /// <summary>ナビゲーションイベントで CanGoBack/CanGoForward が更新されることを検証する。</summary>
    [StaFact]
    public async Task ナビゲーションイベントで戻る進むの状態を更新する() {
        var navigationServiceMock = new Mock<INavigationService>();
        bool canGoBack = false;
        bool canGoForward = false;
        navigationServiceMock
            .SetupGet( service => service.CanGoBack )
            .Returns( () => canGoBack );
        navigationServiceMock
            .SetupGet( service => service.CanGoForward )
            .Returns( () => canGoForward );
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) );

        NavigatedEventHandler? navigatedHandler = null;
        navigationServiceMock
            .SetupAdd( service => service.Navigated += It.IsAny<NavigatedEventHandler>() )
            .Callback<NavigatedEventHandler>( handler => navigatedHandler += handler );
        navigationServiceMock
            .SetupRemove( service => service.Navigated -= It.IsAny<NavigatedEventHandler>() )
            .Callback<NavigatedEventHandler>( handler => navigatedHandler -= handler );

        NavigatingCancelEventHandler? navigatingHandler = null;
        navigationServiceMock
            .SetupAdd( service => service.Navigating += It.IsAny<NavigatingCancelEventHandler>() )
            .Callback<NavigatingCancelEventHandler>( handler => navigatingHandler += handler );
        navigationServiceMock
            .SetupRemove( service => service.Navigating -= It.IsAny<NavigatingCancelEventHandler>() )
            .Callback<NavigatingCancelEventHandler>( handler => navigatingHandler -= handler );

        var eventAggregatorMock = new Mock<IEventAggregator>();

        var viewModel = new ShellViewModel(
            eventAggregatorMock.Object,
            navigationServiceMock.Object );

        await viewModel.ActivateAsync( CancellationToken.None );

        canGoBack = true;
        navigatingHandler?.Invoke( navigationServiceMock.Object, CreateNavigatingEventArgs() );

        Assert.True( viewModel.CanGoBack );
        Assert.False( viewModel.CanGoForward );

        canGoForward = true;
        navigatedHandler?.Invoke( navigationServiceMock.Object, CreateNavigatedEventArgs() );

        Assert.True( viewModel.CanGoBack );
        Assert.True( viewModel.CanGoForward );
    }

    private static NavigatingCancelEventArgs CreateNavigatingEventArgs() {
        var ctor = typeof( NavigatingCancelEventArgs )
            .GetConstructors( BindingFlags.Instance | BindingFlags.NonPublic )
            .Single();
        return (NavigatingCancelEventArgs)ctor.Invoke( [null, null, null, null, NavigationMode.New, null, null, false] );
    }

    private static NavigationEventArgs CreateNavigatedEventArgs() {
        var ctor = typeof( NavigationEventArgs ).GetConstructors( BindingFlags.Instance | BindingFlags.NonPublic ).Single();
        return (NavigationEventArgs)ctor.Invoke( [null, null, null, null, null, false] );
    }
}