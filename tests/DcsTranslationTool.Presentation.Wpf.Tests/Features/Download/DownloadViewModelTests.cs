using System.IO.Compression;
using System.Text;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.Download;

/// <summary>DownloadViewModel の動作を検証するテストを提供する。</summary>
public sealed class DownloadViewModelTests {
    /// <summary>manifest が欠落している場合に検証エラーメッセージを提示することを確認する。</summary>
    [Fact]
    public async Task Downloadを呼び出すとmanifestが欠落している場合は検証エラーを通知する() {
        using var tempDirectory = new TempDirectory();
        var appSettings = new AppSettings { TranslateFileDir = tempDirectory.Path };
        const string repoEntryPath = "DCSWorld/Mods/aircraft/A10C/L10N/Example.lua";

        var repoEntry = new RepoFileEntry( "Example.lua", repoEntryPath, false, repoSha: "deadbeef" );
        var treeResult = Result.Ok<IReadOnlyList<FileEntry>>( [repoEntry] );

        byte[] archiveBytes = CreateZipWithoutManifest( repoEntryPath, "dummy-content" );
        var downloadResult = Result.Ok(
            new ApiDownloadFilesResult(
                [repoEntryPath],
                archiveBytes,
                archiveBytes.Length,
                "application/zip",
                "test.zip",
                null,
                false
            )
        );

        var apiService = new StubApiService( treeResult, downloadResult );
        var appSettingsService = new StubAppSettingsService( appSettings );
        var dispatcherService = new ImmediateDispatcherService();
        var fileEntryService = new StubFileEntryService();
        var logger = new RecordingLoggingService();
        var snackbarService = new RecordingSnackbarService();
        var systemService = new StubSystemService();
        var zipService = new StubZipService();

        var viewModel = new DownloadViewModel(
            apiService,
            appSettingsService,
            dispatcherService,
            fileEntryService,
            logger,
            snackbarService,
            systemService,
            zipService
        );

        await viewModel.Fetch();
        var aircraftIndex = viewModel.Tabs
            .Select( ( tab, index ) => ( tab, index ) )
            .First( pair => pair.tab.TabType == CategoryType.Aircraft )
            .index;
        viewModel.SelectedTabIndex = aircraftIndex;

        var aircraftRoot = viewModel.Tabs[aircraftIndex].Root;
        var fileNode = FindNodeByPath( aircraftRoot, "A10C", "L10N", "Example.lua" );
        Assert.NotNull( fileNode );
        fileNode!.CheckState = true;

        Assert.True( viewModel.CanDownload );

        await viewModel.Download();

        Assert.Contains( "マニフェストの検証に失敗しました", snackbarService.Messages );
        var expectedFilePath = Path.Combine( tempDirectory.Path, "DCSWorld", "Mods", "aircraft", "A10C", "L10N", "Example.lua" );
        Assert.False( File.Exists( expectedFilePath ) );
    }

    private static IFileEntryViewModel? FindNodeByPath( IFileEntryViewModel root, params string[] segments ) {
        IFileEntryViewModel? current = root;
        foreach(var segment in segments) {
            current = current?.Children.FirstOrDefault( child => string.Equals( child?.Name, segment, StringComparison.Ordinal ) );
            if(current is null) return null;
        }
        return current;
    }

    private static byte[] CreateZipWithoutManifest( string entryPath, string content ) {
        using var stream = new MemoryStream();
        using(var archive = new ZipArchive( stream, ZipArchiveMode.Create, leaveOpen: true )) {
            var entry = archive.CreateEntry( entryPath );
            using var writer = new StreamWriter( entry.Open(), Encoding.UTF8 );
            writer.Write( content );
        }
        return stream.ToArray();
    }

