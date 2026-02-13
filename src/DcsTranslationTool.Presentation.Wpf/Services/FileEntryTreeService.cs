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
        var rootNode = new TreeNode( string.Empty, string.Empty, true );
        foreach(var entry in entries) {
            AddFileEntryToTree( rootNode, entry );
        }

        var rootVm = this.BuildViewModelTree( rootNode, mode );

        var tabs = Enum.GetValues<CategoryType>()
            .Select( tabType => this.BuildTabItem( tabType, rootVm, mode ) )
            .ToList();
        return tabs;
    }

    /// <inheritdoc/>
    public void ApplyFilter( IEnumerable<TabItemViewModel> tabs, HashSet<FileChangeType?> types ) {
        foreach(var tab in tabs.ToArray()) {
            ApplyFilterRecursive( tab.Root, types );
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
            target = target?.Children.FirstOrDefault( child => child is { Name: var childName, IsDirectory: true } && childName == name );
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
    private static bool ApplyFilterRecursive( IFileEntryViewModel node, HashSet<FileChangeType?> types ) {
        var visible = types.Contains( node.ChangeType );
        if(node.IsDirectory) {
            var childVisible = false;
            foreach(var child in node.Children.ToArray()) {
                if(ApplyFilterRecursive( child, types )) {
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
    private static void AddFileEntryToTree( TreeNode root, FileEntry entry ) {
        var parts = entry.Path.Split( "/", StringSplitOptions.RemoveEmptyEntries );
        if(parts.Length == 0) {
            return;
        }

        var current = root;
        var absolutePath = string.Empty;

        for(var index = 0; index < parts.Length; index++) {
            var part = parts[index];
            absolutePath += absolutePath.Length == 0 ? part : "/" + part;
            var isLeaf = index == parts.Length - 1;
            var expectedIsDirectory = !isLeaf || entry.IsDirectory;
            var next = current.Children.FirstOrDefault( child => child.Name == part && child.IsDirectory == expectedIsDirectory );
            if(next is null) {
                next = new TreeNode(
                    part,
                    absolutePath,
                    expectedIsDirectory,
                    isLeaf ? entry.LocalSha : null,
                    isLeaf ? entry.RepoSha : null
                );
                current.Children.Add( next );
            }

            if(isLeaf) {
                next.LocalSha = entry.LocalSha;
                next.RepoSha = entry.RepoSha;
            }

            current = next;
        }
    }

    /// <summary>
    /// 内部ノードツリーから <see cref="IFileEntryViewModel"/> ツリーを構築する。
    /// </summary>
    /// <param name="node">変換対象ノード。</param>
    /// <param name="mode">差分判定モード。</param>
    /// <returns>構築済みノード。</returns>
    private IFileEntryViewModel BuildViewModelTree( TreeNode node, ChangeTypeMode mode ) {
        var viewModel = new FileEntryViewModel(
            new FileEntry( node.Name, node.Path, node.IsDirectory, node.LocalSha, node.RepoSha ),
            mode,
            logger
        );

        foreach(var child in node.Children
            .OrderBy( child => child.Name, StringComparer.Ordinal )
            .ThenByDescending( child => child.IsDirectory )) {
            viewModel.Children.Add( this.BuildViewModelTree( child, mode ) );
        }

        return viewModel;
    }

    /// <summary>
    /// ツリー構築用の内部ノードを表す。
    /// </summary>
    /// <param name="name">ノード名。</param>
    /// <param name="path">ルートからのパス。</param>
    /// <param name="isDirectory">ディレクトリかどうか。</param>
    /// <param name="localSha">ローカル SHA。</param>
    /// <param name="repoSha">リポジトリ SHA。</param>
    private sealed class TreeNode(
        string name,
        string path,
        bool isDirectory,
        string? localSha = null,
        string? repoSha = null
    ) {
        /// <summary>
        /// ノード名を取得する。
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// ルートからのパスを取得する。
        /// </summary>
        public string Path { get; } = path;

        /// <summary>
        /// ディレクトリかどうかを取得する。
        /// </summary>
        public bool IsDirectory { get; } = isDirectory;

        /// <summary>
        /// ローカル SHA を取得または設定する。
        /// </summary>
        public string? LocalSha { get; set; } = localSha;

        /// <summary>
        /// リポジトリ SHA を取得または設定する。
        /// </summary>
        public string? RepoSha { get; set; } = repoSha;

        /// <summary>
        /// 子ノード一覧を取得する。
        /// </summary>
        public List<TreeNode> Children { get; } = [];
    }
}