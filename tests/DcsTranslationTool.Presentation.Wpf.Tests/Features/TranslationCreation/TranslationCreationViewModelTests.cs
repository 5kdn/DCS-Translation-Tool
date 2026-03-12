using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Resources;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationViewModel の動作を検証する。
/// </summary>
public sealed class TranslationCreationViewModelTests {
    [Fact]
    public void コンストラクタは選択中アーカイブ絶対パスを保持する() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Equal( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz", viewModel.ArchiveFullPath );
    }

    [Fact]
    public void コンストラクタは空白のアーカイブ絶対パスを拒否する() {
        var context = new TranslationCreationViewModelTestContext();

        Assert.Throws<ArgumentException>( () => context.CreateViewModel( string.Empty ) );
    }

    [Fact]
    public void 初期状態では選択項目詳細は空文字になる() {
        var context = new TranslationCreationViewModelTestContext();
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        Assert.Null( viewModel.SelectedDictionaryItem );
        Assert.Equal( string.Empty, viewModel.SelectedOriginal );
        Assert.Equal( string.Empty, viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task ActivateAsyncはdictionary項目一覧を読み込む() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "k1", "o1" ),
                    new TranslationDictionaryItem( "k2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.Equal( 2, viewModel.DictionaryItems.Count );
        Assert.True( viewModel.HasDictionaryItems );
        Assert.False( viewModel.HasStatusMessage );
        Assert.False( viewModel.ShowOnlyUntranslated );
        Assert.True( viewModel.HidePossibleNonTranslationTargets );
        Assert.True( viewModel.HideEmptyOriginal );
        Assert.Equal( "o1", viewModel.DictionaryItems[0].Original );
        Assert.Equal( string.Empty, viewModel.DictionaryItems[0].Translated );
    }

    [Fact]
    public async Task SelectedDictionaryItem設定時に詳細表示が追従する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "t1" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" ) { Translated = "t2" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems[1];

        Assert.Equal( "o2", viewModel.SelectedOriginal );
        Assert.Equal( "t2", viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task SelectedTranslated更新時に選択項目へ反映する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedTranslated = "translated";

        Assert.Equal( "translated", viewModel.DictionaryItems.Single().Translated );
        Assert.Equal( "translated", viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task 選択中項目のTranslated変更時にSelectedTranslated変更通知を発火する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );
        var notifiedProperties = new List<string>();

        viewModel.PropertyChanged += ( _, e ) => {
            if(e.PropertyName is not null) {
                notifiedProperties.Add( e.PropertyName );
            }
        };

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();

        notifiedProperties.Clear();
        viewModel.DictionaryItems.Single().Translated = "translated";

        Assert.Contains( nameof( TranslationCreationViewModel.SelectedTranslated ), notifiedProperties );
    }

    [Fact]
    public async Task SelectedDictionaryItemをnullにすると詳細表示は空文字へ戻る() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "t1" }
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.SelectedDictionaryItem = viewModel.DictionaryItems.Single();
        viewModel.SelectedDictionaryItem = null;

