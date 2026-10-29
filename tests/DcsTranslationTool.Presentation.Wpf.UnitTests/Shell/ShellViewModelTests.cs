using System.Reflection;
using System.Windows.Navigation;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Shared.Models;

using MaterialDesignThemes.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Shell;

/// <summary>ShellViewModel のナビゲーション挙動を検証するテストを提供する。</summary>
public sealed class ShellViewModelTests {
    /// <summary>ダウンロード確認では更新方式を表示せず、ダウンロード操作を強調することを検証する。</summary>
    [StaFact]
    public async Task ExecuteApplicationUpdateAsyncは更新方式を表示せずダウンロードを強調する() {
        var appSettingsService = new Mock<IAppSettingsService>();
        appSettingsService.SetupGet( service => service.Settings ).Returns( new AppSettings() );
        ConfirmationDialogParameters? capturedParameters = null;
        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .Callback<ConfirmationDialogParameters>( parameters => capturedParameters = parameters )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = new ShellViewModel(
            appSettingsService.Object,
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILoggingService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<ISnackbarService>(),
            Mock.Of<ISystemService>(),
            Mock.Of<IUpdateCheckService>(),
            Mock.Of<IApplicationUpdateService>(),
            dialogService.Object );
        var update = CreateUpdateCheckResult();
        var method = typeof( ShellViewModel ).GetMethod( "ExecuteApplicationUpdateAsync", BindingFlags.Instance | BindingFlags.NonPublic );

        await (Task)method!.Invoke( viewModel, [update] )!;

        Assert.NotNull( capturedParameters );
        Assert.DoesNotContain( "種類:", capturedParameters.Message, StringComparison.Ordinal );
    }

    /// <summary>更新準備の失敗時は次の操作を案内し、ボタンを指定順で強調することを検証する。</summary>
    [StaFact]
    public async Task HandleUpdateFailureAsyncは再試行と手動更新を案内する() {
        var appSettingsService = new Mock<IAppSettingsService>();
        appSettingsService.SetupGet( service => service.Settings ).Returns( new AppSettings() );
        ConfirmationDialogParameters? capturedParameters = null;
        var dialogService = new Mock<IDialogService>();
        dialogService
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .Callback<ConfirmationDialogParameters>( parameters => capturedParameters = parameters )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var viewModel = new ShellViewModel(
            appSettingsService.Object,
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILoggingService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<ISnackbarService>(),
            Mock.Of<ISystemService>(),
            Mock.Of<IUpdateCheckService>(),
            Mock.Of<IApplicationUpdateService>(),
            dialogService.Object );
        var method = typeof( ShellViewModel ).GetMethod( "HandleUpdateFailureAsync", BindingFlags.Instance | BindingFlags.NonPublic );

        await (Task)method!.Invoke( viewModel, [CreateUpdateCheckResult()] )!;

        Assert.NotNull( capturedParameters );
        Assert.Equal( "自動更新に失敗しました", capturedParameters.Title );
        Assert.Contains( "再試行", capturedParameters.Message, StringComparison.Ordinal );
        Assert.Contains( "リリースページ", capturedParameters.Message, StringComparison.Ordinal );
        Assert.Equal(
            [ConfirmationDialogResult.Confirm, ConfirmationDialogResult.Secondary, ConfirmationDialogResult.Cancel],
            capturedParameters.ButtonOrder );
    }

