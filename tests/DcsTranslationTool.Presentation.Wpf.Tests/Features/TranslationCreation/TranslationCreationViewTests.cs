using System.Windows.Threading;

using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// TranslationCreation 関連の軽量ロジックを検証する。
/// </summary>
public sealed class TranslationCreationViewTests {
    [StaFact]
    public async Task ExecuteWindowLoadedAsyncはHandleWindowLoadedAsyncの完了まで待機する() {
        var dispatcherReadySource = new TaskCompletionSource<Dispatcher>();
        var actionCompletionSource = new TaskCompletionSource();
        var invocationStartedSource = new TaskCompletionSource();
        var invocationCount = 0;
        var dispatcherThread = new Thread( () => {
            dispatcherReadySource.SetResult( Dispatcher.CurrentDispatcher );
            Dispatcher.Run();
        } );

        dispatcherThread.SetApartmentState( ApartmentState.STA );
        dispatcherThread.Start();

        var dispatcher = await dispatcherReadySource.Task.WaitAsync( TestContext.Current.CancellationToken );

        try {
            var executeTask = TranslationCreationWindowLifecycleHelper.ExecuteWindowLoadedAsync(
                dispatcher,
                async () => {
                    Interlocked.Increment( ref invocationCount );
                    invocationStartedSource.TrySetResult();
                    await actionCompletionSource.Task;
                },
                DispatcherPriority.Background,
                TestContext.Current.CancellationToken );

            await invocationStartedSource.Task.WaitAsync( TestContext.Current.CancellationToken );

            Assert.False( executeTask.IsCompleted );
            Assert.Equal( 1, Volatile.Read( ref invocationCount ) );

            actionCompletionSource.SetResult();
            await executeTask.WaitAsync( TestContext.Current.CancellationToken );
            Assert.Equal( 1, Volatile.Read( ref invocationCount ) );
        }
        finally {
            dispatcher.InvokeShutdown();
            dispatcherThread.Join();
        }
    }

    public static TheoryData<string, int[]> GetNewlineMarkerIndicesTestData => new()
    {
        { string.Empty, [] },
        { "single line", [] },
        { "line1\nline2", [5] },
        { "line1\r\nline2", [5] },
        { "\nline2\n", [0, 6] },
        { "line1\rline2", [5] }
    };

    [Theory]
    [MemberData( nameof( GetNewlineMarkerIndicesTestData ) )]
    public void 改行マーカー位置抽出はCRLFとLFを正しく扱う( string text, int[] expectedIndices ) {
        var actual = TextBoxNewlineMarkerAdorner.GetNewlineMarkerIndices( text );

        Assert.Equal( expectedIndices, actual );
    }

    public static TheoryData<string, int[]> GetWhitespaceHighlightIndicesTestData => new()
    {
        { string.Empty, [] },
        { "single space ", [12] },
        { "abcd ", [4] },
        { "abcd　", [4] },
        { "abcd\t", [4] },
        { "abcd \n", [4] },
        { "abcd　\n", [4] },
        { "abcd\t\r\n", [4] },
        { "a  b", [1, 2] },
        { "a　　b", [1, 2] },
        { "a \tb", [1, 2] },
        { "a \r\nb", [1] },
        { "a　\nb", [1] },
        { "a  \r\nb", [1, 2] },
        { "a\t \nb", [1, 2] },
        { "ab  cd", [2, 3] },
        { "ab　　cd", [2, 3] },
        { "ab\t\tcd", [2, 3] },
        { "ab 　cd", [2, 3] },
        { "ab \tcd", [2, 3] },
        { "ab　 cd", [2, 3] },
        { "ab　\tcd", [2, 3] },
        { "ab\t cd", [2, 3] },
        { "ab\t　cd", [2, 3] },
        { "  start", [0, 1] },
        { "end\t\t", [3, 4] },
        { "a \n b", [1] },
        { "trail　", [5] },
        { "trail\t", [5] },
        { "a b", [] }
    };

    [Theory]
    [MemberData( nameof( GetWhitespaceHighlightIndicesTestData ) )]
    public void 空白強調位置抽出は連続空白と改行前空白を正しく扱う( string text, int[] expectedIndices ) {
        var actual = TextBoxWhitespaceHighlightAdorner.GetHighlightIndices( text );

        Assert.Equal( expectedIndices, actual );
    }
}