using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.UI.Enums;
using DcsTranslationTool.Presentation.Wpf.ViewModels;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Services;

public sealed class EntryApplyServiceTests : IDisposable {
    private readonly string _tempDir;

    public EntryApplyServiceTests() {
        _tempDir = Path.Combine( Path.GetTempPath(), $"EntryApplyServiceTests_{Guid.NewGuid():N}" );
        Directory.CreateDirectory( _tempDir );
    }

    [Fact]
    public async Task ApplyEntriesAsyncはキャンセル時にOperationCanceledExceptionを送出する() {
        var logger = new Mock<ILoggingService>();
        var zipService = new Mock<IZipService>( MockBehavior.Strict );
        var sut = new EntryApplyService( logger.Object, new PathSafetyGuard(), zipService.Object );

        var entry = new FileEntryViewModel(
            new LocalFileEntry( "a.lua", "UserMissions/M/a.lua", false, "local" ),
            ChangeTypeMode.Download,
            logger.Object
        );

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>( () => sut.ApplyEntriesAsync(
            [entry],
            _tempDir,
            EnsureSeparator( _tempDir ),
            _tempDir,
            EnsureSeparator( _tempDir ),
            _ => Task.CompletedTask,
            _ => Task.CompletedTask,
            cts.Token
        ) );
    }

    [Fact]
    public async Task ApplyEntriesAsyncは翻訳ファイル未存在時に失敗件数を通知する() {
        var logger = new Mock<ILoggingService>();
        var zipService = new Mock<IZipService>( MockBehavior.Strict );
        var sut = new EntryApplyService( logger.Object, new PathSafetyGuard(), zipService.Object );

        var entry = new FileEntryViewModel(
            new LocalFileEntry( "a.lua", "UserMissions/M/a.lua", false, "local" ),
            ChangeTypeMode.Download,
            logger.Object
        );

        var messages = new List<string>();
        var result = await sut.ApplyEntriesAsync(
            [entry],
            _tempDir,
            EnsureSeparator( _tempDir ),
            _tempDir,
            EnsureSeparator( _tempDir ),
            message => {
                messages.Add( message );
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        Assert.True( result );
        Assert.Contains( messages, message => message.Contains( "翻訳ファイルが見つかりません", StringComparison.Ordinal ) );
        Assert.Contains( "適用完了 成功:0 件 失敗:1 件", messages );
    }

    [Fact]
    public async Task ApplyEntriesAsyncはzip適用失敗時に失敗件数を通知する() {
        var logger = new Mock<ILoggingService>();
        var zipService = new Mock<IZipService>( MockBehavior.Strict );
        var sut = new EntryApplyService( logger.Object, new PathSafetyGuard(), zipService.Object );

        var translateRoot = Path.Combine( _tempDir, "translate" );
        var targetRoot = Path.Combine( _tempDir, "target" );
        Directory.CreateDirectory( translateRoot );
        Directory.CreateDirectory( targetRoot );

        const string path = "UserMissions/My/Sample.miz/Localization/test.lua";
        var sourcePath = Path.Combine( translateRoot, "UserMissions", "My", "Sample.miz", "Localization", "test.lua" );
        Directory.CreateDirectory( Path.GetDirectoryName( sourcePath )! );
        File.WriteAllText( sourcePath, "content" );

        var archivePath = Path.Combine( targetRoot, "My", "Sample.miz" );
        Directory.CreateDirectory( Path.GetDirectoryName( archivePath )! );
        File.WriteAllBytes( archivePath, [] );

        zipService
            .Setup( service => service.AddEntry(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>()
            ) )
            .Returns( Result.Fail( "zip failed" ) );

        var entry = new FileEntryViewModel(
            new LocalFileEntry( "test.lua", path, false, "local" ),
            ChangeTypeMode.Download,
            logger.Object
        );

        var messages = new List<string>();
        var result = await sut.ApplyEntriesAsync(
            [entry],
            targetRoot,
            EnsureSeparator( targetRoot ),
            translateRoot,
            EnsureSeparator( translateRoot ),
            message => {
                messages.Add( message );
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            TestContext.Current.CancellationToken
        );

        Assert.True( result );
        Assert.Contains( "適用失敗: UserMissions/My/Sample.miz/Localization/test.lua", messages );
        Assert.Contains( "適用完了 成功:0 件 失敗:1 件", messages );
    }

    public void Dispose() {
        if(Directory.Exists( _tempDir )) {
            Directory.Delete( _tempDir, true );
        }
    }

    private static string EnsureSeparator( string path ) =>
        path.EndsWith( Path.DirectorySeparatorChar ) ? path : path + Path.DirectorySeparatorChar;
}