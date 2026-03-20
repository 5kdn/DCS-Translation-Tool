using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Resources;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// <see cref="TranslationCreationWorkflowService"/> の動作を検証する。
/// </summary>
public sealed class TranslationCreationWorkflowServiceTests {
    [Fact]
    public async Task LoadAsyncは読込失敗時に失敗結果を返す() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TranslationCreationWorkflowServiceTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Fail<TranslationArchiveDictionaries>( "failed" ) );
        var sut = context.CreateSut();

        var result = await sut.LoadAsync( @"C:\DCSWorld\Mission1.miz", cancellationToken );

        Assert.False( result.IsSuccess );
        Assert.Equal( Strings_Translation.CreateTranslationDictionaryLoadFailedMessage, result.StatusMessage );
        Assert.Empty( result.LoadState.RowStates );
    }

    [Fact]
    public async Task LoadAsyncは読込成功時に状態を返す() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TranslationCreationWorkflowServiceTestContext();
        context.TranslationDictionaryServiceMock
            .Setup( service => service.LoadArchiveDictionaries( It.IsAny<string>() ) )
            .Returns( Result.Ok( new TranslationArchiveDictionaries(
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original" )
                ],
                true,
                [
                    new TranslationDictionaryItem( "DictKey_sortie_1", "Original" )
                ] ) ) );
        var sut = context.CreateSut();

        var result = await sut.LoadAsync( @"C:\DCSWorld\Mission1.miz", cancellationToken );

        Assert.True( result.IsSuccess );
        Assert.True( result.HasJapaneseDictionary );
        Assert.Single( result.LoadState.RowStates );
        Assert.Equal( string.Empty, result.StatusMessage );
    }

    [Fact]
    public async Task HandleEmbeddedJapaneseDictionaryAsyncは警告確認で拒否されたらウィンドウを閉じる() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TranslationCreationWorkflowServiceTestContext();
        context.DialogServiceMock
            .Setup( service => service.ConfirmArchiveContainsJapaneseDictionaryAsync( It.IsAny<string>() ) )
            .ReturnsAsync( false );
        var sut = context.CreateSut();

        var result = await sut.HandleEmbeddedJapaneseDictionaryAsync(
            @"C:\DCSWorld\Mission1.miz",
            new TranslationCreationImportContext(
                [
                    new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", "Original" ) )
                ],
                false ),
            true,
            [
                new TranslationDictionaryItem( "DictKey_sortie_1", "Original" )
            ],
            cancellationToken );

        Assert.True( result.ShouldCloseWindow );
        Assert.Null( result.CommandResult );
    }

    [Fact]
    public async Task HandleEmbeddedJapaneseDictionaryAsyncは確認後に取り込み結果を返す() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TranslationCreationWorkflowServiceTestContext();
        context.DialogServiceMock
            .Setup( service => service.ConfirmArchiveContainsJapaneseDictionaryAsync( It.IsAny<string>() ) )
            .ReturnsAsync( true );
        context.DialogServiceMock
            .Setup( service => service.ConfirmJapaneseDictionaryImportAsync() )
            .ReturnsAsync( true );
        context.ImportExportServiceMock
            .Setup( service => service.ImportJapaneseDictionaryAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<TranslationDictionaryItemRowViewModel>>(),
                It.IsAny<IReadOnlyList<TranslationDictionaryItem>>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationCreationCommandResult( true, false, false, 1, "imported" ) );
        var sut = context.CreateSut();

        var result = await sut.HandleEmbeddedJapaneseDictionaryAsync(
            @"C:\DCSWorld\Mission1.miz",
            new TranslationCreationImportContext(
                [
                    new TranslationDictionaryItemRowViewModel( new TranslationDictionaryItem( "DictKey_sortie_1", "Original" ) )
                ],
                false ),
            true,
            [
                new TranslationDictionaryItem( "DictKey_sortie_1", "Original" )
            ],
            cancellationToken );

        Assert.False( result.ShouldCloseWindow );
        Assert.NotNull( result.CommandResult );
        Assert.Equal( "imported", result.CommandResult!.StatusMessage );
    }

    [Fact]
    public async Task ExportAsyncは指定形式のサービスへ委譲する() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var context = new TranslationCreationWorkflowServiceTestContext();
        context.ImportExportServiceMock
            .Setup( service => service.ExportCsvAsync(
                It.IsAny<string>(),
                It.IsAny<TranslationCreationDocumentSnapshot>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( new TranslationCreationCommandResult( true, false, false, 0, "exported" ) );
        var sut = context.CreateSut();

        var result = await sut.ExportAsync(
            @"C:\DCSWorld\Mission1.miz",
            TranslationCreationExportFormat.Csv,
            new TranslationCreationDocumentSnapshot( [] ),
            cancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( "exported", result.StatusMessage );
        context.ImportExportServiceMock.Verify( service => service.ExportCsvAsync(
            @"C:\DCSWorld\Mission1.miz",
            It.IsAny<TranslationCreationDocumentSnapshot>(),
            It.IsAny<CancellationToken>() ), Times.Once );
    }

    private sealed class TranslationCreationWorkflowServiceTestContext {
        internal Mock<ITranslationDictionaryService> TranslationDictionaryServiceMock { get; } = new();
        internal Mock<ITranslationCreationDialogService> DialogServiceMock { get; } = new();
        internal Mock<ITranslationCreationImportExportService> ImportExportServiceMock { get; } = new();
        internal Mock<ILoggingService> LoggerMock { get; } = new();

        internal TranslationCreationWorkflowService CreateSut() =>
            new(
                new TranslationCreationDictionaryLoader( TranslationDictionaryServiceMock.Object ),
                DialogServiceMock.Object,
                ImportExportServiceMock.Object,
                LoggerMock.Object );
    }
}