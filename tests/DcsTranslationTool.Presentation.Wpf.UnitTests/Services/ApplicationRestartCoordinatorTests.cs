using System.Windows;

using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;
using DcsTranslationTool.Presentation.Wpf.Services;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

using Microsoft.Xaml.Behaviors;

using Moq;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Services;

/// <summary>
/// <see cref="ApplicationRestartCoordinator"/> の再起動前終了確認を検証する。
/// </summary>
public sealed class ApplicationRestartCoordinatorTests {
    [StaFact]
    public async Task PrepareForRestartAsyncは一つでも終了を拒否した場合に全ウィンドウを維持する() {
        var firstViewModel = CreateViewModel( true );
        var secondViewModel = CreateViewModel( false );
        var firstWindow = CreateWindow( firstViewModel.Object );
        var secondWindow = CreateWindow( secondViewModel.Object );
        var provider = new Mock<IApplicationWindowProvider>();
        provider.Setup( value => value.GetWindows() ).Returns( [firstWindow, secondWindow] );
        var coordinator = new ApplicationRestartCoordinator( provider.Object );

        var result = await coordinator.PrepareForRestartAsync( TestContext.Current.CancellationToken );

        Assert.False( result );
        Assert.False( firstWindow.WasClosed );
        Assert.False( secondWindow.WasClosed );
        firstViewModel.Verify( value => value.ConfirmCloseAsync(), Times.Once );
        secondViewModel.Verify( value => value.ConfirmCloseAsync(), Times.Once );
    }

    [StaFact]
    public async Task PrepareForRestartAsyncは全確認後に再確認せずウィンドウを閉じる() {
        var firstViewModel = CreateViewModel( true );
        var secondViewModel = CreateViewModel( true );
        var firstWindow = CreateWindow( firstViewModel.Object );
        var secondWindow = CreateWindow( secondViewModel.Object );
        var provider = new Mock<IApplicationWindowProvider>();
        provider.Setup( value => value.GetWindows() ).Returns( [firstWindow, secondWindow] );
        var coordinator = new ApplicationRestartCoordinator( provider.Object );

        var result = await coordinator.PrepareForRestartAsync( TestContext.Current.CancellationToken );

        Assert.True( result );
        Assert.True( firstWindow.WasClosed );
        Assert.True( secondWindow.WasClosed );
        firstViewModel.Verify( value => value.ConfirmCloseAsync(), Times.Once );
        secondViewModel.Verify( value => value.ConfirmCloseAsync(), Times.Once );
    }

    /// <summary>
    /// 指定した終了可否を返す ViewModel モックを生成する。
    /// </summary>
    /// <param name="canClose">終了を許可するかどうかを受け取る。</param>
    /// <returns>生成したモックを返す。</returns>
    private static Mock<ITranslationCreationViewModel> CreateViewModel( bool canClose ) {
        var viewModel = new Mock<ITranslationCreationViewModel>();
        viewModel.Setup( value => value.ConfirmCloseAsync() ).ReturnsAsync( canClose );
        return viewModel;
    }

    /// <summary>
    /// ライフサイクル Behavior を持つテスト用ウィンドウを生成する。
    /// </summary>
    /// <param name="viewModel">DataContext を受け取る。</param>
    /// <returns>生成したウィンドウを返す。</returns>
    private static TestWindow CreateWindow( ITranslationCreationViewModel viewModel ) {
        var window = new TestWindow { DataContext = viewModel };
        Interaction.GetBehaviors( window ).Add( new TranslationCreationLifecycleBehavior() );
        return window;
    }

    /// <summary>
    /// 終了状態を観測するテスト用ウィンドウを表す。
    /// </summary>
    private sealed class TestWindow : Window {
        /// <summary>
        /// ウィンドウが閉じられたかどうかを取得する。
        /// </summary>
        public bool WasClosed { get; private set; }

        /// <summary>
        /// 終了状態を記録する。
        /// </summary>
        /// <param name="e">イベント引数を受け取る。</param>
        protected override void OnClosed( EventArgs e ) {
            WasClosed = true;
            base.OnClosed( e );
        }
    }
}