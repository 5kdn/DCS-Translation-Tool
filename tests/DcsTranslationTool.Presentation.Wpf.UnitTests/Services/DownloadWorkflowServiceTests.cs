using System.Net;
using System.Text;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Services;

/// <summary>
/// <see cref="DownloadWorkflowService"/> の適用先解決と委譲を検証する。
/// </summary>
public sealed class DownloadWorkflowServiceTests : IDisposable {
    private readonly string _tempDir = Path.Combine( Path.GetTempPath(), $"DownloadWorkflowServiceTests_{Guid.NewGuid():N}" );

    /// <summary>
    /// テスト用ディレクトリを初期化する。
    /// </summary>
    public DownloadWorkflowServiceTests() {
        Directory.CreateDirectory( _tempDir );
    }

    /// <summary>
    /// 依存サービスへそのまま委譲することを検証する。
    /// </summary>
    [Fact]
    public async Task ApplyAsyncはApplyWorkflowServiceへ委譲する() {
        var apiService = new Mock<IApiService>( MockBehavior.Strict );
        var logger = new Mock<ILoggingService>();
        var applyWorkflowService = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiService.Object, logger.Object, applyWorkflowService.Object );

        var targetEntries = Array.Empty<IFileEntryViewModel>();
        applyWorkflowService
            .Setup( service => service.ApplyAsync(
                targetEntries,
                "root",
                "root\\",
                "translate",
                "translate\\",
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( true );

        var result = await sut.ApplyAsync(
            targetEntries,
            "root",
            "root\\",
            "translate",
            "translate\\",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.True( result );
        applyWorkflowService.VerifyAll();
    }

    /// <summary>
    /// カテゴリと保存方式に応じた適用先ルートを解決することを検証する。
    /// </summary>
    [Theory]
    [MemberData( nameof( GetRootResolutionCases ) )]
    public async Task ExecuteApplyAsyncはカテゴリと保存設定に応じた適用先を解決する(
        CategoryType categoryType,
        string entryPath,
        bool useExternalAircraftInjectionDir,
        string? externalAircraftInjectionDir,
        bool useExternalCampaignInjectionDir,
        string? externalCampaignInjectionDir,
        string expectedRootRelativePath
    ) {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( entryPath );
        var selectedTab = CreateSelectedTab( categoryType, loggerMock.Object, targetEntry );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var expectedRootPath = Path.Combine( _tempDir, expectedRootRelativePath );
        if(!useExternalAircraftInjectionDir && !useExternalCampaignInjectionDir) {
            Directory.CreateDirectory( expectedRootPath );
        }

        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            useExternalAircraftInjectionDir,
            externalAircraftInjectionDir is null ? null : Path.Combine( _tempDir, externalAircraftInjectionDir ),
            useExternalCampaignInjectionDir,
            externalCampaignInjectionDir is null ? null : Path.Combine( _tempDir, externalCampaignInjectionDir ),
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<IFileEntryViewModel>>( entries => entries.Count == 1 ),
                expectedRootPath,
                EnsureSeparator( expectedRootPath ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( true );

        var result = await sut.ExecuteApplyAsync(
            request,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        applyWorkflowServiceMock.VerifyAll();
    }

    /// <summary>
    /// 外部保存時に不足している miz / trk を補完することを検証する。
    /// </summary>
    [Theory]
    [MemberData( nameof( GetExternalArchiveCopyCases ) )]
    public async Task ExecuteApplyAsyncは外部保存先に無いアーカイブをDcsWorldInstallDirから補完する(
        CategoryType categoryType,
        string entryPath,
        string sourceArchiveRelativePath,
        string externalRootRelativePath,
        bool useExternalAircraftInjectionDir,
        string? externalAircraftInjectionDir,
        bool useExternalCampaignInjectionDir,
        string? externalCampaignInjectionDir
    ) {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( entryPath );
        var selectedTab = CreateSelectedTab( categoryType, loggerMock.Object, targetEntry );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceArchivePath = Path.Combine( dcsWorldInstallDir, sourceArchiveRelativePath );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceArchivePath )! );
        await File.WriteAllTextAsync( sourceArchivePath, "source-archive", TestContext.Current.CancellationToken );

        var translateDir = Path.Combine( _tempDir, "Translate" );
        var expectedRootPath = Path.Combine( _tempDir, externalRootRelativePath );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            useExternalAircraftInjectionDir,
            externalAircraftInjectionDir is null ? null : Path.Combine( _tempDir, externalAircraftInjectionDir ),
            useExternalCampaignInjectionDir,
            externalCampaignInjectionDir is null ? null : Path.Combine( _tempDir, externalCampaignInjectionDir ),
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<IFileEntryViewModel>>( entries => entries.Count == 1 ),
                expectedRootPath,
                EnsureSeparator( expectedRootPath ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( true );

        var result = await sut.ExecuteApplyAsync(
            request,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        var copiedArchivePath = Path.Combine( expectedRootPath, GetExpectedCopiedArchiveRelativePath( entryPath ) );
        Assert.True( File.Exists( copiedArchivePath ) );
        Assert.Equal(
            await File.ReadAllTextAsync( sourceArchivePath, TestContext.Current.CancellationToken ),
            await File.ReadAllTextAsync( copiedArchivePath, TestContext.Current.CancellationToken ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    /// <summary>
    /// 外部保存無効時に補完を行わないことを検証する。
    /// </summary>
    [Theory]
    [MemberData( nameof( GetDisabledExternalCopyCases ) )]
    public async Task ExecuteApplyAsyncは外部保存無効時にアーカイブを補完しない(
        CategoryType categoryType,
        string entryPath,
        string sourceArchiveRelativePath,
        string expectedRootRelativePath,
        bool useExternalAircraftInjectionDir,
        string? externalAircraftInjectionDir,
        bool useExternalCampaignInjectionDir,
        string? externalCampaignInjectionDir,
        string disabledExternalRootRelativePath
    ) {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( entryPath );
        var selectedTab = CreateSelectedTab( categoryType, loggerMock.Object, targetEntry );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceArchivePath = Path.Combine( dcsWorldInstallDir, sourceArchiveRelativePath );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceArchivePath )! );
        await File.WriteAllTextAsync( sourceArchivePath, "source-archive", TestContext.Current.CancellationToken );

        var translateDir = Path.Combine( _tempDir, "Translate" );
        var expectedRootPath = Path.Combine( _tempDir, expectedRootRelativePath );
        Directory.CreateDirectory( expectedRootPath );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            useExternalAircraftInjectionDir,
            externalAircraftInjectionDir is null ? null : Path.Combine( _tempDir, externalAircraftInjectionDir ),
            useExternalCampaignInjectionDir,
            externalCampaignInjectionDir is null ? null : Path.Combine( _tempDir, externalCampaignInjectionDir ),
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<IFileEntryViewModel>>( entries => entries.Count == 1 ),
                expectedRootPath,
                EnsureSeparator( expectedRootPath ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( true );

        var result = await sut.ExecuteApplyAsync(
            request,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.False( Directory.Exists( Path.Combine( _tempDir, disabledExternalRootRelativePath ) ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    /// <summary>
    /// 必須設定不足時に失敗を返すことを検証する。
    /// </summary>
    [Fact]
    public async Task ExecuteApplyAsyncは必要設定不足時に失敗を返す() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, loggerMock.Object, targetEntry );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            string.Empty,
            null,
            false,
            null,
            false,
            null,
            translateDir );

        var result = await sut.ExecuteApplyAsync(
            request,
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.False( result.IsSuccess );
        Assert.Contains( result.Events, workflowEvent => workflowEvent.Message == "適用先ディレクトリを設定してください" );
        applyWorkflowServiceMock.Verify(
            service => service.ApplyAsync(
                It.IsAny<IReadOnlyList<IFileEntryViewModel>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<IReadOnlyList<ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>() ),
            Times.Never );
    }

    /// <summary>
    /// Modified 対象のみをサーバー版で翻訳ディレクトリへ同期することを検証する。
    /// </summary>
    [Fact]
    public async Task SyncModifiedFilesWithRepositoryAsyncはModified対象のみを同期する() {
        var translateDir = Path.Combine( _tempDir, "Translate" );
        const string modifiedPath = "DCSWorld/Mods/aircraft/A10C/L10N/Modified.lua";
        const string localOnlyPath = "DCSWorld/Mods/aircraft/A10C/L10N/LocalOnly.lua";
        const string repositoryContent = "repository-content";

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var applyWorkflowServiceMock = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiServiceMock.Object, loggerMock.Object, applyWorkflowServiceMock.Object );

        using var server = new TestHttpServer( repositoryContent );
        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.Is<ApiDownloadFilePathsRequest>( request =>
                    request.Paths.Count == 1 &&
                    request.Paths[0] == modifiedPath ),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( FluentResults.Result.Ok(
                new ApiDownloadFilePathsResult(
                    [new ApiDownloadFilePathsItem( server.Url, modifiedPath )],
                    "\"etag\"" ) ) );

        var result = await sut.SyncModifiedFilesWithRepositoryAsync(
            [CreateModifiedEntry( modifiedPath ), CreateLocalOnlyEntry( localOnlyPath )],
            translateDir,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.True( result );
        var modifiedFilePath = Path.Combine( translateDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Modified.lua" );
        Assert.True( File.Exists( modifiedFilePath ) );
        Assert.Equal( repositoryContent, await File.ReadAllTextAsync( modifiedFilePath, TestContext.Current.CancellationToken ) );
        var localOnlyFilePath = Path.Combine( translateDir, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "LocalOnly.lua" );
        Assert.False( File.Exists( localOnlyFilePath ) );
        apiServiceMock.VerifyAll();
    }

    /// <summary>
    /// Download では LocalOnly を API リクエストから除外することを検証する。
    /// </summary>
    [Fact]
    public async Task ExecuteDownloadAsyncはLocalOnlyを除外してダウンロードする() {
        var saveRootPath = Path.Combine( _tempDir, "Translate" );
        const string repoOnlyPath = "DCSWorld/Mods/aircraft/A10C/L10N/RepoOnly.lua";
        const string modifiedPath = "DCSWorld/Mods/aircraft/A10C/L10N/Modified.lua";
        const string localOnlyPath = "DCSWorld/Mods/aircraft/A10C/L10N/LocalOnly.lua";

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var applyWorkflowServiceMock = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiServiceMock.Object, loggerMock.Object, applyWorkflowServiceMock.Object );

        using var repoOnlyServer = new TestHttpServer( "repo-only-content" );
        using var modifiedServer = new TestHttpServer( "modified-content" );
        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.Is<ApiDownloadFilePathsRequest>( request =>
                    request.Paths.Count == 2 &&
                    request.Paths.Contains( repoOnlyPath ) &&
                    request.Paths.Contains( modifiedPath ) &&
                    !request.Paths.Contains( localOnlyPath ) ),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( FluentResults.Result.Ok(
                new ApiDownloadFilePathsResult(
                    [
                        new ApiDownloadFilePathsItem( repoOnlyServer.Url, repoOnlyPath ),
                        new ApiDownloadFilePathsItem( modifiedServer.Url, modifiedPath )
                    ],
                    "\"etag\"" ) ) );

        var selectedTab = CreateSelectedTab(
            CategoryType.Aircraft,
            loggerMock.Object,
            CreateRepoOnlyEntry( repoOnlyPath ),
            CreateModifiedEntry( modifiedPath ),
            CreateLocalOnlyEntry( localOnlyPath ) );

        var result = await sut.ExecuteDownloadAsync(
            new DownloadExecutionRequest( selectedTab, saveRootPath ),
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.True( File.Exists( Path.Combine( saveRootPath, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "RepoOnly.lua" ) ) );
        Assert.True( File.Exists( Path.Combine( saveRootPath, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Modified.lua" ) ) );
        Assert.False( File.Exists( Path.Combine( saveRootPath, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "LocalOnly.lua" ) ) );
        apiServiceMock.VerifyAll();
    }

    /// <summary>
    /// URL 取得失敗時に失敗を返すことを検証する。
    /// </summary>
    [Fact]
    public async Task SyncModifiedFilesWithRepositoryAsyncはURL取得失敗時にfalseを返す() {
        var translateDir = Path.Combine( _tempDir, "Translate" );
        const string modifiedPath = "DCSWorld/Mods/aircraft/A10C/L10N/Modified.lua";

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var applyWorkflowServiceMock = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiServiceMock.Object, loggerMock.Object, applyWorkflowServiceMock.Object );

        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.IsAny<ApiDownloadFilePathsRequest>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( FluentResults.Result.Fail<ApiDownloadFilePathsResult>( "failed" ) );

        var messages = new List<string>();
        var result = await sut.SyncModifiedFilesWithRepositoryAsync(
            [CreateModifiedEntry( modifiedPath )],
            translateDir,
            message => {
                messages.Add( message );
                return Task.CompletedTask;
            },
            TestContext.Current.CancellationToken );

        Assert.False( result );
        Assert.Contains( messages, message => message.Contains( "ダウンロードURLの取得に失敗しました", StringComparison.Ordinal ) );
        apiServiceMock.VerifyAll();
    }

    /// <summary>
    /// 保存後に対象ファイルが存在しない場合に失敗を返すことを検証する。
    /// </summary>
    [Fact]
    public async Task SyncModifiedFilesWithRepositoryAsyncは保存後に対象ファイルが存在しない場合falseを返す() {
        var translateDir = Path.Combine( _tempDir, "Translate" );
        const string modifiedPath = "DCSWorld/Mods/aircraft/A10C/L10N/Modified.lua";

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var applyWorkflowServiceMock = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiServiceMock.Object, loggerMock.Object, applyWorkflowServiceMock.Object );

        apiServiceMock
            .Setup( service => service.DownloadFilePathsAsync(
                It.IsAny<ApiDownloadFilePathsRequest>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( FluentResults.Result.Ok(
                new ApiDownloadFilePathsResult(
                    [],
                    "\"etag\"" ) ) );

        var result = await sut.SyncModifiedFilesWithRepositoryAsync(
            [CreateModifiedEntry( modifiedPath )],
            translateDir,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken );

        Assert.False( result );
        apiServiceMock.VerifyAll();
    }

    /// <summary>
    /// 適用先解決ケースを返す。
    /// </summary>
    /// <returns>理論値一覧を返す。</returns>
    public static TheoryData<CategoryType, string, bool, string?, bool, string?, string> GetRootResolutionCases() {
        return new TheoryData<CategoryType, string, bool, string?, bool, string?, string> {
            {
                CategoryType.Aircraft,
                "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua",
                false,
                null,
                false,
                null,
                Path.Combine( "DcsWorld", "Mods", "aircraft" )
            },
            {
                CategoryType.Aircraft,
                "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua",
                true,
                "ExternalAircraft",
                false,
                null,
                Path.Combine( "ExternalAircraft", "A10C翻訳", "Mods", "aircraft" )
            },
            {
                CategoryType.DlcCampaigns,
                "DCSWorld/Mods/campaigns/RedFlag/L10N/Example.lua",
                false,
                null,
                false,
                null,
                Path.Combine( "DcsWorld", "Mods", "campaigns" )
            },
            {
                CategoryType.DlcCampaigns,
                "DCSWorld/Mods/campaigns/RedFlag/L10N/Example.lua",
                false,
                null,
                true,
                "ExternalCampaigns",
                Path.Combine( "ExternalCampaigns", "RedFlag翻訳", "Mods", "campaigns" )
            },
        };
    }

    /// <summary>
    /// 外部アーカイブ補完ケースを返す。
    /// </summary>
    /// <returns>理論値一覧を返す。</returns>
    public static TheoryData<CategoryType, string, string, string, bool, string?, bool, string?> GetExternalArchiveCopyCases() {
        return new TheoryData<CategoryType, string, string, string, bool, string?, bool, string?> {
            {
                CategoryType.Aircraft,
                "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.miz/Localization/Example.lua",
                Path.Combine( "Mods", "aircraft", "A10C", "Missions", "EN", "Example.miz" ),
                Path.Combine( "ExternalAircraft", "A10C翻訳", "Mods", "aircraft" ),
                true,
                "ExternalAircraft",
                false,
                null
            },
            {
                CategoryType.Aircraft,
                "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.trk/Localization/Example.lua",
                Path.Combine( "Mods", "aircraft", "A10C", "Missions", "EN", "Example.trk" ),
                Path.Combine( "ExternalAircraft", "A10C翻訳", "Mods", "aircraft" ),
                true,
                "ExternalAircraft",
                false,
                null
            },
            {
                CategoryType.DlcCampaigns,
                "DCSWorld/Mods/campaigns/RedFlag/Missions/EN/Example.miz/Localization/Example.lua",
                Path.Combine( "Mods", "campaigns", "RedFlag", "Missions", "EN", "Example.miz" ),
                Path.Combine( "ExternalCampaigns", "RedFlag翻訳", "Mods", "campaigns" ),
                false,
                null,
                true,
                "ExternalCampaigns"
            },
            {
                CategoryType.DlcCampaigns,
                "DCSWorld/Mods/campaigns/RedFlag/Missions/EN/Example.trk/Localization/Example.lua",
                Path.Combine( "Mods", "campaigns", "RedFlag", "Missions", "EN", "Example.trk" ),
                Path.Combine( "ExternalCampaigns", "RedFlag翻訳", "Mods", "campaigns" ),
                false,
                null,
                true,
                "ExternalCampaigns"
            },
        };
    }

    /// <summary>
    /// 外部補完無効ケースを返す。
    /// </summary>
    /// <returns>理論値一覧を返す。</returns>
    public static TheoryData<CategoryType, string, string, string, bool, string?, bool, string?, string> GetDisabledExternalCopyCases() {
        return new TheoryData<CategoryType, string, string, string, bool, string?, bool, string?, string> {
            {
                CategoryType.Aircraft,
                "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.miz/Localization/Example.lua",
                Path.Combine( "Mods", "aircraft", "A10C", "Missions", "EN", "Example.miz" ),
                Path.Combine( "DcsWorld", "Mods", "aircraft" ),
                false,
                "ExternalAircraft",
                false,
                null,
                "ExternalAircraft"
            },
            {
                CategoryType.DlcCampaigns,
                "DCSWorld/Mods/campaigns/RedFlag/Missions/EN/Example.miz/Localization/Example.lua",
                Path.Combine( "Mods", "campaigns", "RedFlag", "Missions", "EN", "Example.miz" ),
                Path.Combine( "DcsWorld", "Mods", "campaigns" ),
                false,
                null,
                false,
                "ExternalCampaigns",
                "ExternalCampaigns"
            },
        };
    }

    /// <summary>
    /// テスト対象を生成する。
    /// </summary>
    /// <param name="applyWorkflowServiceMock">適用ワークフローモック。</param>
    /// <param name="loggerMock">ロガーモック。</param>
    /// <returns>生成したサービスを返す。</returns>
    private static DownloadWorkflowService CreateSut(
        out Mock<IApplyWorkflowService> applyWorkflowServiceMock,
        out Mock<ILoggingService> loggerMock
    ) {
        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        loggerMock = new Mock<ILoggingService>();
        applyWorkflowServiceMock = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        return new DownloadWorkflowService( apiServiceMock.Object, loggerMock.Object, applyWorkflowServiceMock.Object );
    }

    /// <summary>
    /// チェック済みエントリーを生成する。
    /// </summary>
    /// <param name="entryPath">エントリーパス。</param>
    /// <returns>生成したビューモデルを返す。</returns>
    private static FileEntryViewModel CreateCheckedEntry( string entryPath ) {
        var loggerMock = new Mock<ILoggingService>();
        return new FileEntryViewModel(
            new LocalFileEntry( Path.GetFileName( entryPath ), entryPath, false, "local" ) { RepoSha = "repo" },
            ChangeTypeMode.Download,
            loggerMock.Object )
        {
            CheckState = true
        };
    }

    /// <summary>
    /// Modified エントリーを生成する。
    /// </summary>
    /// <param name="entryPath">エントリーパス。</param>
    /// <returns>生成したビューモデルを返す。</returns>
    private static FileEntryViewModel CreateModifiedEntry( string entryPath ) {
        var loggerMock = new Mock<ILoggingService>();
        return new FileEntryViewModel(
            new LocalFileEntry( Path.GetFileName( entryPath ), entryPath, false, "local" ) { RepoSha = "repo" },
            ChangeTypeMode.Download,
            loggerMock.Object )
        {
            CheckState = true
        };
    }

    /// <summary>
    /// RepoOnly エントリーを生成する。
    /// </summary>
    /// <param name="entryPath">エントリーパス。</param>
    /// <returns>生成したビューモデルを返す。</returns>
    private static FileEntryViewModel CreateRepoOnlyEntry( string entryPath ) {
        var loggerMock = new Mock<ILoggingService>();
        return new FileEntryViewModel(
            new RepoFileEntry( Path.GetFileName( entryPath ), entryPath, false, "repo" ),
            ChangeTypeMode.Download,
            loggerMock.Object )
        {
            CheckState = true
        };
    }

    /// <summary>
    /// LocalOnly エントリーを生成する。
    /// </summary>
    /// <param name="entryPath">エントリーパス。</param>
    /// <returns>生成したビューモデルを返す。</returns>
    private static FileEntryViewModel CreateLocalOnlyEntry( string entryPath ) {
        var loggerMock = new Mock<ILoggingService>();
        return new FileEntryViewModel(
            new LocalFileEntry( Path.GetFileName( entryPath ), entryPath, false, "local" ),
            ChangeTypeMode.Download,
            loggerMock.Object )
        {
            CheckState = true
        };
    }

    /// <summary>
    /// 選択中タブを生成する。
    /// </summary>
    /// <param name="categoryType">カテゴリ。</param>
    /// <param name="entry">対象エントリー。</param>
    /// <param name="logger">ロガー。</param>
    /// <returns>生成したタブを返す。</returns>
    private static TabItemViewModel CreateSelectedTab( CategoryType categoryType, ILoggingService logger, params FileEntryViewModel[] entries ) {
        var root = new FileEntryViewModel(
            new LocalFileEntry( "root", "root", true, "local" ),
            ChangeTypeMode.Download,
            logger )
        {
            Children = [.. entries]
        };
        return new TabItemViewModel( categoryType, logger, root );
    }

    /// <summary>
    /// 外部補完後の相対パスを返す。
    /// </summary>
    /// <param name="entryPath">翻訳エントリーパス。</param>
    /// <returns>コピー後のアーカイブ相対パスを返す。</returns>
    private static string GetExpectedCopiedArchiveRelativePath( string entryPath ) {
        var segments = entryPath.Split( '/', StringSplitOptions.RemoveEmptyEntries );
        var archiveIndex = Array.FindIndex(
            segments,
            static segment =>
                segment.EndsWith( ".miz", StringComparison.OrdinalIgnoreCase ) ||
                segment.EndsWith( ".trk", StringComparison.OrdinalIgnoreCase ) );
        return Path.Combine( [.. segments.Skip( 3 ).Take( archiveIndex - 2 )] );
    }

    /// <summary>
    /// ディレクトリ区切り付きパスへ変換する。
    /// </summary>
    /// <param name="path">対象パス。</param>
    /// <returns>区切り付きパスを返す。</returns>
    private static string EnsureSeparator( string path ) =>
        path.EndsWith( Path.DirectorySeparatorChar ) ? path : path + Path.DirectorySeparatorChar;

    /// <summary>
    /// テスト用HTTPサーバーを表す。
    /// </summary>
    private sealed class TestHttpServer : IDisposable {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        /// <summary>
        /// 応答URLを取得する。
        /// </summary>
        public string Url { get; }
            = $"http://127.0.0.1:{GetFreePort()}/content/";

        /// <summary>
        /// 新しいテスト用HTTPサーバーを初期化する。
        /// </summary>
        /// <param name="content">返却内容。</param>
        public TestHttpServer( string content ) {
            _listener.Prefixes.Add( Url );
            _listener.Start();
            _serverTask = Task.Run( async () => {
                while(!_cts.IsCancellationRequested) {
                    try {
                        var context = await _listener.GetContextAsync();
                        var buffer = Encoding.UTF8.GetBytes( content );
                        context.Response.ContentType = "text/plain; charset=utf-8";
                        context.Response.ContentLength64 = buffer.Length;
                        await context.Response.OutputStream.WriteAsync( buffer, 0, buffer.Length, _cts.Token );
                        context.Response.Close();
                    }
                    catch(HttpListenerException) when(_cts.IsCancellationRequested) {
                        break;
                    }
                    catch(ObjectDisposedException) when(_cts.IsCancellationRequested) {
                        break;
                    }
                }
            }, _cts.Token );
        }

        /// <summary>
        /// サーバーを破棄する。
        /// </summary>
        public void Dispose() {
            _cts.Cancel();
            if(_listener.IsListening) {
                _listener.Stop();
            }
            _listener.Close();
            try {
                _serverTask.GetAwaiter().GetResult();
            }
            catch(OperationCanceledException) {
            }
            GC.SuppressFinalize( this );
        }

        /// <summary>
        /// 空きポートを取得する。
        /// </summary>
        /// <returns>空きポート番号を返す。</returns>
        private static int GetFreePort() {
            var listener = new System.Net.Sockets.TcpListener( IPAddress.Loopback, 0 );
            listener.Start();
            var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }

    /// <summary>
    /// 使用した一時ディレクトリを破棄する。
    /// </summary>
    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }
}