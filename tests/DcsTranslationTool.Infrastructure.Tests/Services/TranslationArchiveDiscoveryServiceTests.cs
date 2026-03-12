using System.IO.Compression;

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
    public async Task DiscoverAsyncはdictionaryを持つmizとtrkのみを返す() {
        var dcsWorldDir = Path.Combine( _tempDir, "DCSWorld" );
        var aircraftDir = Path.Combine( dcsWorldDir, "Mods", "aircraft", "A10C", "Missions", "EN" );
        Directory.CreateDirectory( aircraftDir );

        var includedMiz = Path.Combine( aircraftDir, "Included.miz" );
        var excludedMiz = Path.Combine( aircraftDir, "Excluded.miz" );
        var includedTrk = Path.Combine( aircraftDir, "Included.trk" );
        CreateArchive( includedMiz, includeDictionary: true );
        CreateArchive( excludedMiz, includeDictionary: false );
        CreateArchive( includedTrk, includeDictionary: true );

        var sut = new TranslationArchiveDiscoveryService( _loggerMock.Object );

        var result = await sut.DiscoverAsync( dcsWorldDir, null, TestContext.Current.CancellationToken );

        Assert.Equal( 2, result.Count );
        Assert.Contains( result, entry => entry.Name == "Included.miz" && entry.ArchiveType == TranslationArchiveType.Miz );
        Assert.Contains( result, entry => entry.Name == "Included.trk" && entry.ArchiveType == TranslationArchiveType.Trk );
        Assert.DoesNotContain( result, entry => entry.Name == "Excluded.miz" );
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

        CreateArchive( aircraftArchive, includeDictionary: true );
        CreateArchive( campaignArchive, includeDictionary: true );
        CreateArchive( userMissionArchive, includeDictionary: true );

        var sut = new TranslationArchiveDiscoveryService( _loggerMock.Object );

        var result = await sut.DiscoverAsync( dcsWorldDir, userMissionDir, TestContext.Current.CancellationToken );

        Assert.Contains( result, entry => entry.Name == "Included.miz" && entry.Category == TranslationArchiveCategory.Aircraft );
        Assert.Contains( result, entry => entry.Name == "Included.trk" && entry.Category == TranslationArchiveCategory.DlcCampaigns );
        Assert.Contains( result, entry => entry.FullPath == userMissionArchive && entry.Category == TranslationArchiveCategory.UserMissions );
    }

    [Fact]
    public async Task DiscoverAsyncは壊れたzipと存在しないパスを無視して継続する() {
        var dcsWorldDir = Path.Combine( _tempDir, "DCSWorld" );
        var aircraftDir = Path.Combine( dcsWorldDir, "Mods", "aircraft", "A10C" );
        Directory.CreateDirectory( aircraftDir );

        var validArchive = Path.Combine( aircraftDir, "Valid.miz" );
        var brokenArchive = Path.Combine( aircraftDir, "Broken.trk" );
        CreateArchive( validArchive, includeDictionary: true );
        File.WriteAllText( brokenArchive, "broken archive" );

        var sut = new TranslationArchiveDiscoveryService( _loggerMock.Object );

        var result = await sut.DiscoverAsync( dcsWorldDir, Path.Combine( _tempDir, "MissingUserMissions" ), TestContext.Current.CancellationToken );

        var entry = Assert.Single( result );
        Assert.Equal( "Valid.miz", entry.Name );
        _loggerMock.Verify(
            logger => logger.Warn(
                It.Is<string>( message => message.Contains( "アーカイブの検査に失敗したため対象をスキップする。", StringComparison.Ordinal ) ),
                It.IsAny<Exception>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<int>() ),
            Times.AtLeastOnce );
    }

    /// <summary>
    /// テスト用アーカイブを作成する。
    /// </summary>
    /// <param name="archivePath">作成先パス。</param>
    /// <param name="includeDictionary">dictionary エントリを含めるかどうか。</param>
    private static void CreateArchive( string archivePath, bool includeDictionary ) {
        using var archive = ZipFile.Open( archivePath, ZipArchiveMode.Create );
        archive.CreateEntry( "mission" );
        if(includeDictionary) {
            archive.CreateEntry( "l10n/DEFAULT/dictionary" );
        }
    }
}