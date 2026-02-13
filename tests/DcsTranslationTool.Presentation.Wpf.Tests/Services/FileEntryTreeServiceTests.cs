using System.Collections.ObjectModel;
using System.ComponentModel;

using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class FileEntryTreeServiceTests {
    [Fact]
    public void ApplyFilterは子ノード列挙中にコレクションが変更されても例外を送出しない() {
        var logger = new Mock<ILoggingService>();
        var sut = new FileEntryTreeService( logger.Object );
        var root = new FileEntryViewModel(
            new FileEntry( "root", "root", true ),
            ChangeTypeMode.Upload,
            logger.Object
        );
        var first = new FileEntryViewModel(
            new FileEntry( "first.lua", "root/first.lua", false, "local-1", "repo-1" ),
            ChangeTypeMode.Upload,
            logger.Object
        );
        var second = new FileEntryViewModel(
            new FileEntry( "second.lua", "root/second.lua", false, "local-2", "repo-2" ),
            ChangeTypeMode.Upload,
            logger.Object
        );
        root.Children.Add( first );
        root.Children.Add( second );
        ((INotifyPropertyChanged)first).PropertyChanged += ( _, e ) => {
            if(e.PropertyName != nameof( FileEntryViewModel.IsVisible )) {
                return;
            }

            if(root.Children.Contains( second )) {
                root.Children.Remove( second );
            }
        };

        var tab = new TabItemViewModel( CategoryType.Aircraft, logger.Object, root );

        var exception = Record.Exception( () => sut.ApplyFilter(
            [tab],
            [FileChangeType.Modified]
        ) );

        Assert.Null( exception );
    }

    [Fact]
    public void ApplyFilterはタブ列挙中にコレクションが変更されても例外を送出しない() {
        var logger = new Mock<ILoggingService>();
        var sut = new FileEntryTreeService( logger.Object );
        var firstRoot = new FileEntryViewModel(
            new FileEntry( "first-root", "first-root", true ),
            ChangeTypeMode.Upload,
            logger.Object
        );
        firstRoot.Children.Add( new FileEntryViewModel(
            new FileEntry( "first.lua", "first-root/first.lua", false, "local-1", "repo-1" ),
            ChangeTypeMode.Upload,
            logger.Object
        ) );
        var secondRoot = new FileEntryViewModel(
            new FileEntry( "second-root", "second-root", true ),
            ChangeTypeMode.Upload,
            logger.Object
        );
        secondRoot.Children.Add( new FileEntryViewModel(
            new FileEntry( "second.lua", "second-root/second.lua", false, "local-2", "repo-2" ),
            ChangeTypeMode.Upload,
            logger.Object
        ) );
        var firstTab = new TabItemViewModel( CategoryType.Aircraft, logger.Object, firstRoot );
        var secondTab = new TabItemViewModel( CategoryType.DlcCampaigns, logger.Object, secondRoot );
        var tabs = new ObservableCollection<TabItemViewModel> { firstTab, secondTab };
        ((INotifyPropertyChanged)firstRoot).PropertyChanged += ( _, e ) => {
            if(e.PropertyName != nameof( FileEntryViewModel.IsVisible )) {
                return;
            }

            tabs.Remove( secondTab );
        };

        var exception = Record.Exception( () => sut.ApplyFilter(
            tabs,
            [FileChangeType.Modified]
        ) );

        Assert.Null( exception );
    }

    [Fact]
    public void BuildTabsは同名のファイルとディレクトリが競合しても別ノードとして構築する() {
        var logger = new Mock<ILoggingService>();
        var sut = new FileEntryTreeService( logger.Object );
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "A10C", "DCSWorld/Mods/aircraft/A10C", false, "local-a10c-file-sha" )
        };
        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-example-sha" )
        };

        var tabs = sut.BuildTabs( localEntries, repoEntries, ChangeTypeMode.Upload );
        var aircraftRoot = tabs.Single( tab => tab.TabType == CategoryType.Aircraft ).Root;
        var conflictedNodes = aircraftRoot.Children
            .Where( child => string.Equals( child.Name, "A10C", StringComparison.Ordinal ) )
            .ToArray();

        Assert.Equal( 2, conflictedNodes.Length );
        Assert.Contains( conflictedNodes, node => !node.IsDirectory && node.Path == "DCSWorld/Mods/aircraft/A10C" );

        var directoryNode = Assert.Single( conflictedNodes, node => node.IsDirectory );
        var l10nNode = Assert.Single( directoryNode.Children, node => node.Name == "L10N" && node.IsDirectory );
        var exampleNode = Assert.Single( l10nNode.Children, node => node.Name == "Example.lua" && !node.IsDirectory );

        Assert.Equal( "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", exampleNode.Path );
    }
}