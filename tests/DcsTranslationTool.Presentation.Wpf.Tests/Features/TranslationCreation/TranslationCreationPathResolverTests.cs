using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// <see cref="TranslationCreationPathResolver"/> の動作を検証する。
/// </summary>
public sealed class TranslationCreationPathResolverTests {
    [Fact]
    public void DcsWorld配下のアーカイブはDcsWorld出力先へ解決する() {
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            SourceUserMissionDir = @"C:\UserMissions"
        };
        var sut = new TranslationCreationPathResolver(
            settings,
            @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        var path = sut.GetDictionaryExportPath();

        Assert.Equal( @"C:\Translate\DCSWorld\Mods\aircraft\A10C\Mission1.miz\l10n\JP\dictionary", path );
    }

    [Fact]
    public void UserMissions配下のアーカイブはUserMissions出力先へ解決する() {
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            SourceUserMissionDir = @"C:\Users\me\Saved Games\DCS\Missions"
        };
        var sut = new TranslationCreationPathResolver(
            settings,
            @"C:\Users\me\Saved Games\DCS\Missions\Campaigns\Mission1.trk" );

        var path = sut.GetCsvExportPath();

        Assert.Equal( @"C:\Translate\UserMissions\Campaigns\Mission1.trk\l10n\JP\Mission1.csv", path );
    }

    [Fact]
    public void 出力先を解決できない場合はImport初期パスへアーカイブ隣接名を返す() {
        var settings = new AppSettings
        {
            TranslateFileDir = @"C:\Translate",
            DcsWorldInstallDir = @"C:\DCSWorld",
            SourceUserMissionDir = @"C:\UserMissions"
        };
        var sut = new TranslationCreationPathResolver(
            settings,
            @"D:\Other\Mission1.miz" );

        var path = sut.GetPoImportInitialPath();

        Assert.Equal( @"D:\Other\Mission1.po", path );
    }
}