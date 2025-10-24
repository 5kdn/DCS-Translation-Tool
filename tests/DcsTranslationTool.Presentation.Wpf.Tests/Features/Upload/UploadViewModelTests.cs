using System.Text;

using Caliburn.Micro;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignThemes.Wpf;

using Moq;

using Action = System.Action;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.Upload;

/// <summary>UploadViewModel の動作を検証するテストを提供する。</summary>
public sealed class UploadViewModelTests : IDisposable {
    private readonly string _tempDir;
    public UploadViewModelTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"DownloadViewModelTests_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( _tempDir );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    /// <summary>Fetch 成功時にタブ構造と通知が更新されることを確認する。</summary>
    [StaFact]
    public async Task Fetchを呼び出すと正常取得時にタブと通知を更新する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-sha" )
        };

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var viewModel = context.CreateViewModel();

        await viewModel.Fetch();

        Assert.Contains( viewModel.RepoEntries, entry => entry.Path == repoEntries[0].Path );
        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        Assert.Contains( aircraftTab.Root.Children, child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
        Assert.Contains( "ファイル一覧の取得が完了しました", context.SnackbarMessages );
        Assert.False( viewModel.IsFetching );

        context.ApiServiceMock.Verify( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ), Times.Once );
        context.SnackbarServiceMock.Verify(
            service => service.Show(
                "ファイル一覧の取得が完了しました",
                It.IsAny<string?>(),
                It.IsAny<Action?>(),
                It.IsAny<object?>(),
                It.IsAny<TimeSpan?>() ),
            Times.Once );
    }

    /// <summary>Fetch 失敗時にエラーメッセージが提示されることを確認する。</summary>
    [StaFact]
    public async Task Fetchを呼び出すと取得失敗時にエラーメッセージを通知する() {
        var context = new UploadViewModelTestContext( "C:\\Dummy" );

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Fail<IReadOnlyList<FileEntry>>( "network error" ) );

        var viewModel = context.CreateViewModel();

        await viewModel.Fetch();

        Assert.Empty( viewModel.RepoEntries );
        Assert.Empty( viewModel.Tabs );
        Assert.Contains( "リポジトリファイル一覧の取得に失敗しました", context.SnackbarMessages );
        Assert.False( viewModel.IsFetching );

        context.ApiServiceMock.Verify( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ), Times.Once );
    }

    /// <summary>ActivateAsync で監視購読が有効化され EntriesChanged に応答することを確認する。</summary>
    [StaFact]
    public async Task ActivateAsyncを呼び出すとファイル監視と購読が開始される() {
        var context = new UploadViewModelTestContext( _tempDir );

        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-sha" )
        };
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "local-sha" )
        };

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );

        Assert.Contains( _tempDir, context.WatchedPaths );
        context.FileEntryServiceMock.Verify( service => service.Dispose(), Times.AtLeastOnce );

        await context.RaiseEntriesChangedAsync( localEntries );

        Assert.Equal( localEntries.Select( entry => entry.Path ), viewModel.LocalEntries.Select( entry => entry.Path ) );
        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        Assert.Contains( aircraftTab.Root.Children, child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
    }

    /// <summary>ShowCreatePullRequestDialog を呼び出すと PR 成功時にブラウザ起動アクションをスナックバーへ通知することを確認する。</summary>
    [StaFact]
    public async Task ShowCreatePullRequestDialogを呼び出すと成功時にブラウザ起動アクションを提供する() {
        var context = new UploadViewModelTestContext( _tempDir );
        var repoEntries = new FileEntry[]
        {
            new RepoFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "repo-sha" )
        };
        var localEntries = new FileEntry[]
        {
            new LocalFileEntry( "Example.lua", "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua", false, "local-sha" )
        };

        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( repoEntries ) );

        var prUrl = new Uri( "https://github.com/5kdn/DCS-Translation-Japanese/pull/123", UriKind.Absolute );
        context.ApiServiceMock
            .Setup( api => api.CreatePullRequestAsync(
                It.IsAny<ApiCreatePullRequestRequest>(),
                It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok(
                new ApiCreatePullRequestOutcome(
                    true,
                    "ok",
                    [
                        new ApiCreatePullRequestEntry(
                            "feat/aircraft/A10C",
                            "commit-sha",
                            123,
                            prUrl,
                            null )
                    ] ) ) );

        context.FileContentInspectorMock
            .Setup( inspector => inspector.Inspect( It.IsAny<byte[]>() ) )
            .Returns<byte[]>( bytes => new FileContentInfo(
                false,
                Encoding.UTF8,
                1.0,
                Encoding.UTF8.GetString( bytes ),
                bytes.Length ) );

        context.SystemServiceMock
            .Setup( service => service.OpenInWebBrowser( It.IsAny<string>() ) );

        var viewModel = context.CreateViewModel();

        var absolutePath = Path.Combine(
            context.TranslateDirectory,
            localEntries[0].Path.Replace( "/", Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal ) );
        Directory.CreateDirectory( Path.GetDirectoryName( absolutePath )! );
        File.WriteAllText( absolutePath, "translated content" );

        await viewModel.ActivateAsync( CancellationToken.None );
        await context.RaiseEntriesChangedAsync( localEntries );

        var aircraftTab = viewModel.Tabs.First( tab => tab.TabType == CategoryType.Aircraft );
        var moduleNode = aircraftTab.Root.Children.First( child => string.Equals( child?.Name, "A10C", StringComparison.Ordinal ) );
        moduleNode.CheckState = true;

        //var dialogSource = new TaskCompletionSource<CreatePullRequestViewModel>();
        var dialogSource = new TaskCompletionSource<CreatePullRequestViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        context.WindowManagerMock
            .Setup( manager => manager.ShowDialogAsync(
                It.IsAny<object>(),
                It.IsAny<object?>(),
                It.IsAny<IDictionary<string, object>?>() ) )
            .Callback<object, object?, IDictionary<string, object>?>( ( model, _, _ ) => dialogSource.TrySetResult( (CreatePullRequestViewModel)model ) )
            .ReturnsAsync( (bool?)true );

        var showTask = viewModel.ShowCreatePullRequestDialog();

        //var dialogViewModel = await dialogSource.Task;
        // ShowDialogAsync が呼ばれない場合にハングさせない
        var completed = await Task.WhenAny( dialogSource.Task, Task.Delay( TimeSpan.FromSeconds( 5 ) ) );
        Assert.True( ReferenceEquals( completed, dialogSource.Task ), "ShowDialogAsync が呼ばれずダイアログが表示されませんでした。前提条件を見直してください。" );
        var dialogViewModel = await dialogSource.Task;

        await dialogViewModel.ActivateAsync( CancellationToken.None );
        dialogViewModel.PullRequestChangeKinds[0].IsChecked = true;
        dialogViewModel.AgreementItems[0].IsAgreed = true;

        await dialogViewModel.CreatePullRequest();
        await showTask;

        // ダイアログが実際に呼ばれたことを保証
        context.WindowManagerMock.Verify( m => m.ShowDialogAsync(
            It.IsAny<object>(), It.IsAny<object?>(), It.IsAny<IDictionary<string, object>?>() ),
            Times.Once );

        var notification = context.SnackbarNotifications.Single( tuple => tuple.Message == "Pull Request の作成に成功しました" );
        Assert.Equal( "開く", notification.ActionContent );
        Assert.NotNull( notification.Handler );

        notification.Handler!.Invoke();

        context.SystemServiceMock.Verify(
            service => service.OpenInWebBrowser( It.Is<string>( url => string.Equals( url, prUrl.ToString(), StringComparison.Ordinal ) ) ),
            Times.Once );
        context.ApiServiceMock.Verify(
            api => api.CreatePullRequestAsync(
                It.IsAny<ApiCreatePullRequestRequest>(),
                It.IsAny<CancellationToken>() ),
            Times.Once );
    }

    /// <summary>ActivateAsync を再実行しても購読が重複しないことを確認する。</summary>
    [StaFact]
    public async Task ActivateAsyncを再度呼び出すと購読が重複しない() {
        var context = new UploadViewModelTestContext( _tempDir );
        context.ApiServiceMock
            .Setup( api => api.GetTreeAsync( It.IsAny<CancellationToken>() ) )
            .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );

        var viewModel = context.CreateViewModel();

        await viewModel.ActivateAsync( CancellationToken.None );
        var firstHandler = context.EntriesChangedHandlers.Single();
        Assert.Equal( 1, context.EntriesChangedSubscribeCount );
        Assert.Equal( 0, context.EntriesChangedUnsubscribeCount );

        await viewModel.ActivateAsync( CancellationToken.None );
        var secondHandler = context.EntriesChangedHandlers.Single();
        Assert.NotSame( firstHandler, secondHandler );
        Assert.Equal( 2, context.EntriesChangedSubscribeCount );
        Assert.Equal( 1, context.EntriesChangedUnsubscribeCount );
    }

    /// <summary>UploadViewModel の依存関係を構築するコンテキストを表す。</summary>
    private sealed class UploadViewModelTestContext( string translateDirectory ) {
        private readonly AppSettings _appSettings = new() { TranslateFileDir = translateDirectory };
        private readonly List<Func<IReadOnlyList<FileEntry>, Task>> _entriesChangedHandlers = [];

        public Mock<IApiService> ApiServiceMock { get; } = new( MockBehavior.Strict );
        public Mock<IAppSettingsService> AppSettingsServiceMock { get; } = new();
        public Mock<IDispatcherService> DispatcherServiceMock { get; } = new();
        public Mock<IFileContentInspector> FileContentInspectorMock { get; } = new();
        public Mock<IFileEntryService> FileEntryServiceMock { get; } = new();
        public Mock<ILoggingService> LoggingServiceMock { get; } = new();
        public Mock<ISnackbarService> SnackbarServiceMock { get; } = new();
        public Mock<ISystemService> SystemServiceMock { get; } = new();
        public Mock<IWindowManager> WindowManagerMock { get; } = new();
        public Mock<ISnackbarMessageQueue> SnackbarMessageQueueMock { get; } = new();

        public string TranslateDirectory => _appSettings.TranslateFileDir;
        public List<string> SnackbarMessages { get; } = [];
        public List<(string Message, string? ActionContent, Action? Handler)> SnackbarNotifications { get; } = [];
        public List<string> WatchedPaths { get; } = [];
        public IReadOnlyList<Func<IReadOnlyList<FileEntry>, Task>> EntriesChangedHandlers => _entriesChangedHandlers;
        public int EntriesChangedSubscribeCount { get; private set; }
        public int EntriesChangedUnsubscribeCount { get; private set; }
        private bool _initialized;

        private void EnsureInitialized() {
            if(_initialized) return;
            _initialized = true;

            AppSettingsServiceMock
                .SetupGet( service => service.Settings )
                .Returns( _appSettings );

            DispatcherServiceMock
                .Setup( service => service.InvokeAsync( It.IsAny<Func<Task>>() ) )
                .Returns<Func<Task>>( func => func() );

            FileEntryServiceMock
                .Setup( service => service.Dispose() );
            FileEntryServiceMock
                .Setup( service => service.GetEntriesAsync() )
                .ReturnsAsync( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );
            FileEntryServiceMock
                .Setup( service => service.GetChildrenRecursiveAsync( It.IsAny<string>() ) )
                .ReturnsAsync( Result.Ok<IEnumerable<FileEntry>>( [] ) );
            FileEntryServiceMock
                .Setup( service => service.Watch( It.IsAny<string>() ) )
                .Callback<string>( path => WatchedPaths.Add( path ) );
            FileEntryServiceMock
                .SetupAdd( service => service.EntriesChanged += It.IsAny<Func<IReadOnlyList<FileEntry>, Task>>() )
                .Callback<Func<IReadOnlyList<FileEntry>, Task>>( handler => {
                    EntriesChangedSubscribeCount++;
                    _entriesChangedHandlers.Add( handler );
                } );
            FileEntryServiceMock
                .SetupRemove( service => service.EntriesChanged -= It.IsAny<Func<IReadOnlyList<FileEntry>, Task>>() )
                .Callback<Func<IReadOnlyList<FileEntry>, Task>>( handler => {
                    EntriesChangedUnsubscribeCount++;
                    _entriesChangedHandlers.Remove( handler );
                } );

            SnackbarServiceMock
                .SetupGet( service => service.MessageQueue )
                .Returns( SnackbarMessageQueueMock.Object );
            SnackbarServiceMock
                .Setup( service => service.Show(
                    It.IsAny<string>(),
                    It.IsAny<string?>(),
                    It.IsAny<Action?>(),
                    It.IsAny<object?>(),
                    It.IsAny<TimeSpan?>() ) )
                .Callback<string, string?, Action?, object?, TimeSpan?>(
                    ( message, action, handler, _, _ ) => {
                        SnackbarMessages.Add( message );
                        SnackbarNotifications.Add( (message, action, handler) );
                    } );
            SnackbarServiceMock
                .Setup( service => service.Clear() );
        }

        public UploadViewModel CreateViewModel() {
            EnsureInitialized();
            return new(
                ApiServiceMock.Object,
                AppSettingsServiceMock.Object,
                DispatcherServiceMock.Object,
                FileContentInspectorMock.Object,
                FileEntryServiceMock.Object,
                LoggingServiceMock.Object,
                SnackbarServiceMock.Object,
                SystemServiceMock.Object,
                WindowManagerMock.Object );
        }

        public async Task RaiseEntriesChangedAsync( IReadOnlyList<FileEntry> entries ) {
            EnsureInitialized();
            if(_entriesChangedHandlers.Count == 0) {
                throw new InvalidOperationException( "EntriesChanged の購読が設定されていない。" );
            }

            foreach(var handler in _entriesChangedHandlers.ToArray()) {
                await handler( entries );
            }
        }
    }
}