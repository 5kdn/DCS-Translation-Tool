using System.Reflection;
using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Composition;
using DcsTranslationTool.Infrastructure.IO;
using DcsTranslationTool.Infrastructure.Providers;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.Presentation.Wpf.Features.CreatePullRequest;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;

using Moq;

using NLog.Config;

namespace DcsTranslationTool.Presentation.Wpf.Tests;

public sealed class BootstrapperContainerTests {
    [StaFact]
    public async Task Configure相当の登録で主要サービスを解決できる() {
        await RunOnStaThreadAsync( () => {
            using var context = new ContainerTestContext();
            context.AssertResolutions();
        } );
    }

    private static Task<object?> RunOnStaThreadAsync( System.Action action ) {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread( () => {
            try {
                action();
                tcs.SetResult( null );
            }
            catch(Exception ex) {
                tcs.SetException( ex );
            }
        } )
        {
            IsBackground = true
        };
        thread.SetApartmentState( ApartmentState.STA );
        thread.Start();
        return tcs.Task;
    }

    private sealed class ContainerTestContext : IDisposable {
        private readonly LoggingConfiguration? _originalConfiguration;
        private readonly string? _settingsDirectory;

        internal ContainerTestContext() {
            Container = new SimpleContainer();
            _originalConfiguration = NLog.LogManager.Configuration;

            Container.Singleton<IWindowManager, WindowManager>();
            Container.Singleton<IEventAggregator, EventAggregator>();
            Container.Instance( Container );
            Container.Instance( Dispatcher.CurrentDispatcher );

            CompositionRegistration.Register( Container );
            Container.Singleton<IApplicationInfoService, ApplicationInfoService>();

            AppSettingsService = (AppSettingsService)Container.GetInstance( typeof( IAppSettingsService ), null )!;

            var filePathField = typeof( AppSettingsService ).GetField( "_filePath", BindingFlags.NonPublic | BindingFlags.Instance );
            if(filePathField?.GetValue( AppSettingsService ) is string filePath) {
                var directory = Path.GetDirectoryName( filePath );
                if(directory is { Length: > 0 } dir && !Directory.Exists( dir )) {
                    _settingsDirectory = dir;
                }
            }

            Container.Singleton<IDialogProvider, DialogProvider>();
            Container.Singleton<IDispatcherService, DispatcherService>();
            Container.Singleton<IEntryApplyService, EntryApplyService>();
            Container.Singleton<IRepoOnlySyncService, RepoOnlySyncService>();
            Container.Singleton<IApplyWorkflowService, ApplyWorkflowService>();
            Container.Singleton<IDownloadWorkflowService, DownloadWorkflowService>();
            Container.Singleton<IFileEntryWatcherLifecycle, FileEntryWatcherLifecycle>();
            Container.Singleton<IFileEntryTreeService, FileEntryTreeService>();
            Container.Singleton<IPathSafetyGuard, PathSafetyGuard>();
            Container.Singleton<ISnackbarService, SnackbarService>();

            var navigationServiceMock = new Mock<INavigationService>();
            Container.Instance( navigationServiceMock.Object );

            Container.Singleton<ShellViewModel>();
            Container.PerRequest<MainViewModel>();
            Container.PerRequest<SettingsViewModel>();
            Container.PerRequest<DownloadViewModel>();
            Container.PerRequest<UploadViewModel>();
            Container.PerRequest<CreatePullRequestViewModel>();
        }

        internal SimpleContainer Container { get; }
        internal AppSettingsService AppSettingsService { get; }

        public void AssertResolutions() {
            Assert.IsType<LoggingService>( Get<ILoggingService>() );
            Assert.IsType<AppSettingsService>( Get<IAppSettingsService>() );
            Assert.IsType<ApiService>( Get<IApiService>() );
            Assert.IsType<FileEntryService>( Get<IFileEntryService>() );
            Assert.IsType<FileService>( Get<IFileService>() );
            Assert.IsType<FileContentInspector>( Get<IFileContentInspector>() );
            Assert.IsType<ApplicationInfoService>( Get<IApplicationInfoService>() );
            Assert.IsType<EnvironmentProvider>( Get<IEnvironmentProvider>() );
            Assert.IsType<ProcessLauncher>( Get<IProcessLauncher>() );
            Assert.IsType<SystemService>( Get<ISystemService>() );
            Assert.IsType<UpdateCheckService>( Get<IUpdateCheckService>() );
            Assert.IsType<ZipService>( Get<IZipService>() );
            Assert.IsType<DialogProvider>( Get<IDialogProvider>() );
            Assert.IsType<DispatcherService>( Get<IDispatcherService>() );
            Assert.IsType<EntryApplyService>( Get<IEntryApplyService>() );
            Assert.IsType<RepoOnlySyncService>( Get<IRepoOnlySyncService>() );
            Assert.IsType<ApplyWorkflowService>( Get<IApplyWorkflowService>() );
            Assert.IsType<DownloadWorkflowService>( Get<IDownloadWorkflowService>() );
            Assert.IsType<FileEntryWatcherLifecycle>( Get<IFileEntryWatcherLifecycle>() );
            Assert.IsType<FileEntryTreeService>( Get<IFileEntryTreeService>() );
            Assert.IsType<PathSafetyGuard>( Get<IPathSafetyGuard>() );
            Assert.IsType<SnackbarService>( Get<ISnackbarService>() );
            Assert.IsType<WindowManager>( Get<IWindowManager>() );
            Assert.IsType<EventAggregator>( Get<IEventAggregator>() );

            Assert.IsType<ShellViewModel>( Get<ShellViewModel>() );
            Assert.IsType<MainViewModel>( Get<MainViewModel>() );
            Assert.IsType<SettingsViewModel>( Get<SettingsViewModel>() );
            Assert.IsType<DownloadViewModel>( Get<DownloadViewModel>() );
            Assert.IsType<UploadViewModel>( Get<UploadViewModel>() );
        }

        private T Get<T>() where T : class {
            var instance = Container.GetInstance( typeof( T ), null );
            Assert.NotNull( instance );
            return (T)instance!;
        }

        public void Dispose() {
            AppSettingsService.Dispose();
            NLog.LogManager.Configuration = _originalConfiguration;

            if(_settingsDirectory is { Length: > 0 } dir && Directory.Exists( dir )) {
                try {
                    Directory.Delete( dir, true );
                }
                catch {
                    // テスト環境で削除できなくても無視する
                }
            }
        }
    }
}