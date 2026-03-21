using System.IO;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// 翻訳アーカイブ一覧から表示用タブを構築する。
/// </summary>
public sealed class TranslationArchiveTreeService( ILoggingService logger ) : ITranslationArchiveTreeService {
    /// <inheritdoc/>
    public IReadOnlyList<TabItemViewModel> BuildTabs(
        IReadOnlyList<TranslationArchiveEntry> entries,
        string? dcsWorldInstallDir,
        string? sourceUserMissionDir
    ) {
        var entriesByCategory = entries
            .GroupBy( entry => MapCategory( entry.Category ) )
            .ToDictionary( group => group.Key, group => (IReadOnlyList<TranslationArchiveEntry>)[.. group] );

        return
        [
            .. Enum.GetValues<CategoryType>()
                .Select( categoryType => BuildTab(
                    categoryType,
                    entriesByCategory.GetValueOrDefault( categoryType, [] ),
                    dcsWorldInstallDir,
                    sourceUserMissionDir ) )
        ];
    }

    /// <summary>
    /// 単一カテゴリのタブを構築する。
    /// </summary>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <param name="entries">カテゴリに属する翻訳アーカイブ一覧。</param>
    /// <param name="dcsWorldInstallDir">DCS World インストールディレクトリ。</param>
    /// <param name="sourceUserMissionDir">ユーザーミッションディレクトリ。</param>
    /// <returns>構築済みタブ。</returns>
    private TabItemViewModel BuildTab(
        CategoryType categoryType,
        IReadOnlyList<TranslationArchiveEntry> entries,
        string? dcsWorldInstallDir,
        string? sourceUserMissionDir
    ) {
        IFileEntryViewModel root = new FileEntryViewModel(
            new LocalFileEntry( categoryType.GetTabTitle(), string.Empty, true ),
            ChangeTypeMode.Upload,
            logger );

        var nodesByPath = new Dictionary<string, IFileEntryViewModel>( StringComparer.OrdinalIgnoreCase )
        {
            [string.Empty] = root
        };

        foreach(var entry in entries) {
            var parts = entry.RelativePath.Split( '/', StringSplitOptions.RemoveEmptyEntries );
            var currentPath = string.Empty;
            var currentAbsolutePath = string.Empty;
            var parent = root;

            for(var index = 0; index < parts.Length; index++) {
                var part = parts[index];
                currentPath = string.IsNullOrEmpty( currentPath ) ? part : $"{currentPath}/{part}";
                currentAbsolutePath = string.IsNullOrEmpty( currentAbsolutePath ) ? part : Path.Combine( currentAbsolutePath, part );

                if(nodesByPath.TryGetValue( currentPath, out var existingNode )) {
                    parent = existingNode;
                    continue;
                }

                var isDirectory = index < parts.Length - 1;
                var absolutePath = isDirectory
                    ? Path.Combine( GetCategoryRootPath( categoryType, dcsWorldInstallDir, sourceUserMissionDir ), currentAbsolutePath )
                    : entry.FullPath;
                var node = new FileEntryViewModel(
                    new LocalFileEntry( part, currentPath, isDirectory, absolutePath ),
                    ChangeTypeMode.Upload,
                    logger );
                parent.Children.Add( node );
                nodesByPath[currentPath] = node;
                parent = node;
            }
        }

        return new TabItemViewModel( categoryType, logger, root );
    }

    /// <summary>
    /// カテゴリに対応する探索ルートの絶対パスを取得する。
    /// </summary>
    /// <param name="categoryType">対象カテゴリ。</param>
    /// <param name="dcsWorldInstallDir">DCS World インストールディレクトリ。</param>
    /// <param name="sourceUserMissionDir">ユーザーミッションディレクトリ。</param>
    /// <returns>探索ルートの絶対パス。</returns>
    private static string GetCategoryRootPath(
        CategoryType categoryType,
        string? dcsWorldInstallDir,
        string? sourceUserMissionDir
    ) => categoryType switch
    {
        CategoryType.Aircraft => Path.Combine( dcsWorldInstallDir ?? string.Empty, "Mods", "aircraft" ),
        CategoryType.DlcCampaigns => Path.Combine( dcsWorldInstallDir ?? string.Empty, "Mods", "campaigns" ),
        CategoryType.UserMissions => sourceUserMissionDir ?? string.Empty,
        _ => throw new ArgumentOutOfRangeException( nameof( categoryType ), categoryType, "未対応のカテゴリである。" ),
    };

    /// <summary>
    /// アプリケーション層のカテゴリを UI カテゴリへ変換する。
    /// </summary>
    /// <param name="category">変換元カテゴリ。</param>
    /// <returns>対応する UI カテゴリ。</returns>
    private static CategoryType MapCategory( TranslationArchiveCategory category ) => category switch
    {
        TranslationArchiveCategory.Aircraft => CategoryType.Aircraft,
        TranslationArchiveCategory.DlcCampaigns => CategoryType.DlcCampaigns,
        TranslationArchiveCategory.UserMissions => CategoryType.UserMissions,
        _ => throw new ArgumentOutOfRangeException( nameof( category ), category, "未対応のカテゴリである。" ),
    };
}