using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignThemes.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.Download;

/// <summary>DownloadViewModel の動作を検証するテストを提供する。</summary>
public sealed class DownloadViewModelTests : IDisposable {
    private readonly string _tempDir;

    public DownloadViewModelTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"DownloadViewModelTests_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( _tempDir );
    }



    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    /// <summary>ダウンロードURL取得に失敗した場合にエラーメッセージを提示することを確認する。</summary>
    [StaFact]
    public async Task Downloadを呼び出すとURL取得に失敗するとエラーを通知する() {
        var appSettings = new AppSettings { TranslateFileDir = _tempDir };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";

        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "deadbeef" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var downloadResult = Result.Fail<ApiDownloadFilePathsResult>( ResultErrorFactory.Validation( "network failure", "TEST_VALIDATION" ) );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );
        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.Is<ApiDownloadFilePathsRequest>( request =>
                    request.Paths.Count == 1 &&
                    request.Paths[0] == repoEntryPath &&
                    request.ETag == null
                ),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( downloadResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>();
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );
        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessages = new List<string>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) )
            .Callback<string, string?, Action?, object?, TimeSpan?>(
                ( message, _, _, _, _ ) => snackbarMessages.Add( message )
            );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var pathSafetyGuard = new PathSafetyGuard();
        var applyWorkflowService = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var downloadWorkflowService = new DownloadWorkflowService(
            apiServiceMock.Object,
            loggingServiceMock.Object,
            applyWorkflowService.Object
        );
        var fileEntryWatcherLifecycle = new FileEntryWatcherLifecycle(
            appSettingsServiceMock.Object,
            fileEntryServiceMock.Object,
            loggingServiceMock.Object
        );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowService,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycle,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
        var fileNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        Assert.True( viewModel.CanDownload );

        await viewModel.Download();

        Assert.Contains( "ダウンロード対象が不正です", snackbarMessages );
        var expectedFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Example.lua" );
        Assert.False( File.Exists( expectedFilePath ) );

        apiServiceMock.VerifyAll();
    }

    /// <summary>保存先未設定時はダウンロード処理を開始せずチェック状態を維持することを確認する。</summary>
    [StaFact]
    public async Task Downloadを呼び出すと保存先未設定の場合は処理を中断してチェック状態を維持する() {
        var appSettings = new AppSettings { TranslateFileDir = string.Empty };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";

        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "deadbeef" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>( MockBehavior.Strict );
        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessages = new List<string>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) )
            .Callback<string, string?, Action?, object?, TimeSpan?>(
                ( message, _, _, _, _ ) => snackbarMessages.Add( message )
            );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
        var fileNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;
        Assert.True( viewModel.CanDownload );

        await viewModel.Download();

        Assert.Contains( "保存先フォルダーが設定されていません", snackbarMessages );
        var currentNode = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( currentNode );
        Assert.True( currentNode!.CheckState );

        downloadWorkflowServiceMock.Verify( service => service.ExecuteDownloadAsync(
            It.IsAny<DownloadExecutionRequest>(),
            It.IsAny<Func<double, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
        apiServiceMock.VerifyAll();
    }


    /// <summary>変更が無い場合にダウンロードをスキップすることを確認する。</summary>
    [StaFact]
    public async Task Downloadを呼び出すと変更が無い場合はダウンロードをスキップする() {
        var appSettings = new AppSettings { TranslateFileDir = _tempDir };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";

        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "feedface" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var downloadResult = Result.Ok(
            new ApiDownloadFilePathsResult( [], "\"etag-value\"" )
        );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );
        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.Is<ApiDownloadFilePathsRequest>( request =>
                    request.Paths.Count == 1 &&
                    request.Paths[0] == repoEntryPath &&
                    request.ETag == null
                ),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( downloadResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>();
        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessages = new List<string>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) )
            .Callback<string, string?, Action?, object?, TimeSpan?>(
                ( message, _, _, _, _ ) => snackbarMessages.Add( message )
            );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var pathSafetyGuard = new PathSafetyGuard();
        var applyWorkflowService = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var downloadWorkflowService = new DownloadWorkflowService(
            apiServiceMock.Object,
            loggingServiceMock.Object,
            applyWorkflowService.Object
        );
        var fileEntryWatcherLifecycle = new FileEntryWatcherLifecycle(
            appSettingsServiceMock.Object,
            fileEntryServiceMock.Object,
            loggingServiceMock.Object
        );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowService,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycle,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
        var fileNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        Assert.True( viewModel.CanDownload );

        await viewModel.Download();

        var expectedFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Example.lua" );
        Assert.False( File.Exists( expectedFilePath ) );
        Assert.Contains( snackbarMessages, message => message == "対象ファイルは最新です" );
        Assert.Equal( 0, viewModel.DownloadedProgress );
        Assert.True( viewModel.CanDownload );

        apiServiceMock.VerifyAll();
    }

    /// <summary>Applyは適用ワークフローを実行することを確認する。</summary>
    [StaFact]
    public async Task Applyを呼び出すと適用ワークフローを実行する() {
        var sourceRoot = Path.Combine( _tempDir, "AircraftSource" );
        Directory.CreateDirectory( sourceRoot );

        var appSettings = new AppSettings
        {
            TranslateFileDir = _tempDir,
            DcsWorldInstallDir = sourceRoot
        };

        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "abc12345" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>();
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );

        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        downloadWorkflowServiceMock
            .Setup( service => service.ExecuteApplyAsync(
                It.IsAny<ApplyExecutionRequest>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( new ApplyWorkflowResult( true, [] ) );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
        var fileNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;
        Assert.True( viewModel.CanApply );

        await viewModel.Apply();

        downloadWorkflowServiceMock.Verify( service => service.ExecuteApplyAsync(
            It.IsAny<ApplyExecutionRequest>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<Func<double, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Once );
        apiServiceMock.Verify( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>Applyを呼び出すとModifiedエントリで全てローカル版適用を実行することを確認する。</summary>
    [StaFact]
    public async Task Applyを呼び出すとModifiedエントリで全てローカル版適用を実行する() {
        var appSettings = new AppSettings
        {
            TranslateFileDir = _tempDir,
            DcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" )
        };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "repo-sha" );
        var localEntry = new LocalFileEntry( "Example.lua", repoEntryPath, false, "local-sha" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>( MockBehavior.Strict );
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [localEntry] ) );

        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplyModeDialogShowAsync(
                It.Is<DownloadModifiedApplyModeDialogParameters>( parameters =>
                    parameters.ApplyAllRepositoryButtonText == "全てサーバー版を適用" &&
                    parameters.ApplyAllLocalButtonText == "全てローカル版を適用" &&
                    parameters.SelectIndividuallyButtonText == "個別に選択する" &&
                    parameters.CancelButtonText == "中止" ) ) )
            .ReturnsAsync( DownloadModifiedApplyModeDialogResult.ApplyAllLocal );

        downloadWorkflowServiceMock
            .Setup( service => service.ExecuteApplyAsync(
                It.IsAny<ApplyExecutionRequest>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( new ApplyWorkflowResult( true, [] ) );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var fileNode = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        Assert.Equal( FileChangeType.Modified, fileNode!.ChangeType );
        fileNode.CheckState = true;

        await viewModel.Apply();

        dialogServiceMock.VerifyAll();
        downloadWorkflowServiceMock.Verify( service => service.SyncModifiedFilesWithRepositoryAsync(
            It.IsAny<IReadOnlyList<IFileEntryViewModel>>(),
            It.IsAny<string>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
        downloadWorkflowServiceMock.Verify( service => service.ExecuteApplyAsync(
            It.IsAny<ApplyExecutionRequest>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<Func<double, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Once );
    }

    /// <summary>Applyを呼び出すとModifiedエントリで全てサーバー版適用を実行することを確認する。</summary>
    [StaFact]
    public async Task Applyを呼び出すとModifiedエントリで全てサーバー版適用を実行する() {
        var appSettings = new AppSettings
        {
            TranslateFileDir = _tempDir,
            DcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" )
        };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "repo-sha" );
        var localEntry = new LocalFileEntry( "Example.lua", repoEntryPath, false, "local-sha" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>( MockBehavior.Strict );
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [localEntry] ) );

        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplyModeDialogShowAsync( It.IsAny<DownloadModifiedApplyModeDialogParameters>() ) )
            .ReturnsAsync( DownloadModifiedApplyModeDialogResult.ApplyAllRepository );

        downloadWorkflowServiceMock
            .Setup( service => service.SyncModifiedFilesWithRepositoryAsync(
                It.Is<IReadOnlyList<IFileEntryViewModel>>( entries => entries.Count == 1 && entries[0].Path == repoEntryPath ),
                _tempDir,
                It.IsAny<Func<string, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( true );

        downloadWorkflowServiceMock
            .Setup( service => service.ExecuteApplyAsync(
                It.IsAny<ApplyExecutionRequest>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( new ApplyWorkflowResult( true, [] ) );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var fileNode = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        await viewModel.Apply();

        downloadWorkflowServiceMock.VerifyAll();
    }

    /// <summary>Applyを呼び出すとModifiedエントリで個別選択後にサーバー版指定分のみ同期することを確認する。</summary>
    [StaFact]
    public async Task Applyを呼び出すとModifiedエントリで個別選択後にサーバー版指定分のみ同期する() {
        var appSettings = new AppSettings
        {
            TranslateFileDir = _tempDir,
            DcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" )
        };
        const string repoEntryPath1 = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
        const string repoEntryPath2 = "DCSWorld/Mods/aircraft/A10C/L10N/Alt.lua";
        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", repoEntryPath1, false, repoSha: "repo-sha-1" ),
            new RepoFileEntry( "Alt.lua", repoEntryPath2, false, repoSha: "repo-sha-2" )
        };
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", repoEntryPath1, false, "local-sha-1" ),
            new LocalFileEntry( "Alt.lua", repoEntryPath2, false, "local-sha-2" )
        };
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( repoEntries );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>( MockBehavior.Strict );
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( localEntries ) );

        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplyModeDialogShowAsync( It.IsAny<DownloadModifiedApplyModeDialogParameters>() ) )
            .ReturnsAsync( DownloadModifiedApplyModeDialogResult.SelectIndividually );

        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplySelectionDialogShowAsync( It.IsAny<DownloadModifiedApplySelectionDialogParameters>() ) )
            .ReturnsAsync( new DownloadModifiedApplySelectionDialogResult(
                true,
                new Dictionary<string, DownloadModifiedApplySource>
                {
                    [repoEntryPath1] = DownloadModifiedApplySource.Repository,
                    [repoEntryPath2] = DownloadModifiedApplySource.Local
                } ) );

        downloadWorkflowServiceMock
            .Setup( service => service.SyncModifiedFilesWithRepositoryAsync(
                It.Is<IReadOnlyList<IFileEntryViewModel>>( entries => entries.Count == 1 && entries[0].Path == repoEntryPath1 ),
                _tempDir,
                It.IsAny<Func<string, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( true );

        downloadWorkflowServiceMock
            .Setup( service => service.ExecuteApplyAsync(
                It.IsAny<ApplyExecutionRequest>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( new ApplyWorkflowResult( true, [] ) );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var fileNode1 = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Example.lua" );
        var fileNode2 = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Alt.lua" );
        Assert.NotNull( fileNode1 );
        Assert.NotNull( fileNode2 );
        fileNode1!.CheckState = true;
        fileNode2!.CheckState = true;

        await viewModel.Apply();

        downloadWorkflowServiceMock.VerifyAll();
    }

    /// <summary>Applyを呼び出すとModifiedエントリでモード選択中止時は処理を実行しないことを確認する。</summary>
    [StaFact]
    public async Task Applyを呼び出すとModifiedエントリでモード選択中止時は処理を実行しない() {
        var appSettings = new AppSettings
        {
            TranslateFileDir = _tempDir,
            DcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" )
        };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "repo-sha" );
        var localEntry = new LocalFileEntry( "Example.lua", repoEntryPath, false, "local-sha" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>( MockBehavior.Strict );
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [localEntry] ) );

        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplyModeDialogShowAsync( It.IsAny<DownloadModifiedApplyModeDialogParameters>() ) )
            .ReturnsAsync( DownloadModifiedApplyModeDialogResult.Cancel );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var fileNode = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        await viewModel.Apply();

        downloadWorkflowServiceMock.Verify( service => service.SyncModifiedFilesWithRepositoryAsync(
            It.IsAny<IReadOnlyList<IFileEntryViewModel>>(),
            It.IsAny<string>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
        downloadWorkflowServiceMock.Verify( service => service.ExecuteApplyAsync(
            It.IsAny<ApplyExecutionRequest>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<Func<double, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
    }

    /// <summary>Applyを呼び出すとModifiedエントリで個別選択中止時は処理を実行しないことを確認する。</summary>
    [StaFact]
    public async Task Applyを呼び出すとModifiedエントリで個別選択中止時は処理を実行しない() {
        var appSettings = new AppSettings
        {
            TranslateFileDir = _tempDir,
            DcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" )
        };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "repo-sha" );
        var localEntry = new LocalFileEntry( "Example.lua", repoEntryPath, false, "local-sha" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        apiServiceMock
            .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( treeResult );

        var dispatcherServiceMock = new Mock<IDispatcherService>();
        dispatcherServiceMock
            .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
            .Returns<Func<Task>>( func => func() );

        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock
            .SetupGet( service => service.Settings )
            .Returns( appSettings );

        var fileEntryServiceMock = new Mock<IFileEntryService>( MockBehavior.Strict );
        fileEntryServiceMock
            .Setup( service => service.GetEntriesAsync() )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [localEntry] ) );

        var loggingServiceMock = new Mock<ILoggingService>();
        var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
        var snackbarServiceMock = new Mock<ISnackbarService>();
        snackbarServiceMock
            .SetupGet( service => service.MessageQueue )
            .Returns( snackbarMessageQueueMock.Object );
        snackbarServiceMock
            .Setup( service => service.Show(
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>()
            ) );

        var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
        var downloadWorkflowServiceMock = new Mock<IDownloadWorkflowService>( MockBehavior.Strict );
        var fileEntryWatcherLifecycleMock = new Mock<IFileEntryWatcherLifecycle>( MockBehavior.Strict );
        var fileEntryTreeService = new FileEntryTreeService( loggingServiceMock.Object );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );

        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplyModeDialogShowAsync( It.IsAny<DownloadModifiedApplyModeDialogParameters>() ) )
            .ReturnsAsync( DownloadModifiedApplyModeDialogResult.SelectIndividually );
        dialogServiceMock
            .Setup( service => service.DownloadModifiedApplySelectionDialogShowAsync( It.IsAny<DownloadModifiedApplySelectionDialogParameters>() ) )
            .ReturnsAsync( new DownloadModifiedApplySelectionDialogResult( false, new Dictionary<string, DownloadModifiedApplySource>() ) );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
            dialogServiceMock.Object,
            downloadWorkflowServiceMock.Object,
            dispatcherServiceMock.Object,
            fileEntryServiceMock.Object,
            fileEntryWatcherLifecycleMock.Object,
            fileEntryTreeService,
            loggingServiceMock.Object,
            snackbarServiceMock.Object,
            systemServiceMock.Object
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var fileNode = FindNodeByPath( viewModel.Tabs[aircraftIndex].Root, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        await viewModel.Apply();

        downloadWorkflowServiceMock.Verify( service => service.SyncModifiedFilesWithRepositoryAsync(
            It.IsAny<IReadOnlyList<IFileEntryViewModel>>(),
            It.IsAny<string>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
        downloadWorkflowServiceMock.Verify( service => service.ExecuteApplyAsync(
            It.IsAny<ApplyExecutionRequest>(),
            It.IsAny<Func<string, Task>>(),
            It.IsAny<Func<double, Task>>(),
            It.IsAny<CancellationToken>()
        ), Times.Never );
    }


    private static IFileEntryViewModel? FindNodeByPath( IFileEntryViewModel root, params string[] segments ) {
        IFileEntryViewModel? current = root;
        foreach(var segment in segments) {
            current = current?.Children.FirstOrDefault( child => string.Equals( child?.Name, segment, StringComparison.Ordinal ) );
            if(current is null) return null;
        }
        return current;
    }

}