using System.Text;

using Caliburn.Micro;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.CreatePullRequest;

/// <summary>CreatePullRequestViewModel の動作を検証するテストを提供する。</summary>
public sealed class CreatePullRequestViewModelTests : IDisposable {
    private readonly string _tempDir = Path.Combine( Path.GetTempPath(), $"CreatePullRequestViewModelTests_{Guid.NewGuid():N}" );

    public CreatePullRequestViewModelTests() {
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    /// <summary>CreatePullRequestを呼び出すと同意と変更種別が未充足の場合はAPIを呼び出さないことを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すと同意と変更種別が未充足の場合はAPIを呼び出さない() {
        var commitFilePath = CreateTempFile( "dummy content" );
        var parameters = CreateParameters(
            new CommitFile
            {
                Operation = CommitOperationType.Upsert,
                LocalPath = commitFilePath,
                RepoPath = "DCSWorld/Mods/sample.txt",
            } );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var inspectorMock = new Mock<IFileContentInspector>();
        var loggerMock = new Mock<ILoggingService>();

        var viewModel = new CreatePullRequestViewModel(
            parameters,
            apiServiceMock.Object,
            inspectorMock.Object,
            loggerMock.Object );

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.False( viewModel.CanCreatePullRequest );

        var kind = viewModel.PullRequestChangeKinds.First();
        kind.IsChecked = true;

        Assert.False( viewModel.CanCreatePullRequest );

        await viewModel.CreatePullRequest();

        apiServiceMock.Verify(
            service => service.CreatePullRequestAsync(
                It.IsAny<ApiCreatePullRequestRequest>(),
                It.IsAny<CancellationToken>() ),
            Times.Never );
        Assert.False( viewModel.IsCreatingPullRequest );

        viewModel.PRSummary = "サマリ";
        viewModel.AgreementItems[0].IsAgreed = true;

        Assert.True( viewModel.CanCreatePullRequest );
    }

    /// <summary>CreatePullRequestを呼び出すとAPIが失敗結果を返した場合に失敗レスポンスを伝播することを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すとAPIが失敗を返す場合はエラー結果を返す() {
        var commitFilePath = CreateTempFile( "translated text" );
        var parameters = CreateParameters(
            new CommitFile
            {
                Operation = CommitOperationType.Upsert,
                LocalPath = commitFilePath,
                RepoPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua",
            } );

        var apiServiceMock = new Mock<IApiService>();
        apiServiceMock
            .Setup( service => service.CreatePullRequestAsync(
                It.IsAny<ApiCreatePullRequestRequest>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Fail<ApiCreatePullRequestOutcome>( "API failure" ) );

        var inspectorMock = new Mock<IFileContentInspector>();
        inspectorMock
            .Setup( inspector => inspector.Inspect( It.IsAny<byte[]>() ) )
            .Returns<byte[]>( bytes => new FileContentInfo(
                false,
                Encoding.UTF8,
                1.0,
                Encoding.UTF8.GetString( bytes ),
                bytes.Length ) );

        var loggerMock = new Mock<ILoggingService>();
        var windowManagerMock = new Mock<IWindowManager>();
        CreatePullRequestViewModel? capturedViewModel = null;
        windowManagerMock
            .Setup( manager => manager.ShowDialogAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .Callback<object, object?, IDictionary<string, object>?>( ( model, _, _ ) => capturedViewModel = (CreatePullRequestViewModel)model )
            .ReturnsAsync( (bool?)true );

        var dialogTask = CreatePullRequestViewModel.ShowDialogAsync(
            parameters,
            apiServiceMock.Object,
            inspectorMock.Object,
            loggerMock.Object,
            windowManagerMock.Object,
            CancellationToken.None );

        Assert.NotNull( capturedViewModel );
        var viewModel = capturedViewModel!;

        await viewModel.ActivateAsync( CancellationToken.None );

        viewModel.PullRequestChangeKinds
            .First( vm => vm.Kind == PullRequestChangeKind.ファイルの追加 )
            .IsChecked = true;
        viewModel.AgreementItems[0].IsAgreed = true;
        viewModel.PRSummary = "Pull Request Summary";

        await viewModel.CreatePullRequest();

        var result = await dialogTask;

        Assert.False( result.IsOk );
        Assert.NotNull( result.Errors );
        Assert.Contains( result.Errors!, error => error.Message.Contains( "API failure", StringComparison.Ordinal ) );
        apiServiceMock.Verify(
            service => service.CreatePullRequestAsync(
                It.IsAny<ApiCreatePullRequestRequest>(),
                It.IsAny<CancellationToken>() ),
            Times.Once );
    }

    /// <summary>ShowDialogAsyncを呼び出すとキャンセル要求時にOperationCanceledExceptionを送出することを確認する。</summary>
    [StaFact]
    public async Task ShowDialogAsyncを呼び出すとキャンセル時にOperationCanceledExceptionを送出する() {
        var commitFilePath = CreateTempFile( "cancel content" );
        var parameters = CreateParameters(
            new CommitFile
            {
                Operation = CommitOperationType.Upsert,
                LocalPath = commitFilePath,
                RepoPath = "DCSWorld/Mods/sample-cancel.txt",
            } );

        var apiServiceMock = new Mock<IApiService>();
        var inspectorMock = new Mock<IFileContentInspector>();
        var loggerMock = new Mock<ILoggingService>();
        var windowManagerMock = new Mock<IWindowManager>();
        windowManagerMock
            .Setup( manager => manager.ShowDialogAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .ReturnsAsync( (bool?)true );

        using var cts = new CancellationTokenSource();

        var dialogTask = CreatePullRequestViewModel.ShowDialogAsync(
            parameters,
            apiServiceMock.Object,
            inspectorMock.Object,
            loggerMock.Object,
            windowManagerMock.Object,
            cts.Token );

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>( async () => await dialogTask );

        apiServiceMock.Verify(
            service => service.CreatePullRequestAsync(
                It.IsAny<ApiCreatePullRequestRequest>(),
                It.IsAny<CancellationToken>() ),
            Times.Never );
    }

    private string CreateTempFile( string content ) {
        var path = Path.Combine( _tempDir, $"{Guid.NewGuid():N}.txt" );
        File.WriteAllText( path, content, Encoding.UTF8 );
        return path;
    }

    private static CreatePullRequestDialogParameters CreateParameters( params CommitFile[] files ) =>
        new()
        {
            Category = "Aircraft",
            SubCategory = "A10C",
            CommitFiles = files,
        };
}