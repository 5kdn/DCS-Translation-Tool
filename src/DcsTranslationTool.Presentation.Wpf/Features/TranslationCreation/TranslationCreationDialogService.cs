using System.IO;

using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 機能のダイアログ操作を担う。
/// </summary>
internal sealed class TranslationCreationDialogService(
    IDialogService dialogService,
    IDialogProvider dialogProvider,
    ILoggingService logger ) : ITranslationCreationDialogService {
    /// <inheritdoc />
    public Task<bool> ConfirmCloseAsync() =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationCloseConfirmationTitle,
            Strings_Translation.CreateTranslationCloseConfirmationMessage,
            Strings_Translation.CreateTranslationCloseConfirmationConfirmButtonText,
            Strings_Translation.CreateTranslationCloseConfirmationCancelButtonText );

    /// <inheritdoc />
    public Task<bool> ConfirmPoOverwriteAsync() =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationPoImportOverwriteConfirmationTitle,
            Strings_Translation.CreateTranslationPoImportOverwriteConfirmationMessage,
            "上書き",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmDictionaryOverwriteAsync() =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationDictionaryImportOverwriteConfirmationTitle,
            Strings_Translation.CreateTranslationDictionaryImportOverwriteConfirmationMessage,
            "上書き",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmCsvOverwriteAsync() =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationCsvImportOverwriteConfirmationTitle,
            Strings_Translation.CreateTranslationCsvImportOverwriteConfirmationMessage,
            "上書き",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmDictionaryPartialImportAsync( int matchedCount ) =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationTitle,
            string.Format( Strings_Translation.CreateTranslationDictionaryImportPartialConfirmationMessage, matchedCount ),
            "取り込む",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmPoPartialImportAsync( int matchedCount ) =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationPoImportPartialConfirmationTitle,
            string.Format( Strings_Translation.CreateTranslationPoImportPartialConfirmationMessage, matchedCount ),
            "取り込む",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmCsvPartialImportAsync( int matchedCount ) =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationCsvImportPartialConfirmationTitle,
            string.Format( Strings_Translation.CreateTranslationCsvImportPartialConfirmationMessage, matchedCount ),
            "取り込む",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmArchiveContainsJapaneseDictionaryAsync( string archiveFullPath ) =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryConfirmationTitle,
            string.Format(
                Path.GetExtension( archiveFullPath ).Equals( ".trk", StringComparison.OrdinalIgnoreCase )
                    ? Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryTrkConfirmationMessage
                    : Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryMizConfirmationMessage,
                archiveFullPath ),
            "継続",
            "キャンセル" );

    /// <inheritdoc />
    public Task<bool> ConfirmJapaneseDictionaryImportAsync() =>
        ConfirmAsync(
            Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryImportConfirmationTitle,
            Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryImportConfirmationMessage,
            "取り込む",
            "取り込まない" );

    /// <inheritdoc />
    public async Task<string?> ConfirmExportPathAsync( string exportPath, string saveFileFilter, string logTargetName, string archiveFullPath ) {
        if(!File.Exists( exportPath )) {
            return exportPath;
        }

        var overwriteResult = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = "上書き確認",
                Message = $"保存先にファイルが既に存在します。上書きしますか？{Environment.NewLine}{Environment.NewLine}{exportPath}",
                ConfirmButtonText = "上書き",
                CancelButtonText = "キャンセル",
                SecondaryButtonText = "別名保存",
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );

        if(overwriteResult == ConfirmationDialogResult.Cancel) {
            logger.Info( $"{logTargetName} の上書き確認でキャンセルされた。Archive={archiveFullPath}, Path={exportPath}" );
            return null;
        }

        if(overwriteResult != ConfirmationDialogResult.Secondary) {
            return exportPath;
        }

        if(!dialogProvider.ShowSaveFilePicker( exportPath, saveFileFilter, out var selectedPath )) {
            logger.Info( $"{logTargetName} の別名保存でキャンセルされた。Archive={archiveFullPath}, Path={exportPath}" );
            return null;
        }

        return selectedPath;
    }

    /// <inheritdoc />
    public bool TrySelectImportFile( string initialPath, string openFileFilter, out string selectedPath ) =>
        dialogProvider.ShowOpenFilePicker( initialPath, openFileFilter, out selectedPath );

    private async Task<bool> ConfirmAsync(
        string title,
        string message,
        string confirmButtonText,
        string cancelButtonText ) {
        var result = await dialogService.ConfirmationDialogShowAsync(
            new ConfirmationDialogParameters
            {
                Title = title,
                Message = message,
                ConfirmButtonText = confirmButtonText,
                CancelButtonText = cancelButtonText,
                DialogIdentifier = TranslationCreationDialogHostIdentifiers.Confirmation,
            } );
        return result == ConfirmationDialogResult.Confirm;
    }
}