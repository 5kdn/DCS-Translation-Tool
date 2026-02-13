using System.Reflection;
using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;
using DcsTranslationTool.Shared.Models;

using MaterialDesignThemes.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Shell;

/// <summary>ShellViewModel のナビゲーション挙動を検証するテストを提供する。</summary>
public sealed class ShellViewModelTests {
    /// <summary>ActivateAsync を呼び出した際の初期ナビゲーションと Snackbar 通知を検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncを呼び出すと初期ナビゲーションと設定不足を通知する() {
        var appSettings = new AppSettings
        {
            SourceAircraftDir = string.Empty,
            SourceDlcCampaignDir = string.Empty,
            TranslateFileDir = string.Empty
        };

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var eventAggregatorMock = new Mock<IEventAggregator>();
        var loggerMock = new Mock<ILoggingService>();

        var snackbarQueueMock = new Mock<ISnackbarMessageQueue>();
        System.Action? capturedAction = null;
        string? capturedMessage = null;
        string? capturedActionContent = null;
        TimeSpan? capturedDuration = null;
        object? capturedArgument = null;

        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<System.Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ) )
            .Callback<string, string?, System.Action?, object?, TimeSpan?>(
                ( message, actionContent, action, argument, duration ) => {
                    capturedMessage = message;
                    capturedActionContent = actionContent;
                    capturedAction = action;
                    capturedDuration = duration;
                    capturedArgument = argument;
                } );

        var systemServiceMock = new Mock<ISystemService>();
        var updateCheckServiceMock = new Mock<IUpdateCheckService>();
        updateCheckServiceMock
            .Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( UpdateCheckResult.NoUpdate );

        bool navigateToMainInvoked = false;
        bool navigateToSettingsInvoked = false;
        var navigationServiceMock = new Mock<INavigationService>();
        navigationServiceMock
            .SetupGet( service => service.CanGoBack )
            .Returns( false );
        navigationServiceMock
            .SetupGet( service => service.CanGoForward )
            .Returns( false );
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) )
            .Callback<object?>( _ => navigateToMainInvoked = true );
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<SettingsViewModel>( It.IsAny<object?>() ) )
            .Callback<object?>( _ => navigateToSettingsInvoked = true );

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

        var viewModel = new ShellViewModel(
            appSettingsServiceMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            navigationServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object,
            updateCheckServiceMock.Object );

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.True( navigateToMainInvoked );
        Assert.False( viewModel.CanGoBack );
        Assert.False( viewModel.CanGoForward );

        navigatingHandler?.Invoke( navigationServiceMock.Object, CreateNavigatingEventArgs() );
        navigatedHandler?.Invoke( navigationServiceMock.Object, CreateNavigatedEventArgs() );

        Assert.Equal( "設定が不足しています。", capturedMessage );
        Assert.Equal( "設定", capturedActionContent );
        Assert.Equal( TimeSpan.FromSeconds( 3 ), capturedArgument );
        Assert.Null( capturedDuration );
        Assert.NotNull( capturedAction );

        capturedAction!.Invoke();
        Assert.True( navigateToSettingsInvoked );
    }

    /// <summary>ナビゲーションイベントで CanGoBack/CanGoForward が更新されることを検証する。</summary>
    [StaFact]
    public async Task ナビゲーションイベントで戻る進むの状態を更新する() {
        var appSettings = new AppSettings();
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

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
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<SettingsViewModel>( It.IsAny<object?>() ) );

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

        var loggerMock = new Mock<ILoggingService>();
        var eventAggregatorMock = new Mock<IEventAggregator>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        var snackbarQueueMock = new Mock<ISnackbarMessageQueue>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarQueueMock.Object );

        var systemServiceMock = new Mock<ISystemService>();
        var updateCheckServiceMock = new Mock<IUpdateCheckService>();
        updateCheckServiceMock
            .Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( UpdateCheckResult.NoUpdate );

        var viewModel = new ShellViewModel(
            appSettingsServiceMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            navigationServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object,
            updateCheckServiceMock.Object );

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

    /// <summary>更新がある場合のみ起動時通知を表示することを検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncは更新ありの場合のみ更新通知を表示する() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( new AppSettings
            {
                SourceAircraftDir = "set",
                SourceDlcCampaignDir = "set",
                TranslateFileDir = "set"
            } );

        var eventAggregatorMock = new Mock<IEventAggregator>();
        var loggerMock = new Mock<ILoggingService>();

        var snackbarQueueMock = new Mock<ISnackbarMessageQueue>();
        string? capturedMessage = null;
        string? capturedActionContent = null;
        System.Action? capturedAction = null;
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<System.Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ) )
            .Callback<string, string?, System.Action?, object?, TimeSpan?>( ( message, actionContent, action, _, _ ) => {
                capturedMessage = message;
                capturedActionContent = actionContent;
                capturedAction = action;
            } );

        var navigationServiceMock = new Mock<INavigationService>();
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) );

        var systemServiceMock = new Mock<ISystemService>();

        var updateCheckServiceMock = new Mock<IUpdateCheckService>();
        updateCheckServiceMock
            .Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new UpdateCheckResult( true, "v1.3.2", "https://github.com/5kdn/DCS-Translation-Japanese/releases/tag/v1.3.2" ) );

        var viewModel = new ShellViewModel(
            appSettingsServiceMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            navigationServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object,
            updateCheckServiceMock.Object );

        await viewModel.ActivateAsync( CancellationToken.None );
        await Task.Delay( 100 );

        Assert.Equal( "新しいバージョン v1.3.2 が利用可能です", capturedMessage );
        Assert.Equal( "DLページ", capturedActionContent );
        Assert.NotNull( capturedAction );
        capturedAction!.Invoke();
        systemServiceMock.Verify(
            service => service.OpenInWebBrowser( "https://github.com/5kdn/DCS-Translation-Japanese/releases/tag/v1.3.2" ),
            Times.Once );
    }

    /// <summary>更新がない場合は起動時通知を表示しないことを検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncは更新なしの場合に更新通知を表示しない() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( new AppSettings
            {
                SourceAircraftDir = "set",
                SourceDlcCampaignDir = "set",
                TranslateFileDir = "set"
            } );

        var eventAggregatorMock = new Mock<IEventAggregator>();
        var loggerMock = new Mock<ILoggingService>();

        var snackbarQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarQueueMock.Object );

        var navigationServiceMock = new Mock<INavigationService>();
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) );

        var systemServiceMock = new Mock<ISystemService>();

        var updateCheckServiceMock = new Mock<IUpdateCheckService>();
        updateCheckServiceMock
            .Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( UpdateCheckResult.NoUpdate );

        var viewModel = new ShellViewModel(
            appSettingsServiceMock.Object,
            eventAggregatorMock.Object,
            loggerMock.Object,
            navigationServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object,
            updateCheckServiceMock.Object );

        await viewModel.ActivateAsync( CancellationToken.None );
        await Task.Delay( 100 );

        snackbarServiceMock.Verify(
            service => service.Show(
                It.Is<string>( message => message.StartsWith( "新しいバージョン ", StringComparison.Ordinal ) ),
                It.IsAny<string?>(),
                It.IsAny<System.Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ),
            Times.Never );
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