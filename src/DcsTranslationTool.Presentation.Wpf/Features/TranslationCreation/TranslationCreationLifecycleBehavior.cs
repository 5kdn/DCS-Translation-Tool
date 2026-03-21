using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

using Microsoft.Xaml.Behaviors;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView の初期化とクローズ確認を仲介する。
/// </summary>
public sealed class TranslationCreationLifecycleBehavior : Behavior<Window> {
    private bool _isCloseConfirmationInProgress;
    private bool _isCloseConfirmed;
    private bool _isCloseCleanupCompleted;
    private bool _bypassCloseConfirmation;
    private ITranslationCreationViewModel? _subscribedViewModel;

    /// <summary>
    /// 関連ウィンドウへイベント購読を接続する。
    /// </summary>
    protected override void OnAttached() {
        base.OnAttached();
        AssociatedObject.ContentRendered += Window_ContentRendered;
        AssociatedObject.Closing += Window_Closing;
        AssociatedObject.Closed += Window_Closed;
        AssociatedObject.DataContextChanged += Window_DataContextChanged;
        SubscribeViewModel( GetViewModel() );
    }

    /// <summary>
    /// 関連ウィンドウからイベント購読を解除する。
    /// </summary>
    protected override void OnDetaching() {
        if(AssociatedObject is not null) {
            AssociatedObject.ContentRendered -= Window_ContentRendered;
            AssociatedObject.Closing -= Window_Closing;
            AssociatedObject.Closed -= Window_Closed;
            AssociatedObject.DataContextChanged -= Window_DataContextChanged;
        }

        UnsubscribeViewModel( _subscribedViewModel );
        base.OnDetaching();
    }

    /// <summary>
    /// 初回描画後に遅延初期化を実行する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void Window_ContentRendered( object? sender, EventArgs e ) {
        if(AssociatedObject is null) {
            return;
        }

        AssociatedObject.ContentRendered -= Window_ContentRendered;

        if(GetViewModel() is not { } viewModel) {
            return;
        }

        await TranslationCreationWindowLifecycleHelper.ExecuteWindowLoadedAsync(
            AssociatedObject.Dispatcher,
            () => viewModel.HandleWindowLoadedAsync() );
    }

    /// <summary>
    /// 閉じる前に未保存変更の確認を行う。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void Window_Closing( object? sender, CancelEventArgs e ) {
        if(AssociatedObject is null) {
            return;
        }

        if(_bypassCloseConfirmation) {
            _isCloseConfirmed = true;
            CompleteClosing();
            return;
        }

        if(_isCloseConfirmed) {
            CompleteClosing();
            return;
        }

        e.Cancel = true;
        if(_isCloseConfirmationInProgress) {
            return;
        }

        _isCloseConfirmationInProgress = true;
        try {
            if(GetViewModel() is { } viewModel && !await viewModel.ConfirmCloseAsync()) {
                return;
            }

            _isCloseConfirmed = true;
            await AssociatedObject.Dispatcher.InvokeAsync( AssociatedObject.Close, DispatcherPriority.Background );
        }
        finally {
            _isCloseConfirmationInProgress = false;
        }
    }

    /// <summary>
    /// Closed 時の後始末を一度だけ実行する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_Closed( object? sender, EventArgs e ) {
        UnsubscribeViewModel( _subscribedViewModel );
        CompleteClosing();
    }

    /// <summary>
    /// DataContext 変更時に ViewModel 購読を差し替える。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) {
        UnsubscribeViewModel( e.OldValue as ITranslationCreationViewModel );
        SubscribeViewModel( e.NewValue as ITranslationCreationViewModel );
    }

    /// <summary>
    /// ViewModel の起動時クローズ要求を監視する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName != nameof( ITranslationCreationViewModel.ShouldCloseAfterStartup )
           || AssociatedObject is null
           || GetViewModel() is not { ShouldCloseAfterStartup: true } viewModel) {
            return;
        }

        _bypassCloseConfirmation = true;
        viewModel.AcknowledgeStartupCloseRequest();
        AssociatedObject.Close();
    }

    /// <summary>
    /// 現在の DataContext から ViewModel を取得する。
    /// </summary>
    /// <returns>関連 ViewModel を返す。</returns>
    private ITranslationCreationViewModel? GetViewModel() => AssociatedObject?.DataContext as ITranslationCreationViewModel;

    /// <summary>
    /// ViewModel の変更通知購読を開始する。
    /// </summary>
    /// <param name="viewModel">購読対象の ViewModel。</param>
    private void SubscribeViewModel( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null || ReferenceEquals( _subscribedViewModel, viewModel )) {
            return;
        }

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _subscribedViewModel = viewModel;
    }

    /// <summary>
    /// ViewModel の変更通知購読を解除する。
    /// </summary>
    /// <param name="viewModel">解除対象の ViewModel。</param>
    private void UnsubscribeViewModel( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null || !ReferenceEquals( _subscribedViewModel, viewModel )) {
            return;
        }

        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _subscribedViewModel = null;
    }

    /// <summary>
    /// クローズ完了後の後始末を一度だけ実行する。
    /// </summary>
    private void CompleteClosing() {
        if(_isCloseCleanupCompleted) {
            return;
        }

        _isCloseCleanupCompleted = true;
    }
}