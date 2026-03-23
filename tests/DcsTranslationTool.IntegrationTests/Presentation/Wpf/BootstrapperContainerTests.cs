using System.Windows.Threading;

using Caliburn.Micro;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Composition;
using DcsTranslationTool.Presentation.Wpf.Features.Download;
using DcsTranslationTool.Presentation.Wpf.Features.Main;
using DcsTranslationTool.Presentation.Wpf.Features.Settings;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationFileSelection;
using DcsTranslationTool.Presentation.Wpf.Features.Upload;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.Shell;

using Moq;

using NLog.Config;

namespace DcsTranslationTool.IntegrationTests.Presentation.Wpf;

/// <summary>
/// Presentation コンテナ登録の主要解決を検証する。
/// </summary>
public sealed class BootstrapperContainerTests {
    /// <summary>
    /// 主要サービスと ViewModel を STA 上で解決できることを検証する。
    /// </summary>
    [StaFact]
    public async Task Configure相当の登録で主要サービスとViewModelを解決できる() {
        await RunOnStaThreadAsync( () => {
            using var context = new ContainerTestContext();

            Assert.IsType<DialogService>( context.Get<IDialogService>() );
            Assert.IsType<EntryApplyService>( context.Get<IEntryApplyService>() );
            Assert.IsType<ApplyWorkflowService>( context.Get<IApplyWorkflowService>() );
            Assert.IsType<DownloadWorkflowService>( context.Get<IDownloadWorkflowService>() );
            Assert.IsType<TranslationFileSelectionWorkflowService>( context.Get<ITranslationFileSelectionWorkflowService>() );

            Assert.IsType<ShellViewModel>( context.Get<ShellViewModel>() );
            Assert.IsType<MainViewModel>( context.Get<MainViewModel>() );
            Assert.IsType<SettingsViewModel>( context.Get<SettingsViewModel>() );
            Assert.IsType<DownloadViewModel>( context.Get<DownloadViewModel>() );
            Assert.IsType<UploadViewModel>( context.Get<UploadViewModel>() );
            Assert.IsType<TranslationFileSelectionViewModel>( context.Get<TranslationFileSelectionViewModel>() );
        } );
    }

    /// <summary>
    /// STA スレッドで処理を実行する。
    /// </summary>
    /// <param name="action">実行処理。</param>
    /// <returns>非同期タスクを返す。</returns>
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

    /// <summary>
    /// コンテナテスト用コンテキストを表す。
    /// </summary>
    private sealed class ContainerTestContext : IDisposable {
        private readonly LoggingConfiguration? _originalConfiguration;

        /// <summary>
        /// コンテナ構成を初期化する。
        /// </summary>
        public ContainerTestContext() {
            Container = new SimpleContainer();
            _originalConfiguration = NLog.LogManager.Configuration;

            Container.Singleton<IWindowManager, WindowManager>();
            Container.Singleton<IEventAggregator, EventAggregator>();
            Container.Instance( Container );
            Container.Instance( Dispatcher.CurrentDispatcher );

            CompositionRegistration.Register( Container );
            Container.Singleton<IApplicationInfoService, ApplicationInfoService>();
            Container.Singleton<IDialogService, DialogService>();
            Container.Singleton<IDialogProvider, DialogProvider>();
            Container.Singleton<IDispatcherService, DispatcherService>();
            Container.Singleton<IEntryApplyService, EntryApplyService>();
            Container.Singleton<IRepoOnlySyncService, RepoOnlySyncService>();
            Container.Singleton<IApplyWorkflowService, ApplyWorkflowService>();
            Container.Singleton<IDownloadWorkflowService, DownloadWorkflowService>();
            Container.Singleton<IFileEntryWatcherLifecycle, FileEntryWatcherLifecycle>();
            Container.Singleton<IFileEntryTreeService, FileEntryTreeService>();
            Container.Singleton<ITranslationArchiveTreeService, TranslationArchiveTreeService>();
            Container.Singleton<ITranslationFileSelectionWorkflowService, TranslationFileSelectionWorkflowService>();
            Container.Singleton<ITranslationFileSelectionActionService, TranslationFileSelectionActionService>();
            Container.Singleton<ITranslationFileSelectionWorkflowUiAdapter, TranslationFileSelectionWorkflowUiAdapter>();
            Container.Singleton<IPathSafetyGuard, PathSafetyGuard>();
            Container.Singleton<ISnackbarService, SnackbarService>();

            var navigationServiceMock = new Mock<INavigationService>();
            Container.Instance( navigationServiceMock.Object );

            Container.Singleton<ShellViewModel>();
            Container.PerRequest<MainViewModel>();
            Container.PerRequest<SettingsViewModel>();
            Container.PerRequest<DownloadViewModel>();
            Container.PerRequest<UploadViewModel>();
            Container.PerRequest<TranslationFileSelectionViewModel>();
        }

        /// <summary>
        /// DI コンテナを返す。
        /// </summary>
        public SimpleContainer Container { get; }

        /// <summary>
        /// 指定型を解決する。
        /// </summary>
        /// <typeparam name="T">解決型。</typeparam>
        /// <returns>解決したインスタンスを返す。</returns>
        public T Get<T>() where T : class {
            var instance = Container.GetInstance( typeof( T ), null );
            Assert.NotNull( instance );
            return (T)instance;
        }

        /// <summary>
        /// 使用したリソースを破棄する。
        /// </summary>
        public void Dispose() {
            (Container.GetInstance( typeof( IAppSettingsService ), null ) as IDisposable)?.Dispose();
            NLog.LogManager.Configuration = _originalConfiguration;
        }
    }
}