    private sealed class StubApiService(
        Result<IReadOnlyList<FileEntry>> treeResult,
        Result<ApiDownloadFilesResult> downloadResult
    ) : IApiService {
        private readonly Result<IReadOnlyList<FileEntry>> _treeResult = treeResult;
        private readonly Result<ApiDownloadFilesResult> _downloadResult = downloadResult;

        public Task<Result<ApiHealth>> GetHealthAsync( CancellationToken cancellationToken = default ) =>
            throw new NotSupportedException( "GetHealthAsync はテストで使用しない。" );

        public Task<Result<IReadOnlyList<FileEntry>>> GetTreeAsync( CancellationToken cancellationToken = default ) =>
            Task.FromResult( _treeResult );

        public Task<Result<ApiDownloadFilesResult>> DownloadFilesAsync( ApiDownloadFilesRequest request, CancellationToken cancellationToken = default ) =>
            Task.FromResult( _downloadResult );

        public Task<Result<ApiCreatePullRequestOutcome>> CreatePullRequestAsync( ApiCreatePullRequestRequest request, CancellationToken cancellationToken = default ) =>
            throw new NotSupportedException( "CreatePullRequestAsync はテストで使用しない。" );
    }

    private sealed class StubAppSettingsService( AppSettings settings ) : IAppSettingsService {
        public AppSettings Settings { get; } = settings;

        public Task SaveAsync( CancellationToken cancellationToken = default ) => Task.CompletedTask;

        public void Dispose() {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class ImmediateDispatcherService() : IDispatcherService {
        public Task InvokeAsync( Func<Task> func ) {
            ArgumentNullException.ThrowIfNull( func );
            return func();
        }
    }

    private sealed class StubFileEntryService : IFileEntryService {
        public event Func<IReadOnlyList<FileEntry>, Task>? EntriesChanged;

        public Task<Result<IEnumerable<FileEntry>>> GetChildrenRecursiveAsync( string path ) =>
            Task.FromResult( Result.Ok<IEnumerable<FileEntry>>( [] ) );

        public Task<Result<IReadOnlyList<FileEntry>>> GetEntriesAsync() =>
            Task.FromResult( Result.Ok<IReadOnlyList<FileEntry>>( [] ) );

        public void Watch( string path ) {
        }

        public void Dispose() {
            EntriesChanged = null;
        }
    }

    private sealed class RecordingLoggingService : ILoggingService {
        public void Trace( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
        public void Debug( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
        public void Info( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
        public void Warn( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
        public void Error( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
        public void Fatal( string message, Exception? ex = null, string? member = null, string? file = null, int line = 0 ) { }
    }

    private sealed class RecordingSnackbarService : ISnackbarService {
        private readonly SnackbarService _inner = new();
        public List<string> Messages { get; } = [];

        public ISnackbarMessageQueue MessageQueue => _inner.MessageQueue;

        public void Show(
            string message,
            string? actionContent = null,
            Action? actionHandler = null,
            object? actionArgument = null,
            TimeSpan? duration = null ) {
            Messages.Add( message );
            _inner.Show( message, actionContent, actionHandler, actionArgument, duration );
        }

        public void Clear() {
            _inner.Clear();
            Messages.Clear();
        }
    }

    private sealed class StubSystemService() : ISystemService {
        public void OpenInWebBrowser( string url ) =>
            throw new NotSupportedException( "OpenInWebBrowser はテストで使用しない。" );

        public void OpenDirectory( string path ) {
        }
    }

    private sealed class StubZipService() : IZipService {
        public Result<IReadOnlyList<string>> GetEntries( string zipFilePath ) =>
            Result.Fail( "GetEntries はテストで使用しない。" );

        public Result AddEntry( string zipFilePath, string entryPath, string filePath ) =>
            Result.Fail( "AddEntry はテストで使用しない。" );

        public Result AddEntry( string zipFilePath, string entryPath, byte[] data ) =>
            Result.Fail( "AddEntry はテストで使用しない。" );

        public Result DeleteEntry( string zipFilePath, string targetPath ) =>
            Result.Fail( "DeleteEntry はテストで使用しない。" );
    }

    private sealed class TempDirectory : IDisposable {
        public TempDirectory() {
            Path = System.IO.Path.Combine( System.IO.Path.GetTempPath(), $"DownloadViewModelTests_{Guid.NewGuid():N}" );
            Directory.CreateDirectory( Path );
        }

        public string Path { get; }

        public void Dispose() {
            try {
                if(Directory.Exists( Path )) Directory.Delete( Path, recursive: true );
            }
            catch {
                // テスト終了後のクリーンアップ失敗は無視する
            }
        }
    }
}
