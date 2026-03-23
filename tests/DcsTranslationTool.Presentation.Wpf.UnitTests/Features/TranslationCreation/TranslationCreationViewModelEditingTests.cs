using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Resources;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の選択と編集の動作を検証する。
/// </summary>
public sealed class TranslationCreationViewModelEditingTests {
    [Fact]
    public async Task SelectedDictionaryItem設定時に詳細表示が追従する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationViewModelTestContext.CreateLoadResult(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "Original 2" ) { Translated = "Translated 2" },
                ] ) );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var item = viewModel.DictionaryItems[1];
        viewModel.SelectedDictionaryItem = item;

        Assert.Same( item, viewModel.SelectedDictionaryItem );
        Assert.Equal( item.Original, viewModel.SelectedOriginal );
        Assert.Equal( item.Translated, viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task SelectedTranslated更新時はFlushまで選択項目へ反映しない() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var item = viewModel.DictionaryItems.Single();
        viewModel.SelectedDictionaryItem = item;

        viewModel.SelectedTranslated = "translated";

        Assert.Equal( string.Empty, item.Translated );
        Assert.Equal( "translated", viewModel.SelectedTranslated );

        viewModel.FlushPendingSelectedTranslatedEdit();

        Assert.Equal( "translated", item.Translated );
    }

    [Fact]
    public async Task 無効行選択時にSelectedTranslated更新は無視する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationViewModelTestContext.CreateLoadResult(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ) { IsEnabled = false },
                ] ) );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var item = viewModel.DictionaryItems.Single();
        viewModel.SelectedDictionaryItem = item;
        viewModel.SelectedTranslated = "translated";
        viewModel.FlushPendingSelectedTranslatedEdit();

        Assert.False( viewModel.CanEditSelectedTranslated );
        Assert.Equal( string.Empty, item.Translated );
    }

    [Fact]
    public async Task 選択項目切替時に未反映のSelectedTranslatedを直前の項目へコミットする() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationViewModelTestContext.CreateLoadResult(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ),
                    new TranslationDictionaryItem( "DictKey_sortie_2", "Original 2" ),
                ] ) );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var first = viewModel.DictionaryItems[0];
        var second = viewModel.DictionaryItems[1];
        viewModel.SelectedDictionaryItem = first;
        viewModel.SelectedTranslated = "translated";

        viewModel.SelectedDictionaryItem = second;

        Assert.Equal( "translated", first.Translated );
        Assert.Same( second, viewModel.SelectedDictionaryItem );
    }

    [Fact]
    public async Task ConfirmCloseAsyncは未変更時に確認なしでtrueを返す() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var result = await viewModel.ConfirmCloseAsync();

        Assert.True( result );
        context.DialogServiceMock.Verify( service => service.ConfirmCloseAsync(), Times.Never );
    }

    [Fact]
    public async Task ConfirmCloseAsyncは変更時に確認ダイアログへ委譲する() {
        var context = new TranslationCreationViewModelTestContext();
        context.DialogServiceMock
            .Setup( service => service.ConfirmCloseAsync() )
            .ReturnsAsync( false );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        var result = await viewModel.ConfirmCloseAsync();

        Assert.False( result );
        context.DialogServiceMock.Verify( service => service.ConfirmCloseAsync(), Times.Once );
    }

    [Fact]
    public void CopyOriginalToClipboardはOriginalをクリップボードへ設定して通知する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel();
        var row = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_sortie_1", "Original 1" ) );

        viewModel.CopyOriginalToClipboard( row );

        context.SystemServiceMock.Verify( service => service.SetClipboardText( "Original 1" ), Times.Once );
        context.NotificationServiceMock.Verify(
            service => service.ShowCompleted( Strings_Translation.CreateTranslationOriginalCopiedMessage ),
            Times.Once );
    }
}