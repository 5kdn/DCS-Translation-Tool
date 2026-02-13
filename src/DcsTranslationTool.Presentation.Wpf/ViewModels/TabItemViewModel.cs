using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Extensions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Presentation.Wpf.ViewModels;

/// <summary>
/// タブを表す ViewModel
/// </summary>
/// <param name="tabType">タブの種類。</param>
/// <param name="logger">ロギングサービス。</param>
/// <param name="rootEntry">ルートエントリ。</param>
public sealed class TabItemViewModel(
    CategoryType tabType,
    ILoggingService logger,
    IFileEntryViewModel rootEntry
) : PropertyChangedBase {

    /// <summary>
    /// タブの種類。
    /// </summary>
    public CategoryType TabType { get; } = tabType;

    /// <summary>
    /// タブのタイトル。
    /// </summary>
    public string Title => TabType.GetTabTitle();

    /// <summary>
    /// ルートのファイルエントリ。
    /// </summary>
    public IFileEntryViewModel Root {
        get => rootEntry;
        set {
            if(!Set( ref rootEntry, value )) return;
            logger.Info( $"タブのルートを更新した。TabType={TabType}" );
        }
    }

    /// <summary>
    /// チェック状態を再帰的に設定する。
    /// </summary>
    /// <param name="value">設定するチェック状態。</param>
    public void SetCheckRecursive( bool value ) {
        logger.Info( $"タブ内のチェック状態を一括更新する。TabType={TabType}, Value={value}" );
        Root.SetSelectRecursive( value );
    }

    /// <summary>
    /// チェック状態の <see cref="FileEntry"/> を取得する。
    /// </summary>
    /// <returns>チェック状態の FileEntry リスト。</returns>
    public List<FileEntry> GetCheckedEntries() {
        var entries = Root.GetCheckedModelRecursive();
        logger.Info( $"チェック済みの FileEntry を取得した。TabType={TabType}, Count={entries.Count}" );
        return entries;
    }

    /// <summary>
    /// チェック状態の <see cref="IFileEntryViewModel"/> を取得する。
    /// </summary>
    /// <returns>チェック状態の IFileEntryViewModel リスト。</returns>
    public List<IFileEntryViewModel> GetCheckedViewModels() {
        var viewModels = Root.GetCheckedViewModelRecursive();
        logger.Info( $"チェック済みの FileEntryViewModel を取得した。TabType={TabType}, Count={viewModels.Count}" );
        return viewModels;
    }
}