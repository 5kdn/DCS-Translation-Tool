using System.Windows;

using Caliburn.Micro;

using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Shell;

namespace DcsTranslationTool.Presentation.Wpf;

public class Bootstrapper : BootstrapperBase {
    private SimpleContainer? container;

    public Bootstrapper() {
        Initialize();
    }

    protected override void Configure() {
        container = new SimpleContainer();

        // Caliburn 基盤
        container.Singleton<IWindowManager, WindowManager>();
        container.Singleton<IEventAggregator, EventAggregator>();
        container.Instance( container );

        // Presentation層で実装していないサービス群

        // ViewModels
        container.Singleton<ShellViewModel>();
        container.PerRequest<MainViewModel>();
    }

    protected override async void OnStartup( object sender, StartupEventArgs e ) {
        var shellView = new ShellView();
        var frameAdapter = new FrameAdapter( shellView.RootFrame, true );
        container!.Instance<INavigationService>( frameAdapter );
        var shellVm = container.GetInstance<ShellViewModel>();
        ViewModelBinder.Bind( shellVm, shellView, null );
        await ScreenExtensions.TryActivateAsync( shellVm );
        shellView.Show();
        // （Caliburn の表示ヘルパは使わず、Attach 済み状態で起動する）
        await Task.CompletedTask;
    }
    protected override object? GetInstance( Type service, string key ) => container?.GetInstance( service, key );

    protected override IEnumerable<object>? GetAllInstances( Type service ) => container?.GetAllInstances( service );

    protected override void BuildUp( object instance ) => container?.BuildUp( instance );

    /// <summary>
    /// アプリ終了時処理。
    /// </summary>
    protected override void OnExit( object sender, EventArgs e ) {
        base.OnExit( sender, e );
    }
}