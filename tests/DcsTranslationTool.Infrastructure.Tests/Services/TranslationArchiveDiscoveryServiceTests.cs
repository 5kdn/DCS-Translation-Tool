using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// TranslationArchiveDiscoveryService の動作を検証する。
/// </summary>
public sealed class TranslationArchiveDiscoveryServiceTests : IDisposable {
    private readonly Mock<ILoggingService> _loggerMock = new();
    private readonly string _tempDir;

    public TranslationArchiveDiscoveryServiceTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"TranslationArchiveDiscovery_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    [Fact]
    public async Task DiscoverAsyncはmizとtrkのみを返す() {
        var dcsWorldDir = Path.Combine( _tempDir, "DCSWorld" );
        var aircraftDir = Path.Combine( dcsWorldDir, "Mods", "aircraft", "A10C", "Missions", "EN" );
        Directory.CreateDirectory( aircraftDir );

        var includedMiz = Path.Combine( aircraftDir, "Included.miz" );
        var includedTrk = Path.Combine( aircraftDir, "Included.trk" );
        var excludedTxt = Path.Combine( aircraftDir, "Excluded.txt" );
        File.WriteAllText( includedMiz, "miz" );
        File.WriteAllText( includedTrk, "trk" );
        File.WriteAllText( excludedTxt, "txt" );

        var sut = new TranslationArchiveDiscoveryService( _loggerMock.Object );

        var result = await sut.DiscoverAsync( dcsWorldDir, null, TestContext.Current.CancellationToken );

        Assert.Equal( 2, result.Count );
        Assert.Contains( result, entry => entry.Name == "Included.miz" && entry.ArchiveType == TranslationArchiveType.Miz );
        Assert.Contains( result, entry => entry.Name == "Included.trk" && entry.ArchiveType == TranslationArchiveType.Trk );
        Assert.DoesNotContain( result, entry => entry.Name == "Excluded.txt" );
    }

    [Fact]
    public async Task DiscoverAsyncは探索元ごとにカテゴリを割り当てる() {
        var dcsWorldDir = Path.Combine( _tempDir, "DCSWorld" );
        var userMissionDir = Path.Combine( _tempDir, "UserMissions" );

        var aircraftArchive = Path.Combine( dcsWorldDir, "Mods", "aircraft", "A10C", "Included.miz" );
        var campaignArchive = Path.Combine( dcsWorldDir, "Mods", "campaigns", "CampaignA", "Included.trk" );
        var userMissionArchive = Path.Combine( userMissionDir, "My", "Included.miz" );

        Directory.CreateDirectory( Path.GetDirectoryName( aircraftArchive )! );
        Directory.CreateDirectory( Path.GetDirectoryName( campaignArchive )! );
        Directory.CreateDirectory( Path.GetDirectoryName( userMissionArchive )! );

        File.WriteAllText( aircraftArchive, "miz" );
        File.WriteAllText( campaignArchive, "trk" );
        File.WriteAllText( userMissionArchive, "miz" );

        var sut = new TranslationArchiveDiscoveryService( _loggerMock.Object );

        var result = await sut.DiscoverAsync( dcsWorldDir, userMissionDir, TestContext.Current.CancellationToken );

        Assert.Contains( result, entry => entry.Name == "Included.miz" && entry.Category == TranslationArchiveCategory.Aircraft );
        Assert.Contains( result, entry => entry.Name == "Included.trk" && entry.Category == TranslationArchiveCategory.DlcCampaigns );
        Assert.Contains( result, entry => entry.FullPath == userMissionArchive && entry.Category == TranslationArchiveCategory.UserMissions );
    }

    [Fact]
    public async Task DiscoverAsyncは壊れたzipと存在しないパスを無視せず対象拡張子として返す() {
        var dcsWorldDir = Path.Combine( _tempDir, "DCSWorld" );
        var aircraftDir = Path.Combine( dcsWorldDir, "Mods", "aircraft", "A10C" );
        Directory.CreateDirectory( aircraftDir );

        var validArchive = Path.Combine( aircraftDir, "Valid.miz" );
        var brokenArchive = Path.Combine( aircraftDir, "Broken.trk" );
        File.WriteAllText( validArchive, "valid archive" );
        File.WriteAllText( brokenArchive, "broken archive" );

        var sut = new TranslationArchiveDiscoveryService( _loggerMock.Object );

        var result = await sut.DiscoverAsync( dcsWorldDir, Path.Combine( _tempDir, "MissingUserMissions" ), TestContext.Current.CancellationToken );

        Assert.Equal( 2, result.Count );
        Assert.Contains( result, entry => entry.Name == "Valid.miz" );
        Assert.Contains( result, entry => entry.Name == "Broken.trk" );
    }

    [Fact]
    public async Task DiscoverAsyncは一部サブディレクトリの列挙失敗時も同カテゴリの他ディレクトリを返す() {
        var dcsWorldDir = Path.Combine( _tempDir, "DCSWorld" );
        var aircraftRoot = Path.Combine( dcsWorldDir, "Mods", "aircraft" );
        var healthyDir = Path.Combine( aircraftRoot, "A10C" );
        var failingDir = Path.Combine( aircraftRoot, "BrokenModule" );
        Directory.CreateDirectory( healthyDir );
        Directory.CreateDirectory( failingDir );

        var healthyArchive = Path.Combine( healthyDir, "Healthy.miz" );
        var failingArchive = Path.Combine( failingDir, "Skipped.trk" );
        File.WriteAllText( healthyArchive, "miz" );
        File.WriteAllText( failingArchive, "trk" );

        var sut = new FaultInjectingTranslationArchiveDiscoveryService( _loggerMock.Object, failingDir );

        var result = await sut.DiscoverAsync( dcsWorldDir, null, TestContext.Current.CancellationToken );

        Assert.Single( result );
        Assert.Contains( result, entry => entry.FullPath == healthyArchive && entry.Category == TranslationArchiveCategory.Aircraft );
        Assert.DoesNotContain( result, entry => entry.FullPath == failingArchive );
    }

    private sealed class FaultInjectingTranslationArchiveDiscoveryService(
        ILoggingService logger,
        string failingDirectoryPath
    ) : TranslationArchiveDiscoveryService( logger ) {
        protected override IEnumerable<string> EnumerateFiles( string directoryPath ) {
            if(string.Equals( directoryPath, failingDirectoryPath, StringComparison.OrdinalIgnoreCase )) {
                throw new UnauthorizedAccessException( $"Access denied: {directoryPath}" );
            }

            return base.EnumerateFiles( directoryPath );
        }
    }
}