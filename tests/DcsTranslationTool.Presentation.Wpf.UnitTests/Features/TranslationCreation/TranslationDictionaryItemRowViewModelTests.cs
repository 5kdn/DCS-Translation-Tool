using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// <see cref="TranslationDictionaryItemRowViewModel"/> の動作を検証する。
/// </summary>
public sealed class TranslationDictionaryItemRowViewModelTests {
    [Fact]
    public void コンストラクタは翻訳対象外候補フラグを保持する() {
        var viewModel = new TranslationDictionaryItemRowViewModel(
            new TranslationDictionaryItem( "DictKey_script_1", "trigger.action.outText('x', 10)" ),
            true );

        Assert.True( viewModel.IsPossibleNonTranslationTarget );
    }

    [Fact]
    public void UpdatePendingChangesはTranslated変更でdirtyになる() {
        var viewModel = new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) )
        {
            Translated = "translated"
        };

        Assert.True( viewModel.UpdatePendingChanges() );
        Assert.True( viewModel.HasPendingChanges );
    }

    [Fact]
    public void UpdatePendingChangesは元に戻すとdirtyが解除される() {
        var viewModel = new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) )
        {
            Translated = "translated"
        };
        _ = viewModel.UpdatePendingChanges();

        viewModel.Translated = string.Empty;

        Assert.True( viewModel.UpdatePendingChanges() );
        Assert.False( viewModel.HasPendingChanges );
    }

    [Fact]
    public void ResetPendingChangesBaselineは現在値を基準へ更新する() {
        var viewModel = new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", "o1" ) )
        {
            Translated = "translated",
            IsEnabled = false
        };
        _ = viewModel.UpdatePendingChanges();

        viewModel.ResetPendingChangesBaseline();

        Assert.False( viewModel.HasPendingChanges );
        Assert.False( viewModel.UpdatePendingChanges() );
    }
}