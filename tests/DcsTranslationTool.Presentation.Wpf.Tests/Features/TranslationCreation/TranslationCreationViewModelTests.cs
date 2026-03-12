using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の動作を検証する。
/// </summary>
public sealed class TranslationCreationViewModelTests {
    [Fact]
    public void コンストラクタは選択中アーカイブ絶対パスを保持する() {
        var viewModel = new TranslationCreationViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Equal( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz", viewModel.ArchiveFullPath );
    }

    [Fact]
    public void コンストラクタは空白のアーカイブ絶対パスを拒否する() {
        Assert.Throws<ArgumentException>( () => new TranslationCreationViewModel( string.Empty ) );
    }

    [Fact]
    public void 長いパスでも絶対パスをそのまま保持する() {
        var viewModel = new TranslationCreationViewModel( @"C:\DCSWorld\Mods\aircraft\VeryLongDirectoryName\AnotherLongDirectoryName\Mission1.miz" );

        Assert.Equal( @"C:\DCSWorld\Mods\aircraft\VeryLongDirectoryName\AnotherLongDirectoryName\Mission1.miz", viewModel.ArchiveFullPath );
    }

    [Fact]
    public void 短いパスでも絶対パスをそのまま保持する() {
        var viewModel = new TranslationCreationViewModel( @"C:\A\Mission1.miz" );

        Assert.Equal( @"C:\A\Mission1.miz", viewModel.ArchiveFullPath );
    }
}