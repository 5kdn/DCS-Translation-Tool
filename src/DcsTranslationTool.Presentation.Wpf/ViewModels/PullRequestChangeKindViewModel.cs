using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.UI.Enums;

namespace DcsTranslationTool.Presentation.Wpf.ViewModels;

/// <summary>
/// PullRequest の変更種別を表すチェックボックス項目を管理する ViewModel。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
/// <param name="kind">対応する<see cref="Enums.PullRequestChangeKind"/> 。</param>
public class PullRequestChangeKindViewModel(
    ILoggingService logger,
    PullRequestChangeKind kind
) : PropertyChangedBase {
    private bool _isChecked;

    /// <summary>
    /// チェック状態を管理する。
    /// </summary>
    public bool IsChecked {
        get => _isChecked;
        set {
            if(!Set( ref _isChecked, value )) return;
            logger.Info( $"変更種別のチェック状態を更新した。Kind={Kind}, IsChecked={value}" );
        }
    }

    /// <summary>
    /// 対応する変更種別。
    /// </summary>
    public PullRequestChangeKind Kind { get; init; } = kind;

    /// <summary>
    /// 表示名。
    /// </summary>
    public string DisplayName { get; } = kind.ToString();
}