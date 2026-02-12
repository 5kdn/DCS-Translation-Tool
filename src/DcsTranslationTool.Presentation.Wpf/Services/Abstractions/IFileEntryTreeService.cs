using DcsTranslationTool.Domain.Models;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// FileEntry ツリーの構築とフィルタ適用を提供するサービス契約を表す。
/// </summary>
public interface IFileEntryTreeService {
    /// <summary>
    /// ローカルとリポジトリエントリからタブ一覧を構築する。
    /// </summary>
    /// <param name="localEntries">ローカル側エントリ。</param>
    /// <param name="repoEntries">リポジトリ側エントリ。</param>
    /// <param name="mode">差分判定モード。</param>
    /// <returns>構築済みタブ一覧。</returns>
    IReadOnlyList<TabItemViewModel> BuildTabs(
        IReadOnlyList<FileEntry> localEntries,
        IReadOnlyList<FileEntry> repoEntries,
        ChangeTypeMode mode
    );

    /// <summary>
    /// 指定タブへ変更種別フィルタを適用する。
    /// </summary>
    /// <param name="tabs">適用対象タブ。</param>
    /// <param name="types">表示対象の変更種別。</param>
    void ApplyFilter( IEnumerable<TabItemViewModel> tabs, HashSet<FileChangeType?> types );
}