using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView.xaml の相互作用ロジック。
/// </summary>
public partial class TranslationCreationView : Window {
    private readonly TranslationCreationWindowCoordinator _windowCoordinator;
    private bool _isCloseConfirmationInProgress;
    private bool _isCloseConfirmed;
    private bool _isCloseCleanupCompleted;

    /// <summary>
    /// TranslationCreationView の新しいインスタンスを初期化する。
    /// </summary>
    public TranslationCreationView() {
        InitializeComponent();
        _windowCoordinator = new(
            this,
            DictionaryDataGridRowDefinition,
            DictionaryDetailsRowDefinition,
            SelectedOriginalTextBox,
            SelectedTranslatedTextBox );
        DataContextChanged += Window_DataContextChanged;
        Loaded += Window_Loaded;
        ContentRendered += Window_ContentRendered;
        Closing += Window_Closing;
    }

    private ITranslationCreationViewModel? ViewModel => DataContext as ITranslationCreationViewModel;

    /// <summary>
    /// ウィンドウを最小化する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void MinimizeWindowButton_Click( object sender, RoutedEventArgs e ) =>
        WindowState = WindowState.Minimized;

    /// <summary>
    /// ウィンドウの最大化状態を切り替える。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void ToggleMaximizeWindowButton_Click( object sender, RoutedEventArgs e ) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    /// <summary>
    /// ウィンドウを閉じる。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void CloseWindowButton_Click( object sender, RoutedEventArgs e ) => Close();

    /// <summary>
    /// タイトルバー操作で移動または最大化切り替えを行う。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void TitleBar_MouseLeftButtonDown( object sender, MouseButtonEventArgs e ) {
        if(e.ClickCount == 2) {
            ToggleMaximizeWindowButton_Click( sender, new RoutedEventArgs() );
            return;
        }

        DragMove();
    }

    /// <summary>
    /// DataContext 変更時にレイアウト反映を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) =>
        _windowCoordinator.ApplyLayout( e.NewValue as ITranslationCreationViewModel );

    /// <summary>
    /// Loaded 時に保存済みレイアウトを反映し、折り返しチェックボックス監視を開始する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_Loaded( object sender, RoutedEventArgs e ) =>
        _windowCoordinator.ApplyLayout( ViewModel );

    /// <summary>
    /// 初回描画後に遅延初期化を実行する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void Window_ContentRendered( object? sender, EventArgs e ) {
        ContentRendered -= Window_ContentRendered;

        if(ViewModel is not { } viewModel) {
            return;
        }

        _windowCoordinator.ApplyMinimumWindowSize();
        await ExecuteWindowLoadedAsync( Dispatcher, () => viewModel.HandleWindowLoadedAsync() );
    }

    /// <summary>
    /// 閉じる前に未保存変更の確認と終了処理を行う。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private async void Window_Closing( object? sender, CancelEventArgs e ) {
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
            if(ViewModel is { } viewModel && !await viewModel.ConfirmCloseAsync()) {
                return;
            }

            _isCloseConfirmed = true;
            await Dispatcher.InvokeAsync( Close, DispatcherPriority.Background );
        }
        finally {
            _isCloseConfirmationInProgress = false;
        }
    }

    /// <summary>
    /// 実際にウィンドウを閉じる直前の後始末を一度だけ実行する。
    /// </summary>
    private void CompleteClosing() {
        if(_isCloseCleanupCompleted) {
            return;
        }

        _isCloseCleanupCompleted = true;
        _windowCoordinator.PersistLayout( ViewModel );
    }

    /// <summary>
    /// GridSplitter 操作完了時に pane 比率を保存する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void DictionaryPaneGridSplitter_DragCompleted( object sender, DragCompletedEventArgs e ) =>
        _windowCoordinator.PersistLayout( ViewModel );

    /// <summary>
    /// ContentRendered 後の初期化処理を UI スレッドで最後まで待機して実行する。
    /// </summary>
    /// <param name="dispatcher">実行に利用するディスパッチャー。</param>
    /// <param name="windowLoadedAction">実行対象の初期化処理。</param>
    /// <param name="priority">ディスパッチャー実行優先度。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスクを返す。</returns>
    internal static async Task ExecuteWindowLoadedAsync(
        Dispatcher dispatcher,
        Func<Task> windowLoadedAction,
        DispatcherPriority priority = DispatcherPriority.ContextIdle,
        CancellationToken cancellationToken = default ) {
        ArgumentNullException.ThrowIfNull( dispatcher );
        ArgumentNullException.ThrowIfNull( windowLoadedAction );

        await await dispatcher.InvokeAsync(
            () => {
                cancellationToken.ThrowIfCancellationRequested();
                return windowLoadedAction();
            },
            priority );
    }
}