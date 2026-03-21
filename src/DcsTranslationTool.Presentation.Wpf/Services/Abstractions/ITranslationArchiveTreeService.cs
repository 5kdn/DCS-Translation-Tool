using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.ViewModels;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 翻訳アーカイブ一覧から表示用タブを構築するサービス契約を表す。
/// </summary>
public interface ITranslationArchiveTreeService {
    /// <summary>
    /// 翻訳アーカイブ一覧からカテゴリ別タブを構築する。
    /// </summary>
    /// <param name="entries">構築対象の翻訳アーカイブ一覧。</param>
    /// <param name="dcsWorldInstallDir">DCS World インストールディレクトリ。</param>
    /// <param name="sourceUserMissionDir">ユーザーミッションディレクトリ。</param>
    /// <returns>構築済みタブ一覧。</returns>
    IReadOnlyList<TabItemViewModel> BuildTabs(
        IReadOnlyList<TranslationArchiveEntry> entries,
        string? dcsWorldInstallDir,
        string? sourceUserMissionDir
    );
}