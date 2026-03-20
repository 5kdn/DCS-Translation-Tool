using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Resources;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationDialogService の動作を検証する。
/// </summary>
public sealed class TranslationCreationDialogServiceTests {
    [Fact]
    public async Task PromptEmbeddedJapaneseDictionaryStartupAsyncは3ボタンの順序とスタイルを設定する() {
        var context = new TranslationCreationDialogServiceTestContext();
        ConfirmationDialogParameters? actualParameters = null;
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .Callback<ConfirmationDialogParameters>( parameters => actualParameters = parameters )
            .ReturnsAsync( ConfirmationDialogResult.Secondary );
        var service = context.CreateService();

        var result = await service.PromptEmbeddedJapaneseDictionaryStartupAsync( @"C:\DCS\Mission\sample.miz" );

        Assert.Equal( TranslationCreationEmbeddedJapaneseDictionaryStartupChoice.ContinueWithoutImport, result );
        Assert.NotNull( actualParameters );
        Assert.Equal( Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryImportButtonText, actualParameters.ConfirmButtonText );
        Assert.Equal( Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryContinueWithoutImportButtonText, actualParameters.SecondaryButtonText );
        Assert.Equal( Strings_Translation.CreateTranslationEmbeddedJapaneseDictionaryCloseButtonText, actualParameters.CancelButtonText );
        Assert.Equal(
            [
                ConfirmationDialogResult.Confirm,
                ConfirmationDialogResult.Secondary,
                ConfirmationDialogResult.Cancel
            ],
            actualParameters.ButtonOrder );
        Assert.Equal( "MaterialDesignRaisedAffirmativeButton", actualParameters.ConfirmButtonStyleKey );
        Assert.Equal( "MaterialDesignRaisedNeutralButton", actualParameters.SecondaryButtonStyleKey );
        Assert.Equal( "MaterialDesignRaisedWarnButton", actualParameters.CancelButtonStyleKey );
    }

    [Fact]
    public async Task ConfirmCloseAsyncは2ボタンの順序とスタイルを設定する() {
        var context = new TranslationCreationDialogServiceTestContext();
        ConfirmationDialogParameters? actualParameters = null;
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .Callback<ConfirmationDialogParameters>( parameters => actualParameters = parameters )
            .ReturnsAsync( ConfirmationDialogResult.Confirm );
        var service = context.CreateService();

        var result = await service.ConfirmCloseAsync();

        Assert.True( result );
        Assert.NotNull( actualParameters );
        Assert.Equal(
            [
                ConfirmationDialogResult.Confirm,
                ConfirmationDialogResult.Cancel
            ],
            actualParameters.ButtonOrder );
        Assert.Equal( "MaterialDesignRaisedAffirmativeButton", actualParameters.ConfirmButtonStyleKey );
        Assert.Equal( "MaterialDesignRaisedWarnButton", actualParameters.CancelButtonStyleKey );
    }

    [Fact]
    public async Task ConfirmExportPathAsyncは3ボタンの順序とスタイルを設定する() {
        var context = new TranslationCreationDialogServiceTestContext();
        ConfirmationDialogParameters? actualParameters = null;
        context.DialogServiceMock
            .Setup( service => service.ConfirmationDialogShowAsync( It.IsAny<ConfirmationDialogParameters>() ) )
            .Callback<ConfirmationDialogParameters>( parameters => actualParameters = parameters )
            .ReturnsAsync( ConfirmationDialogResult.Cancel );
        var service = context.CreateService();
        var exportPath = Path.GetTempFileName();

        try {
            var result = await service.ConfirmExportPathAsync( exportPath, "CSV (*.csv)|*.csv", "dictionary", @"C:\DCS\Mission\sample.miz" );

            Assert.Null( result );
            Assert.NotNull( actualParameters );
            Assert.Equal(
                [
                    ConfirmationDialogResult.Confirm,
                    ConfirmationDialogResult.Secondary,
                    ConfirmationDialogResult.Cancel
                ],
                actualParameters.ButtonOrder );
            Assert.Equal( "MaterialDesignRaisedAffirmativeButton", actualParameters.ConfirmButtonStyleKey );
            Assert.Equal( "MaterialDesignRaisedNeutralButton", actualParameters.SecondaryButtonStyleKey );
            Assert.Equal( "MaterialDesignRaisedWarnButton", actualParameters.CancelButtonStyleKey );
        }
        finally {
            File.Delete( exportPath );
        }
    }

    /// <summary>
    /// TranslationCreationDialogService のテストコンテキストを表現する。
    /// </summary>
    private sealed class TranslationCreationDialogServiceTestContext {
        /// <summary>
        /// ダイアログサービスのモックを取得する。
        /// </summary>
        internal Mock<IDialogService> DialogServiceMock { get; } = new();

        /// <summary>
        /// ダイアログプロバイダーのモックを取得する。
        /// </summary>
        internal Mock<IDialogProvider> DialogProviderMock { get; } = new();

        /// <summary>
        /// ログサービスのモックを取得する。
        /// </summary>
        internal Mock<ILoggingService> LoggingServiceMock { get; } = new();

        /// <summary>
        /// テスト対象を生成する。
        /// </summary>
        /// <returns>生成したサービス。</returns>
        internal TranslationCreationDialogService CreateService() =>
            new(
                DialogServiceMock.Object,
                DialogProviderMock.Object,
                LoggingServiceMock.Object );
    }
}