    /// <summary>補助ウィンドウが終了を拒否した場合は更新適用プロセスを開始しないことを検証する。</summary>
    [StaFact]
    public async Task ConfirmRestartAsyncは補助ウィンドウが終了を拒否した場合に更新を起動しない() {
        var appSettingsService = new Mock<IAppSettingsService>();
        appSettingsService.SetupGet( service => service.Settings ).Returns( new AppSettings() );
        var applicationUpdateService = new Mock<IApplicationUpdateService>();
        applicationUpdateService
            .Setup( service => service.DiscardStagedUpdate( It.IsAny<ApplicationUpdateStageResult>() ) )
            .Returns( FluentResults.Result.Ok() );
        var dialogService = new Mock<IDialogService>();
        dialogService.Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        var restartCoordinator = new Mock<IApplicationRestartCoordinator>();
        restartCoordinator.Setup( service => service.PrepareForRestartAsync( It.IsAny<CancellationToken>() ) ).ReturnsAsync( false );
        var viewModel = new ShellViewModel(
            appSettingsService.Object,
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILoggingService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<ISnackbarService>(),
            Mock.Of<ISystemService>(),
            Mock.Of<IUpdateCheckService>(),
            applicationUpdateService.Object,
            dialogService.Object,
            restartCoordinator.Object );
        var stageResult = new ApplicationUpdateStageResult(
            @"C:\updates\workspace",
            @"C:\updates\workspace\staging",
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/dcs-translation-japanese/DCS-Translation-Tool/releases/tag/v1.4.0" );
        var method = typeof( ShellViewModel ).GetMethod( "ConfirmRestartAsync", BindingFlags.Instance | BindingFlags.NonPublic );

        await (Task)method!.Invoke( viewModel, [stageResult] )!;

        applicationUpdateService.Verify(
            service => service.LaunchApplyProcess( It.IsAny<ApplicationUpdateStageResult>(), It.IsAny<int>() ),
            Times.Never );
    }

    /// <summary>再起動を後回しにした場合はステージ済み更新を保持することを検証する。</summary>
    [StaFact]
    public async Task ConfirmRestartAsyncは再起動を後回しにした場合にステージ済み更新を保持する() {
        var appSettingsService = new Mock<IAppSettingsService>();
        appSettingsService.SetupGet( service => service.Settings ).Returns( new AppSettings() );
        var applicationUpdateService = new Mock<IApplicationUpdateService>();
        applicationUpdateService
            .Setup( service => service.DiscardStagedUpdate( It.IsAny<ApplicationUpdateStageResult>() ) )
            .Returns( FluentResults.Result.Ok() );
        var dialogService = new Mock<IDialogService>();
        dialogService.Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        var viewModel = new ShellViewModel(
            appSettingsService.Object,
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILoggingService>(),
            Mock.Of<INavigationService>(),
            Mock.Of<ISnackbarService>(),
            Mock.Of<ISystemService>(),
            Mock.Of<IUpdateCheckService>(),
            applicationUpdateService.Object,
            dialogService.Object );
        var stageResult = new ApplicationUpdateStageResult(
            @"C:\updates\workspace",
            @"C:\updates\workspace\staging",
            new Version( 1, 3, 1 ),
            new Version( 1, 4, 0 ),
            "win-x64",
            ApplicationUpdateAssetKind.Full,
            "https://github.com/dcs-translation-japanese/DCS-Translation-Tool/releases/tag/v1.4.0" );
        var method = typeof( ShellViewModel ).GetMethod( "ConfirmRestartAsync", BindingFlags.Instance | BindingFlags.NonPublic );

        await (Task)method!.Invoke( viewModel, [stageResult] )!;

        applicationUpdateService.Verify(
            service => service.DiscardStagedUpdate( It.IsAny<ApplicationUpdateStageResult>() ),
            Times.Never );
        applicationUpdateService.Verify(
            service => service.LaunchApplyProcess( It.IsAny<ApplicationUpdateStageResult>(), It.IsAny<int>() ),
            Times.Never );
    }

    /// <summary>ステージ済み更新の有無にかかわらず起動時に最新版を確認することを検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncはステージ済み更新サービスがある場合も最新更新を確認する() {
        var appSettingsService = new Mock<IAppSettingsService>();
        appSettingsService.SetupGet( service => service.Settings ).Returns( new AppSettings() );
        var updateCheckService = new Mock<IUpdateCheckService>();
        updateCheckService
            .Setup( service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( UpdateCheckResult.NoUpdate );
        var navigationService = new Mock<INavigationService>();
        navigationService
            .Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) );
        var applicationUpdateService = new Mock<IApplicationUpdateService>();
        applicationUpdateService
            .Setup( service => service.GetPendingUpdate() )
            .Returns( FluentResults.Result.Ok<ApplicationUpdateStageResult?>( null ) );
        var viewModel = new ShellViewModel(
            appSettingsService.Object,
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILoggingService>(),
            navigationService.Object,
            Mock.Of<ISnackbarService>(),
            Mock.Of<ISystemService>(),
            updateCheckService.Object,
            applicationUpdateService.Object );

