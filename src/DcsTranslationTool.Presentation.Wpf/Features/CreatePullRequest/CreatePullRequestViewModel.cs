using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;

/// <summary>
/// PR作成ダイアログの ViewModel。
/// 変更種別の選択と同意項目の状態を集約し、PR作成可否を制御する。
/// </summary>
public class CreatePullRequestViewModel(
) : Conductor<IScreen>.Collection.OneActive, IActivate {

    /// <summary>
    /// モーダル表示ヘルパー。
    /// </summary>
    public static Task<CreatePullRequestResult> ShowDialogAsync(
        CreatePullRequestDialogParameters parameters,
        IApiService apiService,
        IFileContentInspector fileContentInspector,
        ILoggingService logger,
        IWindowManager windowManager,
        CancellationToken cancellationToken = default
    ) {
        throw new NotImplementedException();
    }
}