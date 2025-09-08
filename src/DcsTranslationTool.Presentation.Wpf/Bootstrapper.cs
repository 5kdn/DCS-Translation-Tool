using System.Windows;

using Caliburn.Micro;

using DcsTranslationTool.Composition;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Shell;

using InfraLoggingService = DcsTranslationTool.Infrastructure.Interfaces.ILoggingService;

namespace DcsTranslationTool.Presentation.Wpf;

public class Bootstrapper : BootstrapperBase {
    private SimpleContainer? container;
    private ILoggingService? loggingService;

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
        CompositionRegistration.Register( container );
        var infraLoggingService = container.GetInstance<InfraLoggingService>();
        loggingService = new LoggingServiceAdapter( infraLoggingService );
        container.Instance<ILoggingService>( loggingService );

        // ViewModels
        container.Singleton<ShellViewModel>();
        container.PerRequest<MainViewModel>();
    }

    protected override async void OnStartup( object sender, StartupEventArgs e ) {
        loggingService?.Info( "起動" );
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
        loggingService?.Info( "終了" );
        base.OnExit( sender, e );
        NLog.LogManager.Shutdown();
    }
}