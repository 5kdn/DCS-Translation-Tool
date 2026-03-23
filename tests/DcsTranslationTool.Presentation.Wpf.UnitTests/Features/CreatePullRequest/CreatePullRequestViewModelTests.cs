using System.Text;

using Caliburn.Micro;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Shared.Models;
using DcsTranslationTool.TestCommon.IO;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.CreatePullRequest;

/// <summary>CreatePullRequestViewModel の動作を検証するテストを提供する。</summary>
public sealed class CreatePullRequestViewModelTests : IDisposable {
    private readonly TemporaryDirectory _temporaryDirectory = new( nameof( CreatePullRequestViewModelTests ) );

    public void Dispose() {
        _temporaryDirectory.Dispose();
    }

    /// <summary>CreatePullRequestを呼び出すと同意と変更種別が未充足の場合はAPIを呼び出さないことを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すと同意と変更種別が未充足の場合はAPIを呼び出さない() {
        var commitFilePath = CreateTempFile( "dummy.lua", "dummy content" );
        var parameters = CreateParameters(
            new CommitFile
            {
                Operation = CommitOperationType.Upsert,
                LocalPath = commitFilePath,
                RepoPath = "DCSWorld/Mods/sample.txt",
            } );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var inspectorMock = new Mock<IFileContentInspector>();
        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>( MockBehavior.Strict );
        luaSyntaxValidationServiceMock
            .Setup( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ) )
            .Returns( new LuaSyntaxValidationResult( [] ) );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = new CreatePullRequestViewModel(
            parameters,
            apiServiceMock.Object,
            inspectorMock.Object,
            luaSyntaxValidationServiceMock.Object,
            dialogServiceMock.Object,
            loggerMock.Object,
            systemServiceMock.Object );

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
        Assert.NotEmpty( viewModel.AgreementItems );

        Assert.False( viewModel.CanCreatePullRequest );

        AgreeAllAgreements( viewModel );

        Assert.True( viewModel.CanCreatePullRequest );
    }

    /// <summary>CreatePullRequestを呼び出すとAPIが失敗結果を返した場合に失敗レスポンスを伝播することを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すとAPIが失敗を返す場合はエラー結果を返す() {
        var commitFilePath = CreateTempFile( "Example.lua", "translated text" );
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

        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>();
        luaSyntaxValidationServiceMock
            .Setup( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ) )
            .Returns( new LuaSyntaxValidationResult( [] ) );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();
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
            luaSyntaxValidationServiceMock.Object,
            dialogServiceMock.Object,
            loggerMock.Object,
            systemServiceMock.Object,
            windowManagerMock.Object,
            CancellationToken.None );

        Assert.NotNull( capturedViewModel );
        var viewModel = capturedViewModel!;

        await viewModel.ActivateAsync( CancellationToken.None );

        viewModel.PullRequestChangeKinds
            .First( vm => vm.Kind == PullRequestChangeKind.ファイルの追加 )
            .IsChecked = true;
        AgreeAllAgreements( viewModel );
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
        var commitFilePath = CreateTempFile( "cancel.lua", "cancel content" );
        var parameters = CreateParameters(
            new CommitFile
            {
                Operation = CommitOperationType.Upsert,
                LocalPath = commitFilePath,
                RepoPath = "DCSWorld/Mods/sample-cancel.txt",
            } );

        var apiServiceMock = new Mock<IApiService>();
        var inspectorMock = new Mock<IFileContentInspector>();
        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>();
        var dialogServiceMock = new Mock<IDialogService>();
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();
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
            luaSyntaxValidationServiceMock.Object,
            dialogServiceMock.Object,
            loggerMock.Object,
            systemServiceMock.Object,
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

    /// <summary>CreatePullRequestを呼び出すとLua検証対象ファイルのみを検証してAPIを呼び出すことを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すとLua検証対象ファイルのみを検証してAPIを呼び出す() {
        var luaPath = CreateTempFile( "mission.lua", "return 1" );
        var cmpPath = CreateTempFile( "voice.cmp", "return 2" );
        var dictionaryPath = CreateTempFile( "dictionary", "dictionary = {}" );
        var textPath = CreateTempFile( "notes.txt", "plain text" );
        var parameters = CreateParameters(
            CreateUpsertFile( luaPath, "Repo/mission.lua" ),
            CreateUpsertFile( cmpPath, "Repo/voice.cmp" ),
            CreateUpsertFile( dictionaryPath, "Repo/dictionary" ),
            CreateUpsertFile( textPath, "Repo/notes.txt" ) );

        var apiServiceMock = new Mock<IApiService>();
        apiServiceMock
            .Setup( service => service.CreatePullRequestAsync( It.IsAny<ApiCreatePullRequestRequest>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok( new ApiCreatePullRequestOutcome( true, null, [] ) ) );
        var inspectorMock = CreateInspectorMock();
        IReadOnlyList<LuaSyntaxValidationTarget>? capturedTargets = null;
        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>();
        luaSyntaxValidationServiceMock
            .Setup( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ) )
            .Callback<IReadOnlyList<LuaSyntaxValidationTarget>>( targets => capturedTargets = targets )
            .Returns( new LuaSyntaxValidationResult( [] ) );
        var dialogServiceMock = new Mock<IDialogService>( MockBehavior.Strict );
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = CreateViewModel( parameters, apiServiceMock, inspectorMock, luaSyntaxValidationServiceMock, dialogServiceMock, loggerMock, systemServiceMock );
        await viewModel.ActivateAsync( CancellationToken.None );
        PrepareCreatableState( viewModel );

        await viewModel.CreatePullRequest();

        Assert.NotNull( capturedTargets );
        Assert.Collection(
            capturedTargets!,
            target => Assert.Equal( luaPath, target.FilePath ),
            target => Assert.Equal( cmpPath, target.FilePath ),
            target => Assert.Equal( dictionaryPath, target.FilePath ) );
        apiServiceMock.Verify( service => service.CreatePullRequestAsync( It.IsAny<ApiCreatePullRequestRequest>(), It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>CreatePullRequestを呼び出すとLua構文エラー時にダイアログを表示しAPIを呼び出さないことを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すとLua構文エラー時にダイアログを表示しAPIを呼び出さない() {
        var luaPath = CreateTempFile( "broken.lua", "function(" );
        var parameters = CreateParameters( CreateUpsertFile( luaPath, "Repo/broken.lua" ) );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var inspectorMock = CreateInspectorMock();
        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>();
        luaSyntaxValidationServiceMock
            .Setup( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ) )
            .Returns( new LuaSyntaxValidationResult( [new LuaSyntaxValidationFailure( luaPath, "syntax error" )] ) );
        LuaSyntaxValidationFailureDialogParameters? capturedParameters = null;
        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup( service => service.LuaSyntaxValidationFailureDialogShowAsync( It.IsAny<LuaSyntaxValidationFailureDialogParameters>() ) )
            .Callback<LuaSyntaxValidationFailureDialogParameters>( parameters => capturedParameters = parameters )
            .ReturnsAsync( LuaSyntaxValidationFailureDialogResult.Cancel );
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = CreateViewModel( parameters, apiServiceMock, inspectorMock, luaSyntaxValidationServiceMock, dialogServiceMock, loggerMock, systemServiceMock );
        await viewModel.ActivateAsync( CancellationToken.None );
        PrepareCreatableState( viewModel );

        await viewModel.CreatePullRequest();

        Assert.NotNull( capturedParameters );
        Assert.Contains( luaPath, capturedParameters!.FailedFilePaths );
        Assert.Equal( CreatePullRequestDialogHostIdentifiers.Validation, capturedParameters.DialogIdentifier );
        apiServiceMock.Verify( service => service.CreatePullRequestAsync( It.IsAny<ApiCreatePullRequestRequest>(), It.IsAny<CancellationToken>() ), Times.Never );
    }

    /// <summary>CreatePullRequestを呼び出すとリトライ選択後に再検証して成功時はAPIを呼び出すことを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すとリトライ選択後に再検証して成功時はAPIを呼び出す() {
        var luaPath = CreateTempFile( "retry.lua", "return 1" );
        var parameters = CreateParameters( CreateUpsertFile( luaPath, "Repo/retry.lua" ) );

        var apiServiceMock = new Mock<IApiService>();
        apiServiceMock
            .Setup( service => service.CreatePullRequestAsync( It.IsAny<ApiCreatePullRequestRequest>(), It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok( new ApiCreatePullRequestOutcome( true, null, [] ) ) );
        var inspectorMock = CreateInspectorMock();
        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>();
        luaSyntaxValidationServiceMock
            .SetupSequence( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ) )
            .Returns( new LuaSyntaxValidationResult( [new LuaSyntaxValidationFailure( luaPath, "syntax error" )] ) )
            .Returns( new LuaSyntaxValidationResult( [] ) );
        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup( service => service.LuaSyntaxValidationFailureDialogShowAsync( It.IsAny<LuaSyntaxValidationFailureDialogParameters>() ) )
            .ReturnsAsync( LuaSyntaxValidationFailureDialogResult.Retry );
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();

        var viewModel = CreateViewModel( parameters, apiServiceMock, inspectorMock, luaSyntaxValidationServiceMock, dialogServiceMock, loggerMock, systemServiceMock );
        await viewModel.ActivateAsync( CancellationToken.None );
        PrepareCreatableState( viewModel );

        await viewModel.CreatePullRequest();

        luaSyntaxValidationServiceMock.Verify( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ), Times.Exactly( 2 ) );
        apiServiceMock.Verify( service => service.CreatePullRequestAsync( It.IsAny<ApiCreatePullRequestRequest>(), It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>CreatePullRequestを呼び出すとLua構文エラーで中止選択時に失敗結果で終了することを確認する。</summary>
    [StaFact]
    public async Task CreatePullRequestを呼び出すとLua構文エラーで中止選択時に失敗結果で終了する() {
        var luaPath = CreateTempFile( "cancel-failure.lua", "function(" );
        var parameters = CreateParameters( CreateUpsertFile( luaPath, "Repo/cancel-failure.lua" ) );

        var apiServiceMock = new Mock<IApiService>( MockBehavior.Strict );
        var inspectorMock = CreateInspectorMock();
        var luaSyntaxValidationServiceMock = new Mock<ILuaSyntaxValidationService>();
        luaSyntaxValidationServiceMock
            .Setup( service => service.Validate( It.IsAny<IReadOnlyList<LuaSyntaxValidationTarget>>() ) )
            .Returns( new LuaSyntaxValidationResult( [new LuaSyntaxValidationFailure( luaPath, "syntax error" )] ) );
        var dialogServiceMock = new Mock<IDialogService>();
        dialogServiceMock
            .Setup( service => service.LuaSyntaxValidationFailureDialogShowAsync( It.IsAny<LuaSyntaxValidationFailureDialogParameters>() ) )
            .ReturnsAsync( LuaSyntaxValidationFailureDialogResult.Cancel );
        var loggerMock = new Mock<ILoggingService>();
        var systemServiceMock = new Mock<ISystemService>();
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
            luaSyntaxValidationServiceMock.Object,
            dialogServiceMock.Object,
            loggerMock.Object,
            systemServiceMock.Object,
            windowManagerMock.Object,
            CancellationToken.None );

        Assert.NotNull( capturedViewModel );
        await capturedViewModel!.ActivateAsync( CancellationToken.None );
        PrepareCreatableState( capturedViewModel );

        await capturedViewModel.CreatePullRequest();
        var result = await dialogTask;

        Assert.False( result.IsOk );
        Assert.NotNull( result.Errors );
        Assert.Contains( result.Errors!, error => error.Message.Contains( "Lua 構文チェック", StringComparison.Ordinal ) );
        apiServiceMock.Verify( service => service.CreatePullRequestAsync( It.IsAny<ApiCreatePullRequestRequest>(), It.IsAny<CancellationToken>() ), Times.Never );
    }

    private string CreateTempFile( string fileName, string content ) {
        return _temporaryDirectory.CreateFile( fileName, content, Encoding.UTF8 );
    }

    private static void AgreeAllAgreements( CreatePullRequestViewModel viewModel ) {
        foreach(var item in viewModel.AgreementItems) {
            item.IsAgreed = true;
        }
    }

    private static void PrepareCreatableState( CreatePullRequestViewModel viewModel ) {
        viewModel.PullRequestChangeKinds.First().IsChecked = true;
        viewModel.PRSummary = "サマリ";
        AgreeAllAgreements( viewModel );
    }

    private static CommitFile CreateUpsertFile( string localPath, string repoPath ) =>
        new()
        {
            Operation = CommitOperationType.Upsert,
            LocalPath = localPath,
            RepoPath = repoPath,
        };

    private static Mock<IFileContentInspector> CreateInspectorMock() {
        var inspectorMock = new Mock<IFileContentInspector>();
        inspectorMock
            .Setup( inspector => inspector.Inspect( It.IsAny<byte[]>() ) )
            .Returns<byte[]>( bytes => new FileContentInfo(
                false,
                Encoding.UTF8,
                1.0,
                Encoding.UTF8.GetString( bytes ),
                bytes.Length ) );
        return inspectorMock;
    }

    private static CreatePullRequestViewModel CreateViewModel(
        CreatePullRequestDialogParameters parameters,
        Mock<IApiService> apiServiceMock,
        Mock<IFileContentInspector> inspectorMock,
        Mock<ILuaSyntaxValidationService> luaSyntaxValidationServiceMock,
        Mock<IDialogService> dialogServiceMock,
        Mock<ILoggingService> loggerMock,
        Mock<ISystemService> systemServiceMock ) {
        return new CreatePullRequestViewModel(
            parameters,
            apiServiceMock.Object,
            inspectorMock.Object,
            luaSyntaxValidationServiceMock.Object,
            dialogServiceMock.Object,
            loggerMock.Object,
            systemServiceMock.Object );
    }

    private static CreatePullRequestDialogParameters CreateParameters( params CommitFile[] files ) =>
        new()
        {
            Category = "Aircraft",
            SubCategory = "A10C",
            CommitFiles = files,
        };
}