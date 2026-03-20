using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView.xaml の相互作用ロジック。
/// </summary>
public partial class TranslationCreationView : Window {
    private static readonly DependencyPropertyDescriptor DictionaryDetailsWrapCheckBoxDescriptor =
        DependencyPropertyDescriptor.FromProperty( ToggleButton.IsCheckedProperty, typeof( CheckBox ) );
    private ITranslationCreationViewModel? _currentViewModel;
    private readonly TranslationCreationWindowCoordinator _windowCoordinator;
    private bool _isDictionaryDetailsWrapCheckBoxObserved;
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
    /// Ctrl+上下キーで dictionary 選択移動を行う。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_PreviewKeyDown( object sender, KeyEventArgs e ) {
        if(e.Key is not (Key.Up or Key.Down) || Keyboard.Modifiers != ModifierKeys.Control) {
            return;
        }

        if(ViewModel is not { } viewModel) {
            return;
        }

        var selectionChanged = e.Key == Key.Up
            ? viewModel.MoveSelectionUp()
            : viewModel.MoveSelectionDown();

        if(selectionChanged && viewModel.SelectedDictionaryItem is not null) {
            DictionaryDataGrid.UpdateLayout();
            DictionaryDataGrid.ScrollIntoView( viewModel.SelectedDictionaryItem );
        }

        e.Handled = true;
    }

    /// <summary>
    /// DataContext 変更時に ViewModel 監視とレイアウト反映を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) {
        DetachViewModelEvents( e.OldValue as ITranslationCreationViewModel );
        AttachViewModelEvents( e.NewValue as ITranslationCreationViewModel );
        _windowCoordinator.ApplyLayout( e.NewValue as ITranslationCreationViewModel );
    }

    /// <summary>
    /// Loaded 時に保存済みレイアウトを反映し、折り返しチェックボックス監視を開始する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_Loaded( object sender, RoutedEventArgs e ) {
        _windowCoordinator.ApplyLayout( ViewModel );

        if(_isDictionaryDetailsWrapCheckBoxObserved) {
            return;
        }

        DictionaryDetailsWrapCheckBoxDescriptor.AddValueChanged( DictionaryDetailsWrapCheckBox, DictionaryDetailsWrapCheckBox_ValueChanged );
        _isDictionaryDetailsWrapCheckBoxObserved = true;
    }

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
        if(_isDictionaryDetailsWrapCheckBoxObserved) {
            DictionaryDetailsWrapCheckBoxDescriptor.RemoveValueChanged( DictionaryDetailsWrapCheckBox, DictionaryDetailsWrapCheckBox_ValueChanged );
            _isDictionaryDetailsWrapCheckBoxObserved = false;
        }

        DetachViewModelEvents( _currentViewModel );
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
    /// 折り返しチェックボックス変更を ViewModel へ同期する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void DictionaryDetailsWrapCheckBox_ValueChanged( object? sender, EventArgs e ) {
        if(ViewModel is not { } viewModel) {
            return;
        }

        viewModel.SetDictionaryDetailsWrapEnabled( DictionaryDetailsWrapCheckBox.IsChecked != false );
    }

    /// <summary>
    /// ViewModel の変更監視を開始する。
    /// </summary>
    /// <param name="viewModel">監視対象の ViewModel。</param>
    private void AttachViewModelEvents( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null || ReferenceEquals( _currentViewModel, viewModel )) {
            _currentViewModel = viewModel;
            return;
        }

        _currentViewModel = viewModel;
        _currentViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    /// <summary>
    /// ViewModel の変更監視を解除する。
    /// </summary>
    /// <param name="viewModel">解除対象の ViewModel。</param>
    private void DetachViewModelEvents( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null) {
            return;
        }

        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        if(ReferenceEquals( _currentViewModel, viewModel )) {
            _currentViewModel = null;
        }
    }

    /// <summary>
    /// ViewModel の折り返し設定変更を画面へ反映する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( ITranslationCreationViewModel.IsDictionaryDetailsWrapEnabled )) {
            _windowCoordinator.ApplyDictionaryDetailsTextWrapping( sender as ITranslationCreationViewModel );
        }
    }

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