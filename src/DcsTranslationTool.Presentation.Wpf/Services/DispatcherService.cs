using System.Windows.Threading;

using DcsTranslationTool.Application.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// WPF の Dispatcher を用いてUIスレッドで処理を実行するサービス。
/// </summary>
public sealed class DispatcherService( Dispatcher dispatcher ) : IDispatcherService {
    private readonly Dispatcher _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? throw new InvalidOperationException( "Dispatcher が初期化されていない。" );

    /// <inheritdoc />
    public Task InvokeAsync( Func<Task> func ) {
        ArgumentNullException.ThrowIfNull( func );
        if(_dispatcher.CheckAccess())
            return func();

        var tcs = new TaskCompletionSource<object?>();
        _dispatcher.BeginInvoke( async () => {
            try {
                await func().ConfigureAwait( false );
                tcs.SetResult( null );
            }
            catch(Exception ex) {
                tcs.SetException( ex );
            }
        } );
        return tcs.Task;
    }
}