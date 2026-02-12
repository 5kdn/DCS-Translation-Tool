using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignThemes.Wpf;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.Download;

/// <summary>DownloadViewModel の動作を検証するテストを提供する。</summary>
public sealed class DownloadViewModelTests : IDisposable {
    private readonly string _tempDir;

    public DownloadViewModelTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"DownloadViewModelTests_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( _tempDir );
    }

    ///// <summary>Applyを呼び出すとRepoOnlyエントリを trk へ適用する。</summary>
    //[StaFact]
    //public async Task Applyを呼び出すとRepoOnlyエントリをtrkへ適用する() {
    //    var sourceRoot = Path.Combine( _tempDir, "AircraftSourceTrk" );
    //    var trkDirectory = Path.Combine( sourceRoot, "A10C", "Missions", "EN" );
    //    Directory.CreateDirectory( trkDirectory );
    //    var trkPath = Path.Combine( trkDirectory, "Example.trk" );
    //    File.WriteAllBytes( trkPath, [] );

    //    var appSettings = new AppSettings
    //    {
    //        TranslateFileDir = _tempDir,
    //        SourceAircraftDir = sourceRoot
    //    };
    //    const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.trk/Localization/Example.lua";
    //    const string fileContent = "trk内に適用する";

    //    var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "deadc0de" );
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

    //    byte[] archiveBytes = CreateZipForApply(
    //        (repoEntryPath, fileContent)
    //    );
    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilesResult(
    //            [repoEntryPath],
    //            archiveBytes,
    //            archiveBytes.Length,
    //            "application/zip",
    //            "apply-trk.zip",
    //            "\"apply-trk-etag\"",
    //            false
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 1 &&
    //                request.Paths[0] == repoEntryPath &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    fileEntryServiceMock
    //        .Setup( service => service.GetEntriesAsync() )
    //        .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( Array.Empty<FileEntry>() ) );
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );

    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
    //    zipServiceMock
    //        .Setup( service => service.AddEntry(
    //            It.Is<string>( value => string.Equals( value, trkPath, StringComparison.OrdinalIgnoreCase ) ),
    //            "Localization/Example.lua",
    //            It.Is<string>( value => string.Equals(
    //                value,
    //                Path.Combine(
    //                    _tempDir,
    //                    "DCSWorld",
    //                    "Mods",
    //                    "aircraft",
    //                    "A10C",
    //                    "Missions",
    //                    "EN",
    //                    "Example.trk",
    //                    "Localization",
    //                    "Example.lua"
    //                ),
    //                StringComparison.OrdinalIgnoreCase ) )
    //        ) )
    //        .Returns( Result.Ok );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var fileNode = FindNodeByPath( aircraftRoot, "A10C", "Missions", "EN", "Example.trk", "Localization", "Example.lua" );
    //    Assert.NotNull( fileNode );
    //    fileNode!.CheckState = true;

    //    Assert.True( viewModel.CanApply );

    //    await viewModel.Apply();

    //    var manifestPath = Path.Combine( _tempDir, "manifest.json" );
    //    Assert.False( File.Exists( manifestPath ) );

    //    var expectedTranslationPath = Path.Combine(
    //        _tempDir,
    //        "DCSWorld",
    //        "Mods",
    //        "aircraft",
    //        "A10C",
    //        "Missions",
    //        "EN",
    //        "Example.trk",
    //        "Localization",
    //        "Example.lua"
    //    );
    //    Assert.True( File.Exists( expectedTranslationPath ) );
    //    Assert.Equal( fileContent, File.ReadAllText( expectedTranslationPath, Encoding.UTF8 ) );
    //    zipServiceMock.Verify( service => service.AddEntry(
    //        It.Is<string>( value => string.Equals( value, trkPath, StringComparison.OrdinalIgnoreCase ) ),
    //        "Localization/Example.lua",
    //        It.IsAny<string>()
    //    ), Times.Once );
    //    Assert.Contains( snackbarMessages, message => message == "適用完了 成功:1 件 失敗:0 件" );
    //    Assert.Equal( 100, viewModel.AppliedProgress );
    //    Assert.True( viewModel.CanApply );

    //    apiServiceMock.VerifyAll();
    //    zipServiceMock.VerifyAll();
    //}

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

        var downloadResult = Result.Fail<ApiDownloadFilePathsResult>( "network failure" );

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
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( Array.Empty<FileEntry>() ) );
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
            loggingServiceMock.Object,
            applyWorkflowService.Object
        );
        var fileEntryWatcherLifecycle = new FileEntryWatcherLifecycle(
            appSettingsServiceMock.Object,
            fileEntryServiceMock.Object,
            loggingServiceMock.Object
        );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
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

        Assert.Contains( "ダウンロードURLの取得に失敗しました", snackbarMessages );
        var expectedFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Example.lua" );
        Assert.False( File.Exists( expectedFilePath ) );

        apiServiceMock.VerifyAll();
    }

    ///// <summary>一部のファイルがダウンロード失敗した場合に失敗件数を通知することを確認する。</summary>
    //[StaFact]
    //public async Task Downloadを呼び出すと一部のファイル保存に失敗すると失敗分を通知する() {
    //    var appSettings = new AppSettings { TranslateFileDir = _tempDir };
    //    const string validRepoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Valid.lua";
    //    const string invalidRepoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Broken.lua";
    //    const string validContent = "正常系";

    //    var repoEntries = new List<FileEntry> {
    //        new RepoFileEntry( "Valid.lua", validRepoEntryPath, false, repoSha: "deadbeef" ),
    //        new RepoFileEntry( "Broken.lua", invalidRepoEntryPath, false, repoSha: "badd00d" )
    //    };
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( repoEntries );

    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilePathsResult(
    //            new[]
    //            {
    //                new ApiDownloadFilePathsItem( "https://example.test/raw/valid.lua", validRepoEntryPath ),
    //                new ApiDownloadFilePathsItem( "https://example.test/raw/broken.lua", invalidRepoEntryPath )
    //            },
    //            "\"etag-value\""
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilePathsAsync(
    //            It.Is<ApiDownloadFilePathsRequest>( request =>
    //                request.Paths.Count == 2 &&
    //                request.Paths.Contains( validRepoEntryPath ) &&
    //                request.Paths.Contains( invalidRepoEntryPath ) &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var validNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Valid.lua" );
    //    var invalidNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Broken.lua" );
    //    Assert.NotNull( validNode );
    //    Assert.NotNull( invalidNode );
    //    validNode!.CheckState = true;
    //    invalidNode!.CheckState = true;

    //    Assert.True( viewModel.CanDownload );

    //    await viewModel.Download();

    //    var validFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Valid.lua" );
    //    var invalidFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Broken.lua" );
    //    Assert.True( File.Exists( validFilePath ) );
    //    Assert.Equal( validContent, File.ReadAllText( validFilePath, Encoding.UTF8 ) );
    //    Assert.False( File.Exists( invalidFilePath ) );
    //    Assert.Contains( snackbarMessages, message => message == "一部のファイルの保存に失敗しました (1/2)" );
    //    Assert.Equal( 100, viewModel.DownloadedProgress );
    //    Assert.True( viewModel.CanDownload );

    //    apiServiceMock.VerifyAll();
    //}

    ///// <summary>一部のファイルで検証が失敗した場合に成功分のみが保存されることを確認する。</summary>
    //[StaFact]
    //public async Task Downloadを呼び出すと一部のファイル保存に失敗すると失敗分を通知する() {
    //    var appSettings = new AppSettings { TranslateFileDir = _tempDir };
    //    const string validRepoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Valid.lua";
    //    const string invalidRepoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Broken.lua";
    //    const string validContent = "正常系";

    //    var repoEntries = new List<FileEntry> {
    //        new RepoFileEntry( "Valid.lua", validRepoEntryPath, false, repoSha: "deadbeef" ),
    //        new RepoFileEntry( "Broken.lua", invalidRepoEntryPath, false, repoSha: "badd00d" )
    //    };
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( repoEntries );

    //    byte[] archiveBytes = CreateZipWithManifest(
    //        new ManifestEntryTestData( validRepoEntryPath, validContent ),
    //        new ManifestEntryTestData( invalidRepoEntryPath, "破損データ", DeclaredSize: 1 )
    //    );
    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilesResult(
    //            [validRepoEntryPath, invalidRepoEntryPath],
    //            archiveBytes,
    //            archiveBytes.Length,
    //            "application/zip",
    //            "test.zip",
    //            "\"etag-value\"",
    //            false
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 2 &&
    //                request.Paths.Contains( validRepoEntryPath ) &&
    //                request.Paths.Contains( invalidRepoEntryPath ) &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var validNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Valid.lua" );
    //    var invalidNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Broken.lua" );
    //    Assert.NotNull( validNode );
    //    Assert.NotNull( invalidNode );
    //    validNode!.CheckState = true;
    //    invalidNode!.CheckState = true;

    //    Assert.True( viewModel.CanDownload );

    //    await viewModel.Download();

    //    var validFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Valid.lua" );
    //    var invalidFilePath = Path.Combine( _tempDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Broken.lua" );
    //    Assert.True( File.Exists( validFilePath ) );
    //    Assert.Equal( validContent, File.ReadAllText( validFilePath, Encoding.UTF8 ) );
    //    Assert.False( File.Exists( invalidFilePath ) );
    //    Assert.Contains( snackbarMessages, message => message == "一部のファイルの保存に失敗しました (1/2)" );
    //    Assert.Equal( 100, viewModel.DownloadedProgress );
    //    Assert.True( viewModel.CanDownload );

    //    apiServiceMock.VerifyAll();
    //}

    /// <summary>変更が無い場合にダウンロードをスキップすることを確認する。</summary>
    [StaFact]
    public async Task Downloadを呼び出すと変更が無い場合はダウンロードをスキップする() {
        var appSettings = new AppSettings { TranslateFileDir = _tempDir };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";

        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "feedface" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        var downloadResult = Result.Ok(
            new ApiDownloadFilePathsResult( Array.Empty<ApiDownloadFilePathsItem>(), "\"etag-value\"" )
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
            loggingServiceMock.Object,
            applyWorkflowService.Object
        );
        var fileEntryWatcherLifecycle = new FileEntryWatcherLifecycle(
            appSettingsServiceMock.Object,
            fileEntryServiceMock.Object,
            loggingServiceMock.Object
        );

        var viewModel = new DownloadViewModel(
            apiServiceMock.Object,
            appSettingsServiceMock.Object,
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

    ///// <summary>Applyを呼び出すとRepoOnlyエントリを miz へ適用する。</summary>
    //[StaFact]
    //public async Task Applyを呼び出すとRepoOnlyエントリをmizへ適用する() {
    //    var sourceRoot = Path.Combine( _tempDir, "AircraftSource" );
    //    var mizDirectory = Path.Combine( sourceRoot, "A10C", "Missions", "EN" );
    //    Directory.CreateDirectory( mizDirectory );
    //    var mizPath = Path.Combine( mizDirectory, "Example.miz" );
    //    File.WriteAllBytes( mizPath, [] );

    //    var appSettings = new AppSettings
    //    {
    //        TranslateFileDir = _tempDir,
    //        SourceAircraftDir = sourceRoot
    //    };
    //    const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.miz/Localization/Example.lua";
    //    const string fileContent = "miz内に適用する";

    //    var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "cafefade" );
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

    //    byte[] archiveBytes = CreateZipForApply(
    //        (repoEntryPath, fileContent)
    //    );
    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilesResult(
    //            [repoEntryPath],
    //            archiveBytes,
    //            archiveBytes.Length,
    //            "application/zip",
    //            "apply.zip",
    //            "\"apply-etag\"",
    //            false
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 1 &&
    //                request.Paths[0] == repoEntryPath &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );

    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
    //    zipServiceMock
    //        .Setup( service => service.AddEntry(
    //            It.Is<string>( value => string.Equals( value, mizPath, StringComparison.OrdinalIgnoreCase ) ),
    //            "Localization/Example.lua",
    //            It.Is<string>( value => string.Equals(
    //                value,
    //                Path.Combine(
    //                    _tempDir,
    //                    "DCSWorld",
    //                    "Mods",
    //                    "aircraft",
    //                    "A10C",
    //                    "Missions",
    //                    "EN",
    //                    "Example.miz",
    //                    "Localization",
    //                    "Example.lua"
    //                ),
    //                StringComparison.OrdinalIgnoreCase ) )
    //        ) )
    //        .Returns( Result.Ok );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var fileNode = FindNodeByPath( aircraftRoot, "A10C", "Missions", "EN", "Example.miz", "Localization", "Example.lua" );
    //    Assert.NotNull( fileNode );
    //    fileNode!.CheckState = true;

    //    Assert.True( viewModel.CanApply );

    //    await viewModel.Apply();

    //    var manifestPath = Path.Combine( _tempDir, "manifest.json" );
    //    Assert.False( File.Exists( manifestPath ) );

    //    var expectedTranslationPath = Path.Combine(
    //        _tempDir,
    //        "DCSWorld",
    //        "Mods",
    //        "aircraft",
    //        "A10C",
    //        "Missions",
    //        "EN",
    //        "Example.miz",
    //        "Localization",
    //        "Example.lua"
    //    );
    //    Assert.True( File.Exists( expectedTranslationPath ) );
    //    Assert.Equal( fileContent, File.ReadAllText( expectedTranslationPath, Encoding.UTF8 ) );
    //    zipServiceMock.Verify( service => service.AddEntry(
    //        It.Is<string>( value => string.Equals( value, mizPath, StringComparison.OrdinalIgnoreCase ) ),
    //        "Localization/Example.lua",
    //        It.IsAny<string>()
    //    ), Times.Once );
    //    Assert.Contains( snackbarMessages, message => message == "適用完了 成功:1 件 失敗:0 件" );
    //    Assert.Equal( 100, viewModel.AppliedProgress );
    //    Assert.True( viewModel.CanApply );

    //    apiServiceMock.VerifyAll();
    //    zipServiceMock.VerifyAll();
    //}

    ///// <summary>Applyを呼び出すとmizを含まないエントリはSourceDir配下へコピーする。</summary>
    //[StaFact]
    //public async Task Applyを呼び出すとmizを含まないエントリをSourceDirへ保存する() {
    //    var sourceRoot = Path.Combine( _tempDir, "AircraftSourcePlain" );
    //    Directory.CreateDirectory( sourceRoot );

    //    var appSettings = new AppSettings
    //    {
    //        TranslateFileDir = _tempDir,
    //        SourceAircraftDir = sourceRoot
    //    };
    //    const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";
    //    const string fileContent = "mizなしファイルを保存する";

    //    var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "plaincafe" );
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

    //    byte[] archiveBytes = CreateZipForApply(
    //        (repoEntryPath, fileContent)
    //    );
    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilesResult(
    //            [repoEntryPath],
    //            archiveBytes,
    //            archiveBytes.Length,
    //            "application/zip",
    //            "apply-plain.zip",
    //            "\"apply-plain-etag\"",
    //            false
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 1 &&
    //                request.Paths[0] == repoEntryPath &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var fileNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Example.lua" );
    //    Assert.NotNull( fileNode );
    //    fileNode!.CheckState = true;

    //    Assert.True( viewModel.CanApply );

    //    await viewModel.Apply();

    //    var destinationPath = Path.Combine(
    //        sourceRoot,
    //        "A10C",
    //        "L10N",
    //        "Example.lua"
    //    );
    //    Assert.True( File.Exists( destinationPath ) );
    //    Assert.Equal( fileContent, File.ReadAllText( destinationPath, Encoding.UTF8 ) );

    //    var translationPath = Path.Combine(
    //        _tempDir,
    //        "DCSWorld",
    //        "Mods",
    //        "aircraft",
    //        "A10C",
    //        "L10N",
    //        "Example.lua"
    //    );
    //    Assert.True( File.Exists( translationPath ) );

    //    Assert.Contains( snackbarMessages, message => message == "適用完了 成功:1 件 失敗:0 件" );
    //    Assert.Equal( 100, viewModel.AppliedProgress );
    //    Assert.True( viewModel.CanApply );

    //    apiServiceMock.VerifyAll();
    //    zipServiceMock.VerifyAll();
    //}

    ///// <summary>Applyを呼び出すとUserMissions配下の miz を適用する。</summary>
    //[StaFact]
    //public async Task Applyを呼び出すとUserMissions配下のmizを適用する() {
    //    var sourceRoot = Path.Combine( _tempDir, "UserMissionSource" );
    //    var mizDirectory = Path.Combine( sourceRoot, "MyMissions" );
    //    Directory.CreateDirectory( mizDirectory );
    //    var mizPath = Path.Combine( mizDirectory, "SampleMission.miz" );
    //    File.WriteAllBytes( mizPath, [] );

    //    var appSettings = new AppSettings
    //    {
    //        TranslateFileDir = _tempDir,
    //        SourceUserMissionDir = sourceRoot
    //    };
    //    const string repoEntryPath = "UserMissions/MyMissions/SampleMission.miz/Localization/Example.lua";
    //    const string fileContent = "ユーザーミッション miz に適用する";

    //    var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "userfeed" );
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

    //    byte[] archiveBytes = CreateZipForApply(
    //        (repoEntryPath, fileContent)
    //    );
    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilesResult(
    //            [repoEntryPath],
    //            archiveBytes,
    //            archiveBytes.Length,
    //            "application/zip",
    //            "apply-user-missions.zip",
    //            "\"user-missions-etag\"",
    //            false
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 1 &&
    //                request.Paths[0] == repoEntryPath &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );

    //    var expectedTranslationPath = Path.Combine(
    //        _tempDir,
    //        "UserMissions",
    //        "MyMissions",
    //        "SampleMission.miz",
    //        "Localization",
    //        "Example.lua"
    //    );
    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
    //    zipServiceMock
    //        .Setup( service => service.AddEntry(
    //            It.Is<string>( value => string.Equals( value, mizPath, StringComparison.OrdinalIgnoreCase ) ),
    //            "Localization/Example.lua",
    //            It.Is<string>( value => string.Equals( value, expectedTranslationPath, StringComparison.OrdinalIgnoreCase ) )
    //        ) )
    //        .Returns( Result.Ok );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var userMissionIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.UserMissions )
    //        .index;
    //    viewModel.SelectedTabIndex = userMissionIndex;

    //    var userMissionRoot = viewModel.Tabs[userMissionIndex].Root;
    //    var fileNode = FindNodeByPath( userMissionRoot, "MyMissions", "SampleMission.miz", "Localization", "Example.lua" );
    //    Assert.NotNull( fileNode );
    //    fileNode!.CheckState = true;

    //    Assert.True( viewModel.CanApply );

    //    await viewModel.Apply();

    //    Assert.True( File.Exists( expectedTranslationPath ) );
    //    Assert.Equal( fileContent, File.ReadAllText( expectedTranslationPath, Encoding.UTF8 ) );
    //    zipServiceMock.VerifyAll();
    //    Assert.Contains( snackbarMessages, message => message == "適用完了 成功:1 件 失敗:0 件" );
    //    Assert.Equal( 100, viewModel.AppliedProgress );
    //    Assert.True( viewModel.CanApply );

    //    apiServiceMock.VerifyAll();
    //}

    ///// <summary>Applyを呼び出すと一部の miz 適用に失敗した場合は失敗件数を通知する。</summary>
    //[StaFact]
    //public async Task Applyを呼び出すと一部のmiz適用に失敗すると失敗件数を通知する() {
    //    var sourceRoot = Path.Combine( _tempDir, "AircraftSourcePartial" );
    //    var successMizDirectory = Path.Combine( sourceRoot, "A10C", "Missions", "EN" );
    //    Directory.CreateDirectory( successMizDirectory );
    //    var successMizPath = Path.Combine( successMizDirectory, "Success.miz" );
    //    File.WriteAllBytes( successMizPath, [] );

    //    var appSettings = new AppSettings
    //    {
    //        TranslateFileDir = _tempDir,
    //        SourceAircraftDir = sourceRoot
    //    };
    //    const string successPath = "DCSWorld/Mods/aircraft/A10C/Missions/EN/Success.miz/Localization/Success.lua";
    //    const string missingMizPath = "DCSWorld/Mods/aircraft/A10C/Missions/EN/Missing.miz/Localization/Missing.lua";

    //    var repoEntries = new List<FileEntry> {
    //        new RepoFileEntry( "Success.lua", successPath, false, repoSha: "deadbeef" ),
    //        new RepoFileEntry( "Missing.lua", missingMizPath, false, repoSha: "badd00d" )
    //    };
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( repoEntries );

    //    byte[] archiveBytes = CreateZipForApply(
    //        (successPath, "成功データ"),
    //        (missingMizPath, "失敗データ")
    //    );
    //    var downloadResult = Result.Ok(
    //        new ApiDownloadFilesResult(
    //            [successPath, missingMizPath],
    //            archiveBytes,
    //            archiveBytes.Length,
    //            "application/zip",
    //            "apply-partial.zip",
    //            "\"partial-etag\"",
    //            false
    //        )
    //    );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 2 &&
    //                request.Paths.Contains( successPath ) &&
    //                request.Paths.Contains( missingMizPath ) &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( downloadResult );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );

    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );
    //    zipServiceMock
    //        .Setup( service => service.AddEntry(
    //            It.Is<string>( value => string.Equals( value, successMizPath, StringComparison.OrdinalIgnoreCase ) ),
    //            "Localization/Success.lua",
    //            It.Is<string>( value => string.Equals(
    //                value,
    //                Path.Combine(
    //                    _tempDir,
    //                    "DCSWorld",
    //                    "Mods",
    //                    "aircraft",
    //                    "A10C",
    //                    "Missions",
    //                    "EN",
    //                    "Success.miz",
    //                    "Localization",
    //                    "Success.lua"
    //                ),
    //                StringComparison.OrdinalIgnoreCase ) )
    //        ) )
    //        .Returns( Result.Ok );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var successNode = FindNodeByPath( aircraftRoot, "A10C", "Missions", "EN", "Success.miz", "Localization", "Success.lua" );
    //    var missingNode = FindNodeByPath( aircraftRoot, "A10C", "Missions", "EN", "Missing.miz", "Localization", "Missing.lua" );
    //    Assert.NotNull( successNode );
    //    Assert.NotNull( missingNode );
    //    successNode!.CheckState = true;
    //    missingNode!.CheckState = true;

    //    Assert.True( viewModel.CanApply );

    //    await viewModel.Apply();

    //    var expectedSuccessTranslationPath = Path.Combine(
    //        _tempDir,
    //        "DCSWorld",
    //        "Mods",
    //        "aircraft",
    //        "A10C",
    //        "Missions",
    //        "EN",
    //        "Success.miz",
    //        "Localization",
    //        "Success.lua"
    //    );
    //    Assert.True( File.Exists( expectedSuccessTranslationPath ) );

    //    var expectedMissingTranslationPath = Path.Combine(
    //        _tempDir,
    //        "DCSWorld",
    //        "Mods",
    //        "aircraft",
    //        "A10C",
    //        "Missions",
    //        "EN",
    //        "Missing.miz",
    //        "Localization",
    //        "Missing.lua"
    //    );
    //    Assert.True( File.Exists( expectedMissingTranslationPath ) );

    //    zipServiceMock.Verify( service => service.AddEntry(
    //        It.Is<string>( value => string.Equals( value, successMizPath, StringComparison.OrdinalIgnoreCase ) ),
    //        "Localization/Success.lua",
    //        It.IsAny<string>()
    //    ), Times.Once );
    //    zipServiceMock.VerifyNoOtherCalls();

    //    Assert.Contains( snackbarMessages, message => message == $"圧縮ファイルが存在しません: {missingMizPath}" );
    //    Assert.Contains( snackbarMessages, message => message == "適用完了 成功:1 件 失敗:1 件" );
    //    Assert.Equal( 100, viewModel.AppliedProgress );
    //    Assert.True( viewModel.CanApply );

    //    apiServiceMock.VerifyAll();
    //}

    ///// <summary>Applyを呼び出すとリポジトリ取得に失敗した場合は処理を中断する。</summary>
    //[StaFact]
    //public async Task Applyを呼び出すとリポジトリ取得に失敗すると処理を中断する() {
    //    var sourceRoot = Path.Combine( _tempDir, "AircraftSourceFailure" );
    //    Directory.CreateDirectory( sourceRoot );

    //    var appSettings = new AppSettings
    //    {
    //        TranslateFileDir = _tempDir,
    //        SourceAircraftDir = sourceRoot
    //    };
    //    const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/Missions/EN/Failure.miz/Localization/Failure.lua";

    //    var repoEntry = new RepoFileEntry( "Failure.lua", repoEntryPath, false, repoSha: "facefeed" );
    //    var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

    //    var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
    //    apiServiceMock
    //        .Setup( service => service.GetTreeAsync( It.IsAny<CancellationToken>() ) )
    //        .ReturnsAsync( treeResult );
    //    apiServiceMock
    //        .Setup( service => service.DownloadFilesAsync(
    //            It.Is<ApiDownloadFilesRequest>( request =>
    //                request.Paths.Count == 1 &&
    //                request.Paths[0] == repoEntryPath &&
    //                request.ETag == null
    //            ),
    //            It.IsAny<CancellationToken>()
    //        ) )
    //        .ReturnsAsync( Result.Fail<ApiDownloadFilesResult>( "network failure" ) );

    //    var dispatcherServiceMock = new Mock<IDispatcherService>();
    //    dispatcherServiceMock
    //        .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
    //        .Returns<Func<Task>>( func => func() );

    //    var appSettingsServiceMock = new Mock<IAppSettingsService>();
    //    appSettingsServiceMock
    //        .SetupGet( service => service.Settings )
    //        .Returns( appSettings );

    //    var fileEntryServiceMock = new Mock<IFileEntryService>();
    //    var loggingServiceMock = new Mock<ILoggingService>();
    //    var snackbarMessages = new List<string>();
    //    var snackbarMessageQueueMock = new Mock<ISnackbarMessageQueue>();
    //    var snackbarServiceMock = new Mock<ISnackbarService>();
    //    snackbarServiceMock
    //        .SetupGet( service => service.MessageQueue )
    //        .Returns( snackbarMessageQueueMock.Object );
    //    snackbarServiceMock
    //        .Setup( service => service.Show(
    //            It.IsAny<string>(),
    //            It.IsAny<string?>(),
    //            It.IsAny<Action?>(),
    //            It.IsAny<object?>(),
    //            It.IsAny<TimeSpan?>()
    //        ) )
    //        .Callback<string, string?, Action?, object?, TimeSpan?>(
    //            ( message, _, _, _, _ ) => snackbarMessages.Add( message )
    //        );

    //    var systemServiceMock = new Mock<ISystemService>( MockBehavior.Strict );
    //    var zipServiceMock = new Mock<IZipService>( MockBehavior.Strict );

    //    var viewModel = new DownloadViewModel(
    //        apiServiceMock.Object,
    //        appSettingsServiceMock.Object,
    //        dispatcherServiceMock.Object,
    //        fileEntryServiceMock.Object,
    //        loggingServiceMock.Object,
    //        snackbarServiceMock.Object,
    //        systemServiceMock.Object,
    //        zipServiceMock.Object
    //    );

    //    await viewModel.Fetch();
    //    var aircraftIndex = viewModel.Tabs
    //        .Select( ( tab, index ) => ( tab, index ) )
    //        .First( pair => pair.tab.TabType == CategoryType.Aircraft )
    //        .index;
    //    viewModel.SelectedTabIndex = aircraftIndex;

    //    var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
    //    var fileNode = FindNodeByPath( aircraftRoot, "A10C", "Missions", "EN", "Failure.miz", "Localization", "Failure.lua" );
    //    Assert.NotNull( fileNode );
    //    fileNode!.CheckState = true;

    //    Assert.True( viewModel.CanApply );

    //    await viewModel.Apply();

    //    zipServiceMock.VerifyNoOtherCalls();
    //    Assert.DoesNotContain( snackbarMessages, message => message.StartsWith( "適用完了", StringComparison.Ordinal ) );
    //    Assert.Contains( snackbarMessages, message => message == "リポジトリからの取得に失敗しました" );
    //    Assert.Equal( 0, viewModel.AppliedProgress );
    //    Assert.True( viewModel.CanApply );

    //    apiServiceMock.VerifyAll();
    //}

    private static IFileEntryViewModel? FindNodeByPath( IFileEntryViewModel root, params string[] segments ) {
        IFileEntryViewModel? current = root;
        foreach(var segment in segments) {
            current = current?.Children.FirstOrDefault( child => string.Equals( child?.Name, segment, StringComparison.Ordinal ) );
            if(current is null) return null;
        }
        return current;
    }

    ///// <summary>適用テスト向けにシンプルなZIPアーカイブを作成する。</summary>
    //private static byte[] CreateZipForApply( params (string Path, string Content)[] entries ) =>
    //    CreateZipWithManifest( entries.Select( entry => new ManifestEntryTestData( entry.Path, entry.Content ) ).ToArray() );

    //private sealed record ManifestEntryTestData( string Path, string Content, long? DeclaredSize = null, string? DeclaredHash = null );

    //private static byte[] CreateZipWithManifest( params ManifestEntryTestData[] entries ) {
    //    using var stream = new MemoryStream();
    //    using(var archive = new ZipArchive( stream, ZipArchiveMode.Create, leaveOpen: true )) {
    //        var manifestFiles = new List<object>();
    //        foreach(var entryData in entries) {
    //            var entry = archive.CreateEntry( entryData.Path );
    //            using var entryStream = entry.Open();
    //            var contentBytes = Encoding.UTF8.GetBytes( entryData.Content );
    //            entryStream.Write( contentBytes, 0, contentBytes.Length );

    //            var declaredSize = entryData.DeclaredSize ?? contentBytes.LongLength;
    //            var declaredHash = entryData.DeclaredHash ?? Convert.ToHexString( SHA256.HashData( contentBytes ) );
    //            manifestFiles.Add( new { path = entryData.Path, size = declaredSize, sha256 = declaredHash } );
    //        }

    //        var manifestEntry = archive.CreateEntry( "manifest.json" );
    //        using var writer = new StreamWriter( manifestEntry.Open(), Encoding.UTF8 );
    //        var manifest = new {
    //            version = 1,
    //            generatedAt = DateTimeOffset.UtcNow.ToString( "O" ),
    //            files = manifestFiles
    //        };
    //        writer.Write( JsonSerializer.Serialize( manifest ) );
    //    }
    //    return stream.ToArray();
    //}
}