using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.ViewModels;

/// <summary>
/// FileEntryViewModel の選択状態に関する動作を検証する。
/// </summary>
public sealed class FileEntryViewModelTests {
    [Fact]
    public void ディレクトリ選択時に子ノードへ選択状態を伝播しない() {
        var loggerMock = new Mock<ILoggingService>();
        var directoryNode = new FileEntryViewModel(
            new LocalFileEntry( "A10C", "A10C", true, @"C:\DCSWorld\Mods\aircraft\A10C" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        var fileNode = new FileEntryViewModel(
            new LocalFileEntry( "Mission1.miz", "A10C/Mission1.miz", false, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        directoryNode.Children.Add( fileNode );

        directoryNode.IsSelected = true;

        Assert.True( directoryNode.IsSelected );
        Assert.False( fileNode.IsSelected );
    }

    [Fact]
    public void ディレクトリ選択解除時に子ノードの選択状態を変更しない() {
        var loggerMock = new Mock<ILoggingService>();
        var directoryNode = new FileEntryViewModel(
            new LocalFileEntry( "A10C", "A10C", true, @"C:\DCSWorld\Mods\aircraft\A10C" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        var fileNode = new FileEntryViewModel(
            new LocalFileEntry( "Mission1.miz", "A10C/Mission1.miz", false, @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" ),
            ChangeTypeMode.Upload,
            loggerMock.Object );
        directoryNode.Children.Add( fileNode );
        fileNode.IsSelected = true;
        directoryNode.IsSelected = true;

        directoryNode.IsSelected = false;

        Assert.False( directoryNode.IsSelected );
        Assert.True( fileNode.IsSelected );
    }
}