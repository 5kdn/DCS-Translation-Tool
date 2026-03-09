using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

/// <summary>DownloadWorkflowService の適用先解決を検証するテストを提供する。</summary>
public sealed class DownloadWorkflowServiceTests : IDisposable {
    private readonly string _tempDir = Path.Combine( Path.GetTempPath(), $"DownloadWorkflowServiceTests_{Guid.NewGuid():N}" );

    public DownloadWorkflowServiceTests() {
        Directory.CreateDirectory( _tempDir );
    }

    [Fact]
    public async Task ApplyAsyncはApplyWorkflowServiceへ委譲する() {
        var apiService = new Mock<IApiService>( MockBehavior.Strict );
        var logger = new Mock<ILoggingService>();
        var applyWorkflowService = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        var sut = new DownloadWorkflowService( apiService.Object, logger.Object, applyWorkflowService.Object );

        var targetEntries = Array.Empty<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>();
        applyWorkflowService
            .Setup( service => service.ApplyAsync(
                targetEntries,
                "root",
                "root\\",
                "translate",
                "translate\\",
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>()
            ) )
            .ReturnsAsync( true );

        var result = await sut.ApplyAsync(
            targetEntries,
            "root",
            "root\\",
            "translate",
            "translate\\",
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        Assert.True( result );
        applyWorkflowService.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncはDcsWorldInstallDirからAircraft適用先を解決する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        Directory.CreateDirectory( Path.Combine( dcsWorldInstallDir, "Mods", "aircraft" ) );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            false,
            null,
            false,
            null,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                Path.Combine( dcsWorldInstallDir, "Mods", "aircraft" ),
                EnsureSeparator( Path.Combine( dcsWorldInstallDir, "Mods", "aircraft" ) ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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

    [Fact]
    public async Task ExecuteApplyAsyncは外部Aircraft保存先を機体名ごとに解決する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        Directory.CreateDirectory( dcsWorldInstallDir );
        var externalRootDir = Path.Combine( _tempDir, "ExternalAircraft" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            true,
            externalRootDir,
            false,
            null,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                Path.Combine( externalRootDir, "A10C翻訳", "Mods", "aircraft" ),
                EnsureSeparator( Path.Combine( externalRootDir, "A10C翻訳", "Mods", "aircraft" ) ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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

    [Fact]
    public async Task ExecuteApplyAsyncは外部Aircraft保存先に無いmizをDcsWorldInstallDirから補完する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.miz/Localization/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceMizPath = Path.Combine( dcsWorldInstallDir, "Mods", "aircraft", "A10C", "Missions", "EN", "Example.miz" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceMizPath )! );
        await File.WriteAllTextAsync( sourceMizPath, "source-miz", TestContext.Current.CancellationToken );

        var externalRootDir = Path.Combine( _tempDir, "ExternalAircraft" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var externalAircraftRoot = Path.Combine( externalRootDir, "A10C翻訳", "Mods", "aircraft" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            true,
            externalRootDir,
            false,
            null,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                externalAircraftRoot,
                EnsureSeparator( externalAircraftRoot ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
        var copiedMizPath = Path.Combine( externalAircraftRoot, "A10C", "Missions", "EN", "Example.miz" );
        Assert.True( File.Exists( copiedMizPath ) );
        Assert.Equal( await File.ReadAllTextAsync( sourceMizPath, TestContext.Current.CancellationToken ), await File.ReadAllTextAsync( copiedMizPath, TestContext.Current.CancellationToken ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncは外部Campaign保存先をキャンペーン名ごとに解決する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/campaigns/RedFlag/L10N/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.DlcCampaigns, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        Directory.CreateDirectory( dcsWorldInstallDir );
        var externalRootDir = Path.Combine( _tempDir, "ExternalCampaigns" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            false,
            null,
            true,
            externalRootDir,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                Path.Combine( externalRootDir, "RedFlag翻訳", "Mods", "campaigns" ),
                EnsureSeparator( Path.Combine( externalRootDir, "RedFlag翻訳", "Mods", "campaigns" ) ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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

    [Fact]
    public async Task ExecuteApplyAsyncは外部Campaign保存先に無いmizをDcsWorldInstallDirから補完する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/campaigns/RedFlag/Missions/EN/Example.miz/Localization/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.DlcCampaigns, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceMizPath = Path.Combine( dcsWorldInstallDir, "Mods", "campaigns", "RedFlag", "Missions", "EN", "Example.miz" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceMizPath )! );
        await File.WriteAllTextAsync( sourceMizPath, "source-campaign-miz", TestContext.Current.CancellationToken );

        var externalRootDir = Path.Combine( _tempDir, "ExternalCampaigns" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var externalCampaignRoot = Path.Combine( externalRootDir, "RedFlag翻訳", "Mods", "campaigns" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            false,
            null,
            true,
            externalRootDir,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                externalCampaignRoot,
                EnsureSeparator( externalCampaignRoot ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
        var copiedMizPath = Path.Combine( externalCampaignRoot, "RedFlag", "Missions", "EN", "Example.miz" );
        Assert.True( File.Exists( copiedMizPath ) );
        Assert.Equal( await File.ReadAllTextAsync( sourceMizPath, TestContext.Current.CancellationToken ), await File.ReadAllTextAsync( copiedMizPath, TestContext.Current.CancellationToken ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncは外部Aircraft保存先に無いtrkをDcsWorldInstallDirから補完する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.trk/Localization/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceTrkPath = Path.Combine( dcsWorldInstallDir, "Mods", "aircraft", "A10C", "Missions", "EN", "Example.trk" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceTrkPath )! );
        await File.WriteAllTextAsync( sourceTrkPath, "source-trk", TestContext.Current.CancellationToken );

        var externalRootDir = Path.Combine( _tempDir, "ExternalAircraft" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var externalAircraftRoot = Path.Combine( externalRootDir, "A10C翻訳", "Mods", "aircraft" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            true,
            externalRootDir,
            false,
            null,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                externalAircraftRoot,
                EnsureSeparator( externalAircraftRoot ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
        var copiedTrkPath = Path.Combine( externalAircraftRoot, "A10C", "Missions", "EN", "Example.trk" );
        Assert.True( File.Exists( copiedTrkPath ) );
        Assert.Equal( await File.ReadAllTextAsync( sourceTrkPath, TestContext.Current.CancellationToken ), await File.ReadAllTextAsync( copiedTrkPath, TestContext.Current.CancellationToken ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncは外部Campaign保存先に無いtrkをDcsWorldInstallDirから補完する() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/campaigns/RedFlag/Missions/EN/Example.trk/Localization/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.DlcCampaigns, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceTrkPath = Path.Combine( dcsWorldInstallDir, "Mods", "campaigns", "RedFlag", "Missions", "EN", "Example.trk" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceTrkPath )! );
        await File.WriteAllTextAsync( sourceTrkPath, "source-campaign-trk", TestContext.Current.CancellationToken );

        var externalRootDir = Path.Combine( _tempDir, "ExternalCampaigns" );
        var translateDir = Path.Combine( _tempDir, "Translate" );
        var externalCampaignRoot = Path.Combine( externalRootDir, "RedFlag翻訳", "Mods", "campaigns" );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            false,
            null,
            true,
            externalRootDir,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                externalCampaignRoot,
                EnsureSeparator( externalCampaignRoot ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
        var copiedTrkPath = Path.Combine( externalCampaignRoot, "RedFlag", "Missions", "EN", "Example.trk" );
        Assert.True( File.Exists( copiedTrkPath ) );
        Assert.Equal( await File.ReadAllTextAsync( sourceTrkPath, TestContext.Current.CancellationToken ), await File.ReadAllTextAsync( copiedTrkPath, TestContext.Current.CancellationToken ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncは外部Aircraft保存無効時にmizを補完しない() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/Missions/EN/Example.miz/Localization/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceMizPath = Path.Combine( dcsWorldInstallDir, "Mods", "aircraft", "A10C", "Missions", "EN", "Example.miz" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceMizPath )! );
        await File.WriteAllTextAsync( sourceMizPath, "source-miz", TestContext.Current.CancellationToken );

        var translateDir = Path.Combine( _tempDir, "Translate" );
        var aircraftRoot = Path.Combine( dcsWorldInstallDir, "Mods", "aircraft" );
        Directory.CreateDirectory( aircraftRoot );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            false,
            Path.Combine( _tempDir, "ExternalAircraft" ),
            false,
            null,
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                aircraftRoot,
                EnsureSeparator( aircraftRoot ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
        Assert.False( Directory.Exists( Path.Combine( _tempDir, "ExternalAircraft" ) ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncは外部Campaign保存無効時にmizを補完しない() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/campaigns/RedFlag/Missions/EN/Example.miz/Localization/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.DlcCampaigns, targetEntry, loggerMock.Object );
        var dcsWorldInstallDir = Path.Combine( _tempDir, "DcsWorld" );
        var sourceMizPath = Path.Combine( dcsWorldInstallDir, "Mods", "campaigns", "RedFlag", "Missions", "EN", "Example.miz" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourceMizPath )! );
        await File.WriteAllTextAsync( sourceMizPath, "source-campaign-miz", TestContext.Current.CancellationToken );

        var translateDir = Path.Combine( _tempDir, "Translate" );
        var campaignsRoot = Path.Combine( dcsWorldInstallDir, "Mods", "campaigns" );
        Directory.CreateDirectory( campaignsRoot );
        var request = new ApplyExecutionRequest(
            selectedTab,
            dcsWorldInstallDir,
            null,
            false,
            null,
            false,
            Path.Combine( _tempDir, "ExternalCampaigns" ),
            translateDir );

        applyWorkflowServiceMock
            .Setup( service => service.ApplyAsync(
                It.Is<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>( entries => entries.Count == 1 ),
                campaignsRoot,
                EnsureSeparator( campaignsRoot ),
                translateDir,
                EnsureSeparator( translateDir ),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
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
        Assert.False( Directory.Exists( Path.Combine( _tempDir, "ExternalCampaigns" ) ) );
        applyWorkflowServiceMock.VerifyAll();
    }

    [Fact]
    public async Task ExecuteApplyAsyncは必要設定不足時に失敗を返す() {
        var sut = CreateSut( out var applyWorkflowServiceMock, out var loggerMock );
        var targetEntry = CreateCheckedEntry( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua" );
        var selectedTab = CreateSelectedTab( CategoryType.Aircraft, targetEntry, loggerMock.Object );
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
                It.IsAny<IReadOnlyList<DcsTranslationTool.Presentation.Wpf.UI.Interfaces.IFileEntryViewModel>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<Func<IReadOnlyList<DcsTranslationTool.Application.Contracts.ApiDownloadFilePathsItem>, Task>>(),
                It.IsAny<Func<string, Task>>(),
                It.IsAny<Func<double, Task>>(),
                It.IsAny<CancellationToken>() ),
            Times.Never );
    }

    private static DownloadWorkflowService CreateSut(
        out Mock<IApplyWorkflowService> applyWorkflowServiceMock,
        out Mock<ILoggingService> loggerMock
    ) {
        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        loggerMock = new Mock<ILoggingService>();
        applyWorkflowServiceMock = new Mock<IApplyWorkflowService>( MockBehavior.Strict );
        return new DownloadWorkflowService( apiServiceMock.Object, loggerMock.Object, applyWorkflowServiceMock.Object );
    }

    private static FileEntryViewModel CreateCheckedEntry( string entryPath ) {
        var loggerMock = new Mock<ILoggingService>();
        var entry = new FileEntryViewModel(
            new LocalFileEntry( Path.GetFileName( entryPath ), entryPath, false, "local" ) { RepoSha = "repo" },
            ChangeTypeMode.Download,
            loggerMock.Object );
        entry.CheckState = true;
        return entry;
    }

    private static TabItemViewModel CreateSelectedTab( CategoryType categoryType, FileEntryViewModel entry, ILoggingService logger ) {
        var root = new FileEntryViewModel(
            new LocalFileEntry( "root", "root", true, "local" ),
            ChangeTypeMode.Download,
            logger );
        root.Children = [entry];
        return new TabItemViewModel( categoryType, logger, root );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    private static string EnsureSeparator( string path ) =>
        path.EndsWith( Path.DirectorySeparatorChar ) ? path : path + Path.DirectorySeparatorChar;
}