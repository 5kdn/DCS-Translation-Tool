using System.Threading;

using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 汎用的なダイアログ表示を提供するサービスを表現する。
/// </summary>
public interface IDialogService {
    /// <summary>
    /// ダイアログを表示して結果を取得する。
    /// </summary>
    /// <param name="parameters">表示に利用する引数。</param>
    /// <returns>承認された場合は <see langword="true"/>。</returns>
    Task<bool> ContinueCancelDialogShowAsync( ConfirmationDialogParameters parameters );

    /// <summary>
    /// CreatePullRequest ダイアログを表示して結果を取得する。
    /// </summary>
    /// <param name="parameters">表示に利用する引数。</param>
    /// <param name="cancellationToken">キャンセル要求。</param>
    /// <returns>ダイアログの結果。</returns>
    Task<CreatePullRequestResult> CreatePullRequestDialogShowAsync(
        CreatePullRequestDialogParameters parameters,
        CancellationToken cancellationToken = default );
}