        await viewModel.ActivateAsync( CancellationToken.None );
        await Task.Delay( 100 );

        updateCheckService.Verify(
            service => service.CheckForUpdateAsync( It.IsAny<CancellationToken>() ),
            Times.Once );
    }

    /// <summary>ActivateAsync を呼び出した際の初期ナビゲーションと Snackbar 通知を検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncを呼び出すと初期ナビゲーションと設定不足を通知する() {
        var appSettings = new AppSettings
        {
            DcsWorldInstallDir = string.Empty,
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
                DcsWorldInstallDir = "set",
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
            .ReturnsAsync( new UpdateCheckResult( true, "v1.3.2", "https://github.com/dcs-translation-japanese/translations/releases/tag/v1.3.2" ) );

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
            service => service.OpenInWebBrowser( "https://github.com/dcs-translation-japanese/translations/releases/tag/v1.3.2" ),
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
                DcsWorldInstallDir = "set",
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

    /// <summary>外部保存が有効で保存先未設定の場合に設定不足を通知することを検証する。</summary>
    [StaFact]
    public async Task ActivateAsyncは外部保存先が不足している場合に設定不足を通知する() {
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( new AppSettings
            {
                DcsWorldInstallDir = @"C:\DCS",
                TranslateFileDir = @"C:\Translate",
                UseExternalAircraftInjectionDir = true,
                ExternalAircraftInjectionDir = string.Empty
            } );

        var eventAggregatorMock = new Mock<IEventAggregator>();
        var loggerMock = new Mock<ILoggingService>();
        var snackbarQueueMock = new Mock<ISnackbarMessageQueue>();
        string? capturedMessage = null;
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
            .Callback<string, string?, System.Action?, object?, TimeSpan?>( ( message, _, _, _, _ ) => capturedMessage = message );

        var navigationServiceMock = new Mock<INavigationService>();
        navigationServiceMock
            .SetupGet( service => service.CanGoBack )
            .Returns( false );
        navigationServiceMock
            .SetupGet( service => service.CanGoForward )
            .Returns( false );
        navigationServiceMock
            .Setup( service => service.NavigateToViewModel<MainViewModel>( It.IsAny<object?>() ) );

        NavigatedEventHandler? navigatedHandler = null;
        navigationServiceMock
            .SetupAdd( service => service.Navigated += It.IsAny<NavigatedEventHandler>() )
            .Callback<NavigatedEventHandler>( handler => navigatedHandler += handler );

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
        navigatedHandler?.Invoke( navigationServiceMock.Object, CreateNavigatedEventArgs() );

        Assert.Equal( "設定が不足しています。", capturedMessage );
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

    /// <summary>ダウンロード確認用の更新結果を作成する。</summary>
    private static UpdateCheckResult CreateUpdateCheckResult() {
        var checksum = new ApplicationUpdateChecksumAsset( "update.zip.sha256", "https://example.test/update.zip.sha256", 512 * 1024, "checksum" );
        var asset = new ApplicationUpdateAsset(
            "update.zip",
            "https://example.test/update.zip",
            9 * 1024 * 1024,
            "digest",
            ApplicationUpdateAssetKind.Full,
            null,
            new Version( 1, 2, 0 ),
            checksum );
        return new UpdateCheckResult(
            true,
            new Version( 1, 0, 0 ),
            new Version( 1, 2, 0 ),
            "v1.2.0",
            "https://github.com/dcs-translation-japanese/DCS-Translation-Tool/releases/tag/v1.2.0",
            "win-x64",
            true,
            asset,
            [] );
    }
}