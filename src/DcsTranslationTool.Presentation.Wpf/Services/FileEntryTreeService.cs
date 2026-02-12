using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Helpers;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// FileEntry ツリーの構築とフィルタ適用を提供する。
/// </summary>
public sealed class FileEntryTreeService( ILoggingService logger ) : IFileEntryTreeService {
    /// <inheritdoc/>
    public IReadOnlyList<TabItemViewModel> BuildTabs(
        IReadOnlyList<FileEntry> localEntries,
        IReadOnlyList<FileEntry> repoEntries,
        ChangeTypeMode mode
    ) {
        var entries = FileEntryComparisonHelper.Merge( localEntries, repoEntries );

        IFileEntryViewModel rootVm = new FileEntryViewModel(
            new FileEntry( string.Empty, string.Empty, true ),
            mode,
            logger
        );
        foreach(var entry in entries) {
            this.AddFileEntryToTree( rootVm, entry, mode );
        }

        var tabs = Enum.GetValues<CategoryType>()
            .Select( tabType => this.BuildTabItem( tabType, rootVm, mode ) )
            .ToList();
        return tabs;
    }

    /// <inheritdoc/>
    public void ApplyFilter( IEnumerable<TabItemViewModel> tabs, HashSet<FileChangeType?> types ) {
        foreach(var tab in tabs) {
            this.ApplyFilterRecursive( tab.Root, types );
        }
    }

    /// <summary>
    /// タブ定義から表示用タブ ViewModel を構築する。
    /// </summary>
    /// <param name="tabType">タブ種別。</param>
    /// <param name="rootVm">全体ルート。</param>
    /// <param name="mode">差分判定モード。</param>
    /// <returns>構築済みタブ。</returns>
    private TabItemViewModel BuildTabItem( CategoryType tabType, IFileEntryViewModel rootVm, ChangeTypeMode mode ) {
        IFileEntryViewModel? target = rootVm;
        foreach(var name in tabType.GetRepoDirRoot()) {
            target = target?.Children.FirstOrDefault( child => child?.Name == name );
            if(target is null) {
                break;
            }
        }

        return new TabItemViewModel(
            tabType,
            logger,
            target ?? new FileEntryViewModel(
                new FileEntry( "null", string.Empty, false ),
                mode,
                logger
            )
        );
    }

    /// <summary>
    /// 指定ノードへフィルタを再帰適用する。
    /// </summary>
    /// <param name="node">適用対象ノード。</param>
    /// <param name="types">可視とする変更種別集合。</param>
    /// <returns>可視である場合は <see langword="true"/>。</returns>
    private bool ApplyFilterRecursive( IFileEntryViewModel node, HashSet<FileChangeType?> types ) {
        var visible = types.Contains( node.ChangeType );
        if(node.IsDirectory) {
            var childVisible = false;
            foreach(var child in node.Children) {
                if(this.ApplyFilterRecursive( child, types )) {
                    childVisible = true;
                }
            }
            visible |= childVisible;
        }

        node.IsVisible = visible;
        return visible;
    }

    /// <summary>
    /// <see cref="FileEntry"/> をツリーへ追加する。
    /// </summary>
    /// <param name="root">ルートノード。</param>
    /// <param name="entry">追加対象エントリ。</param>
    /// <param name="mode">差分判定モード。</param>
    private void AddFileEntryToTree( IFileEntryViewModel root, FileEntry entry, ChangeTypeMode mode ) {
        var parts = entry.Path.Split( "/", StringSplitOptions.RemoveEmptyEntries );
        if(parts.Length == 0) {
            return;
        }

        IFileEntryViewModel current = root;
        var absolutePath = string.Empty;

        foreach(var part in parts[..^1]) {
            absolutePath += absolutePath.Length == 0 ? part : "/" + part;
            var next = current.Children.FirstOrDefault( child => child?.Name == part && child.IsDirectory );
            if(next is null) {
                next = new FileEntryViewModel( new FileEntry( part, absolutePath, true ), mode, logger );
                current.Children.Add( next );
            }
            current = next;
        }

        var last = parts[^1];
        if(current.Children.Any( child => child?.Name == last )) {
            return;
        }

        current.Children.Add(
            new FileEntryViewModel(
                new FileEntry( last, entry.Path, entry.IsDirectory, entry.LocalSha, entry.RepoSha ),
                mode,
                logger
            )
        );
    }
}