        Assert.Equal( string.Empty, viewModel.SelectedOriginal );
        Assert.Equal( string.Empty, viewModel.SelectedTranslated );
    }

    [Fact]
    public async Task ActivateAsyncは指定した接頭辞優先順とKey昇順で並べる() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_z", "v8" ),
                    new TranslationDictionaryItem( "DictKey_other_b", "v7b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_b", "v6b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionNeutralsTask_b", "v5b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionRedTask_b", "v4b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionBlueTask_b", "v3b" ),
                    new TranslationDictionaryItem( "DictKey_descriptionText_b", "v2b" ),
                    new TranslationDictionaryItem( "DictKey_sortie_b", "v1b" ),
                    new TranslationDictionaryItem( "DictKey_sortie_a", "v1a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionText_a", "v2a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionBlueTask_a", "v3a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionRedTask_a", "v4a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionNeutralsTask_a", "v5a" ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_a", "v6a" ),
                    new TranslationDictionaryItem( "DictKey_other_a", "v7a" ),
                    new TranslationDictionaryItem( "AnotherKey_a", "v8a" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.Equal(
            [
                "DictKey_sortie_a",
                "DictKey_sortie_b",
                "DictKey_descriptionText_a",
                "DictKey_descriptionText_b",
                "DictKey_descriptionBlueTask_a",
                "DictKey_descriptionBlueTask_b",
                "DictKey_descriptionRedTask_a",
                "DictKey_descriptionRedTask_b",
                "DictKey_descriptionNeutralsTask_a",
                "DictKey_descriptionNeutralsTask_b",
                "DictKey_descriptionFoo_a",
                "DictKey_descriptionFoo_b",
                "DictKey_other_a",
                "DictKey_other_b",
                "AnotherKey_a",
                "OtherKey_z"
            ],
            viewModel.DictionaryItems.Select( item => item.Key ).ToArray() );
    }

    [Fact]
    public async Task ActivateAsyncはOriginalが空欄の行を既定で非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_blank", string.Empty ),
                    new TranslationDictionaryItem( "DictKey_filled", "value" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.Single( viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>() );
        Assert.Equal( "DictKey_filled", viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().Single().Key );
    }

    [Fact]
    public async Task ActivateAsyncは対象外候補の行を既定で非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_visible", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "DictKey_ActionComment_1", "o3" ),
                    new TranslationDictionaryItem( "DictKey_GroupName_1", "o4" ),
                    new TranslationDictionaryItem( "DictKey_UnitName_1", "o5" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o6" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.Equal(
            ["DictKey_sortie_1"],
            viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().Select( item => item.Key ).ToArray() );
    }

    [Fact]
    public async Task 対象外フィルターを無効にすると対象外候補の行を再表示する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_visible", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o3" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.HidePossibleNonTranslationTargets = false;

        Assert.Equal(
            ["DictKey_sortie_1", "DictKey_WptName_1", "OtherKey_visible"],
            viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().Select( item => item.Key ).ToArray() );
    }

    [Fact]
    public async Task ShowOnlyUntranslatedを有効にするとTranslated入力済み行を非表示にする() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", "o1" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.ShowOnlyUntranslated = true;

        var visibleItems = viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().ToArray();
        Assert.Single( visibleItems );
        Assert.Equal( "DictKey_descriptionFoo_2", visibleItems[0].Key );
    }

    [Fact]
    public async Task 二つのフィルターは同時適用される() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_blank", string.Empty ),
                    new TranslationDictionaryItem( "DictKey_done", "o1" ) { Translated = "translated" },
                    new TranslationDictionaryItem( "DictKey_todo", "o2" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.ShowOnlyUntranslated = true;

        var visibleItems = viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().ToArray();
        Assert.Single( visibleItems );
        Assert.Equal( "DictKey_todo", visibleItems[0].Key );
    }

    [Fact]
    public async Task 三つのフィルターは同時適用される() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "OtherKey_hidden", "o1" ),
                    new TranslationDictionaryItem( "DictKey_WptName_1", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_1", "o3" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_1", string.Empty ),
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_2", "o4" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.ShowOnlyUntranslated = true;

        Assert.Equal(
            ["DictKey_descriptionFoo_2"],
            viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().Select( item => item.Key ).ToArray() );
    }

    [Fact]
    public async Task フィルター適用後も表示順を維持する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_b", "o2" ),
                    new TranslationDictionaryItem( "DictKey_sortie_b", "o1" ) { Translated = "done" },
                    new TranslationDictionaryItem( "DictKey_descriptionFoo_a", "o3" ),
                    new TranslationDictionaryItem( "OtherKey_a", "o4" ),
                    new TranslationDictionaryItem( "DictKey_sortie_a", "o5" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.HidePossibleNonTranslationTargets = false;
        viewModel.ShowOnlyUntranslated = true;

        Assert.Equal(
            [
                "DictKey_sortie_a",
                "DictKey_descriptionFoo_a",
                "DictKey_descriptionFoo_b",
                "OtherKey_a"
            ],
            viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>().Select( item => item.Key ).ToArray() );
    }

    [Fact]
    public async Task Translated更新時に未翻訳フィルターを再適用する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>(
                [
                    new TranslationDictionaryItem( "k1", "o1" )
                ] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );
        viewModel.ShowOnlyUntranslated = true;

        var item = viewModel.DictionaryItems.Single();
        item.Translated = "translated";

        Assert.Empty( viewModel.FilteredDictionaryItemsView.Cast<TranslationDictionaryItemRowViewModel>() );
    }

    [Fact]
    public async Task ActivateAsyncは読込失敗時に状態メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Fail<IReadOnlyList<TranslationDictionaryItem>>( ResultErrorFactory.NotFound( "not found", "TEST" ) ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryLoadFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task ActivateAsyncはdictionary読込サービス例外時も状態メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Throws( new InvalidOperationException( "boom" ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryLoadFailedMessage, viewModel.StatusMessage );
    }

    [Fact]
    public async Task ActivateAsyncは空結果時に空状態メッセージを設定する() {
        var context = new TranslationCreationViewModelTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadDictionary( It.IsAny<string>() ) )
            .Returns( Result.Ok<IReadOnlyList<TranslationDictionaryItem>>( [] ) );
        var viewModel = context.CreateViewModel( @"C:\DCSWorld\Mods\aircraft\A10C\Mission1.miz" );

        await ScreenExtensions.TryActivateAsync( viewModel, TestContext.Current.CancellationToken );

        Assert.False( viewModel.HasDictionaryItems );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryEmptyMessage, viewModel.StatusMessage );
    }

    private sealed class TranslationCreationViewModelTestContext {
        internal Mock<ITranslationDictionaryService> TranslationDictionaryServiceMock { get; } = new();
        internal Mock<ILoggingService> LoggerMock { get; } = new();

        internal TranslationCreationViewModel CreateViewModel( string archiveFullPath ) =>
            new( archiveFullPath, LoggerMock.Object, TranslationDictionaryServiceMock.Object );
    }
}