using System.Windows.Threading;

using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.Tests.Features.TranslationCreation;

/// <summary>
/// <see cref="TranslationCreationView"/> の軽量ロジックを検証する。
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
            var executeTask = TranslationCreationView.ExecuteWindowLoadedAsync(
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
}