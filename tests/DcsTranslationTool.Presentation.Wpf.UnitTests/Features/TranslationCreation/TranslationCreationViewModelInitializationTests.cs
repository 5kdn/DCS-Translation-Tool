using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Resources;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の起動と初期化の動作を検証する。
/// </summary>
public sealed class TranslationCreationViewModelInitializationTests {
    [Fact]
    public void コンストラクタは選択中アーカイブ絶対パスを保持する() {
        var context = new TranslationCreationViewModelTestContext();

        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mission1.miz" );

        Assert.Equal( @"C:\DCSWorld\Mission1.miz", viewModel.ArchiveFullPath );
    }

    [Fact]
    public void コンストラクタは空白のアーカイブ絶対パスを拒否する() {
        var context = new TranslationCreationViewModelTestContext();

        Assert.Throws<ArgumentException>( () => context.CreateViewModel( " " ) );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncは読込結果を画面状態へ反映する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationViewModelTestContext.CreateLoadResult(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ) { Translated = "Translated 1" },
                    new TranslationDictionaryItem( "DictKey_sortie_2", "Original 2" ),
                ] ) );
        var viewModel = context.CreateViewModel();

        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.True( viewModel.HasDictionaryItems );
        Assert.Equal( 2, viewModel.DictionaryItems.Count );
        Assert.Equal( string.Empty, viewModel.StatusMessage );
        Assert.False( viewModel.IsLoading );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncは失敗結果を状態メッセージへ反映する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationLoadResult.Failed() );
        var viewModel = context.CreateViewModel();

        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryLoadFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task InitializeAfterShownAsyncは重複実行しても読込を一度だけ行う() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel();

        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );
        await viewModel.InitializeAfterShownAsync( TestContext.Current.CancellationToken );

        context.WorkflowServiceMock.Verify(
            service => service.LoadAsync( viewModel.ArchiveFullPath, It.IsAny<CancellationToken>() ),
            Times.Once );
    }

    [Fact]
    public async Task HandleWindowLoadedAsyncは起動時クローズ要求を反映する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.CreateInitialPromptPlan(
                It.IsAny<TranslationCreationImportContext>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<TranslationDictionaryItem>>() ) )
            .Returns( new TranslationCreationInitialPromptPlan(
                true,
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ),
                ] ) );
        context.DialogServiceMock
            .Setup( service => service.PromptEmbeddedJapaneseDictionaryStartupAsync( It.IsAny<string>() ) )
            .ReturnsAsync( TranslationCreationEmbeddedJapaneseDictionaryStartupChoice.Close );
        var viewModel = context.CreateViewModel();

        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        Assert.True( viewModel.ShouldCloseAfterStartup );

        viewModel.AcknowledgeStartupCloseRequest();

        Assert.False( viewModel.ShouldCloseAfterStartup );
    }

    [Fact]
    public async Task HandleWindowLoadedAsyncは初期プロンプトを一度だけ処理する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.CreateInitialPromptPlan(
                It.IsAny<TranslationCreationImportContext>(),
                It.IsAny<bool>(),
                It.IsAny<IReadOnlyList<TranslationDictionaryItem>>() ) )
            .Returns( new TranslationCreationInitialPromptPlan(
                true,
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ),
                ] ) );
        context.DialogServiceMock
            .Setup( service => service.PromptEmbeddedJapaneseDictionaryStartupAsync( It.IsAny<string>() ) )
            .ReturnsAsync( TranslationCreationEmbeddedJapaneseDictionaryStartupChoice.ContinueWithoutImport );
        var viewModel = context.CreateViewModel();

        await Caliburn.Micro.ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );
        await viewModel.HandleWindowLoadedAsync( TestContext.Current.CancellationToken );

        context.DialogServiceMock.Verify(
            service => service.PromptEmbeddedJapaneseDictionaryStartupAsync( viewModel.ArchiveFullPath ),
            Times.Once );
    }
}