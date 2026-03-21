using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Views;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// Material Design ダイアログを利用して汎用的なダイアログ処理を提供する。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
/// <param name="apiService">GitHub API サービス。</param>
/// <param name="fileContentInspector">ファイル内容検査サービス。</param>
/// <param name="systemService">システム連携サービス。</param>
/// <param name="windowManager">ウィンドウマネージャー。</param>
public sealed class DialogService(
    ILoggingService logger,
    IApiService apiService,
    IFileContentInspector fileContentInspector,
    ISystemService systemService,
    IWindowManager windowManager
) : IDialogService {
    /// <inheritdoc/>
    public async Task<ConfirmationDialogResult> ConfirmationDialogShowAsync( ConfirmationDialogParameters parameters ) {
        ArgumentNullException.ThrowIfNull( parameters );
        logger.Info( $"確認ダイアログを表示する。Title={parameters.Title}, Identifier={parameters.DialogIdentifier}" );
        var dialog = new ConfirmationDialog
        {
            DataContext = parameters
        };
        object? result;

        try {
            result = await DialogHost.Show( dialog, parameters.DialogIdentifier );
        }
        catch(InvalidOperationException ex) {
            logger.Error( $"確認ダイアログ表示に失敗した。Identifier={parameters.DialogIdentifier}", ex );
            throw;
        }

        var dialogResult = result switch
        {
            ConfirmationDialogResult confirmationDialogResult => confirmationDialogResult,
            true => ConfirmationDialogResult.Confirm,
            false => ConfirmationDialogResult.Cancel,
            string value when Enum.TryParse<ConfirmationDialogResult>( value, true, out var parsed ) => parsed,
            _ => ConfirmationDialogResult.Cancel
        };
        logger.Info( $"確認ダイアログが閉じられた。Result={dialogResult}" );
        return dialogResult;
    }

    /// <inheritdoc/>
    public async Task<bool> ContinueCancelDialogShowAsync( ConfirmationDialogParameters parameters ) {
        var result = await ConfirmationDialogShowAsync( parameters );
        return result == ConfirmationDialogResult.Confirm;
    }

    /// <inheritdoc/>
    public async Task<CreatePullRequestResult> CreatePullRequestDialogShowAsync(
        CreatePullRequestDialogParameters parameters,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( parameters );
        logger.Info( $"PRダイアログを表示する。Category={parameters.Category}, SubCategory={parameters.SubCategory}" );
        var result = await CreatePullRequestViewModel.ShowDialogAsync(
            parameters,
            apiService,
            fileContentInspector,
            logger,
            systemService,
            windowManager,
            cancellationToken );
        logger.Info( $"PRダイアログが閉じられた。IsOk={result.IsOk}" );
        return result;
    }
}