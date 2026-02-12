using System.Windows;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Composition;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;

namespace DcsTranslationTool.Presentation.Wpf;

public class Bootstrapper : BootstrapperBase {
    private SimpleContainer? container;
    private ILoggingService? loggingService;
    private IAppSettingsService? appSettingsService;

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
        loggingService = container.GetInstance<ILoggingService>();
        container.Singleton<IApplicationInfoService, ApplicationInfoService>();
        appSettingsService = container.GetInstance<IAppSettingsService>();

        container.Singleton<IDialogService, DialogService>();
        container.Singleton<IDialogProvider, DialogProvider>();
        container.Singleton<IDispatcherService, DispatcherService>();
        container.Singleton<IEntryApplyService, EntryApplyService>();
        container.Singleton<IRepoOnlySyncService, RepoOnlySyncService>();
        container.Singleton<IApplyWorkflowService, ApplyWorkflowService>();
        container.Singleton<IDownloadWorkflowService, DownloadWorkflowService>();
        container.Singleton<IFileEntryWatcherLifecycle, FileEntryWatcherLifecycle>();
        container.Singleton<IFileEntryTreeService, FileEntryTreeService>();
        container.Singleton<IPathSafetyGuard, PathSafetyGuard>();
        container.Singleton<ISnackbarService, SnackbarService>();

        // ViewModels
        container.Singleton<ShellViewModel>();
        container.PerRequest<MainViewModel>();
        container.PerRequest<SettingsViewModel>();
        container.PerRequest<DownloadViewModel>();
        container.PerRequest<UploadViewModel>();
        container.PerRequest<CreatePullRequestViewModel>();
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
        if(appSettingsService is IAsyncDisposable asyncDisposable) {
            try {
                loggingService?.Debug( "AppSettingsService を非同期破棄する。" );
                asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
                loggingService?.Debug( "AppSettingsService の非同期破棄が完了した。" );
            }
            catch(Exception ex) {
                loggingService?.Error( "AppSettingsService の破棄に失敗した。", ex );
            }
        }
        else if(appSettingsService is IDisposable disposable) {
            try {
                loggingService?.Debug( "AppSettingsService を同期破棄する。" );
                disposable.Dispose();
                loggingService?.Debug( "AppSettingsService の同期破棄が完了した。" );
            }
            catch(Exception ex) {
                loggingService?.Error( "AppSettingsService の同期破棄に失敗した。", ex );
            }
        }
        NLog.LogManager.Shutdown();
    }
}