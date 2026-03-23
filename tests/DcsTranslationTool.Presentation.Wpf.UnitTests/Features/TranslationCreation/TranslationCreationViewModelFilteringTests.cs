using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の表示フィルター動作を検証する。
/// </summary>
public sealed class TranslationCreationViewModelFilteringTests {
    [Fact]
    public async Task 初期状態では空Original行と対象外候補行を非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationLoadResult.Succeeded(
                new TranslationCreationDictionaryLoadState(
                    [
                        new TranslationDictionaryItem( "visible", "Original" ),
                        new TranslationDictionaryItem( "empty", "" ),
                        new TranslationDictionaryItem( "lua", "trigger.action.outText('x', 10)" ),
                    ],
                    [
                        new TranslationCreationRowState( new TranslationDictionaryItem( "visible", "Original" ) ),
                        new TranslationCreationRowState( new TranslationDictionaryItem( "empty", "" ) ),
                        new TranslationCreationRowState( new TranslationDictionaryItem( "lua", "trigger.action.outText('x', 10)" ), true ),
                    ] ),
                false,
                [],
                string.Empty ) );
        var viewModel = context.CreateViewModel();

        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var visible = TranslationCreationViewModelTestContext.GetVisibleDictionaryItems( viewModel );
        Assert.Single( visible );
        Assert.Equal( "visible", visible[0].Key );
    }

    [Fact]
    public async Task ShowEnabledItemsとShowDisabledItemsを両方無効にすると0件表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationViewModelTestContext.CreateLoadResult(
                [
                    new TranslationDictionaryItem( "enabled", "Original 1" ),
                    new TranslationDictionaryItem( "disabled", "Original 2" ) { IsEnabled = false },
                ] ) );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        viewModel.ShowEnabledItems = false;
        viewModel.ShowDisabledItems = false;

        Assert.Empty( TranslationCreationViewModelTestContext.GetVisibleDictionaryItems( viewModel ) );
    }

    [Fact]
    public async Task ShowOnlyUntranslatedはFlush後にTranslated入力済み行を非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.ShowOnlyUntranslated = true;
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.Single( TranslationCreationViewModelTestContext.GetVisibleDictionaryItems( viewModel ) );

        viewModel.FlushPendingSelectedTranslatedEdit();

        Assert.Empty( TranslationCreationViewModelTestContext.GetVisibleDictionaryItems( viewModel ) );
    }

    [Fact]
    public async Task MoveSelectionDownは非表示行を飛ばして次の表示行を選択する() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationLoadResult.Succeeded(
                new TranslationCreationDictionaryLoadState(
                    [
                        new TranslationDictionaryItem( "first", "Original 1" ),
                        new TranslationDictionaryItem( "hidden", "" ),
                        new TranslationDictionaryItem( "last", "Original 3" ),
                    ],
                    [
                        new TranslationCreationRowState( new TranslationDictionaryItem( "first", "Original 1" ) ),
                        new TranslationCreationRowState( new TranslationDictionaryItem( "hidden", "" ) ),
                        new TranslationCreationRowState( new TranslationDictionaryItem( "last", "Original 3" ) ),
                    ] ),
                false,
                [],
                string.Empty ) );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[0];

        var changed = viewModel.MoveSelectionDown();

        Assert.True( changed );
        Assert.Equal( "last", viewModel.SelectedDictionaryItem?.Key );
    }

    [Fact]
    public async Task MoveSelectionUpは表示項目が空のときnoOpにする() {
        var context = new TranslationCreationViewModelTestContext();
        context.WorkflowServiceMock
            .Setup( service => service.LoadAsync( It.IsAny<string>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( TranslationCreationLoadResult.Succeeded(
                new TranslationCreationDictionaryLoadState(
                    [
                        new TranslationDictionaryItem( "hidden", "" ),
                    ],
                    [
                        new TranslationCreationRowState( new TranslationDictionaryItem( "hidden", "" ) ),
                    ] ),
                false,
                [],
                string.Empty ) );
        var viewModel = context.CreateViewModel();
        await TranslationCreationViewModelTestContext.ActivateAndInitializeAfterShownAsync( viewModel );

        var changed = viewModel.MoveSelectionUp();

        Assert.False( changed );
        Assert.Null( viewModel.SelectedDictionaryItem );
    }
}