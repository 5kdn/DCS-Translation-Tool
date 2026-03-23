using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Services;

/// <summary>
/// <see cref="TranslationArchiveTreeService"/> の動作を検証する。
/// </summary>
public sealed class TranslationArchiveTreeServiceTests {
    [StaFact]
    public void BuildTabsはカテゴリ別にタブを構築する() {
        var logger = new Mock<ILoggingService>();
        var sut = new TranslationArchiveTreeService( logger.Object );

        var result = sut.BuildTabs(
            [
                new TranslationArchiveEntry(
                    "Mission1.miz",
                    @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz",
                    "A10C/Mission1.miz",
                    TranslationArchiveCategory.Aircraft,
                    TranslationArchiveType.Miz ),
                new TranslationArchiveEntry(
                    "Campaign1.trk",
                    @"C:\DCSWorld\Mods\campaigns\RedFlag\Campaign1.trk",
                    "RedFlag/Campaign1.trk",
                    TranslationArchiveCategory.DlcCampaigns,
                    TranslationArchiveType.Trk )
            ],
            @"C:\DCSWorld",
            @"C:\UserMissions" );

        Assert.Equal( 3, result.Count );
        var aircraftTab = result.Single( tab => tab.TabType == CategoryType.Aircraft );
        var aircraftDirectory = Assert.Single( aircraftTab.Root.Children );
        var aircraftFile = Assert.Single( aircraftDirectory.Children );
        Assert.Equal( @"C:\DCSWorld\Mods\aircraft\A10C", aircraftDirectory.Model.LocalSha );
        Assert.Equal( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz", aircraftFile.Model.LocalSha );
    }

    [StaFact]
    public void BuildTabsは空一覧でも全カテゴリタブを返す() {
        var logger = new Mock<ILoggingService>();
        var sut = new TranslationArchiveTreeService( logger.Object );

        var result = sut.BuildTabs( [], @"C:\DCSWorld", @"C:\UserMissions" );

        Assert.Equal( 3, result.Count );
        Assert.All( result, tab => Assert.Empty( tab.Root.Children ) );
    }
}