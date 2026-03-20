using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView.xaml の相互作用ロジックである。
/// </summary>
public partial class TranslationCreationView : Window {
    private const double DetailPaneBaseRatio = 1;
    private static readonly DependencyPropertyDescriptor DictionaryDetailsWrapCheckBoxDescriptor =
        DependencyPropertyDescriptor.FromProperty( ToggleButton.IsCheckedProperty, typeof( CheckBox ) );
    private TranslationCreationViewModel? _currentViewModel;
    private bool _isDictionaryDetailsWrapCheckBoxObserved;

    public TranslationCreationView() {
        InitializeComponent();
        DataContextChanged += Window_DataContextChanged;
        Loaded += Window_Loaded;
        ContentRendered += Window_ContentRendered;
        Closing += Window_Closing;
    }

    private void MinimizeWindowButton_Click( object sender, RoutedEventArgs e ) =>
        WindowState = WindowState.Minimized;

    private void ToggleMaximizeWindowButton_Click( object sender, RoutedEventArgs e ) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private void CloseWindowButton_Click( object sender, RoutedEventArgs e ) => Close();

    private void TitleBar_MouseLeftButtonDown( object sender, MouseButtonEventArgs e ) {
        if(e.ClickCount == 2) {
            ToggleMaximizeWindowButton_Click( sender, new RoutedEventArgs() );
            return;
        }

        DragMove();
    }

    private void Window_PreviewKeyDown( object sender, KeyEventArgs e ) {
        if(e.Key is not (Key.Up or Key.Down) || Keyboard.Modifiers != ModifierKeys.Control) {
            return;
        }

        if(DataContext is not TranslationCreationViewModel viewModel) {
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

    private void Window_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) {
        DetachViewModelEvents( e.OldValue as TranslationCreationViewModel );
        AttachViewModelEvents( e.NewValue as TranslationCreationViewModel );
        ApplyMinimumWindowSize();
        ApplyWindowSize();
        ApplyDictionaryPaneRatio();
        ApplyDictionaryDetailsTextWrapping();
    }

    private void Window_Loaded( object sender, RoutedEventArgs e ) {
        ApplyMinimumWindowSize();
        ApplyWindowSize();
        ApplyDictionaryPaneRatio();
        ApplyDictionaryDetailsTextWrapping();

        if(_isDictionaryDetailsWrapCheckBoxObserved) {
            return;
        }

        DictionaryDetailsWrapCheckBoxDescriptor.AddValueChanged( DictionaryDetailsWrapCheckBox, DictionaryDetailsWrapCheckBox_ValueChanged );
        _isDictionaryDetailsWrapCheckBoxObserved = true;
    }

    private async void Window_ContentRendered( object? sender, EventArgs e ) {
        ContentRendered -= Window_ContentRendered;

        if(DataContext is not TranslationCreationViewModel viewModel) {
            return;
        }

        ApplyMinimumWindowSize();
        await ExecuteWindowLoadedAsync( Dispatcher, () => viewModel.HandleWindowLoadedAsync() );
    }

    private void Window_Closing( object? sender, CancelEventArgs e ) {
        if(_isDictionaryDetailsWrapCheckBoxObserved) {
            DictionaryDetailsWrapCheckBoxDescriptor.RemoveValueChanged( DictionaryDetailsWrapCheckBox, DictionaryDetailsWrapCheckBox_ValueChanged );
            _isDictionaryDetailsWrapCheckBoxObserved = false;
        }

        DetachViewModelEvents( _currentViewModel );
        PersistWindowSize();
        PersistDictionaryPaneRatio();
    }

    private void DictionaryPaneGridSplitter_DragCompleted( object sender, DragCompletedEventArgs e ) =>
        PersistDictionaryPaneRatio();

    private void DictionaryDetailsWrapCheckBox_ValueChanged( object? sender, EventArgs e ) {
        if(DataContext is not TranslationCreationViewModel viewModel) {
            return;
        }

        viewModel.SetDictionaryDetailsWrapEnabled( DictionaryDetailsWrapCheckBox.IsChecked != false );
    }

    /// <summary>
    /// 保存済みの dictionary 領域比率をレイアウトへ反映する。
    /// </summary>
    private void ApplyDictionaryPaneRatio() {
        var ratio = DataContext is TranslationCreationViewModel viewModel
            ? viewModel.DictionaryPaneRatio
            : TranslationCreationViewModel.DefaultDictionaryPaneRatio;

        DictionaryDataGridRowDefinition.Height = new GridLength( ratio, GridUnitType.Star );
        DictionaryDetailsRowDefinition.Height = new GridLength( DetailPaneBaseRatio, GridUnitType.Star );
    }

    /// <summary>
    /// 保存済みのウィンドウサイズをレイアウトへ反映する。
    /// </summary>
    private void ApplyWindowSize() {
        ApplyMinimumWindowSize();

        var width = DataContext is TranslationCreationViewModel viewModel
            ? viewModel.WindowWidth
            : TranslationCreationViewModel.DefaultWindowWidth;
        var height = DataContext is TranslationCreationViewModel sizedViewModel
            ? sizedViewModel.WindowHeight
            : TranslationCreationViewModel.DefaultWindowHeight;

        Width = Math.Max( width, MinWidth );
        Height = Math.Max( height, MinHeight );
    }

    /// <summary>
    /// 現在のレイアウト要求に基づいてウィンドウの最小サイズを更新する。
    /// </summary>
    private void ApplyMinimumWindowSize() {
        MinWidth = TranslationCreationViewModel.MinWindowWidth;
        MinHeight = TranslationCreationViewModel.MinWindowHeight;
    }

    /// <summary>
    /// 現在の折り返し設定を詳細テキストボックスへ反映する。
    /// </summary>
    private void ApplyDictionaryDetailsTextWrapping() {
        var textWrapping = DataContext is TranslationCreationViewModel viewModel
            ? viewModel.DictionaryDetailsTextWrapping
            : TextWrapping.Wrap;

        if(SelectedOriginalTextBox is not null) {
            SelectedOriginalTextBox.TextWrapping = textWrapping;
        }

        if(SelectedTranslatedTextBox is not null) {
            SelectedTranslatedTextBox.TextWrapping = textWrapping;
        }
    }

    /// <summary>
    /// ViewModel の変更監視を開始する。
    /// </summary>
    /// <param name="viewModel">監視対象の ViewModel。</param>
    private void AttachViewModelEvents( TranslationCreationViewModel? viewModel ) {
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
    private void DetachViewModelEvents( TranslationCreationViewModel? viewModel ) {
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
    private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName is nameof( TranslationCreationViewModel.IsDictionaryDetailsWrapEnabled ) or nameof( TranslationCreationViewModel.DictionaryDetailsTextWrapping )) {
            ApplyDictionaryDetailsTextWrapping();
        }
    }

    /// <summary>
    /// 現在の dictionary 領域比率を設定へ保存する。
    /// </summary>
    private void PersistDictionaryPaneRatio() {
        if(DataContext is not TranslationCreationViewModel viewModel) {
            return;
        }

        viewModel.DictionaryPaneRatio = CalculateDictionaryPaneRatio();
    }

    /// <summary>
    /// 現在のウィンドウサイズを設定へ保存する。
    /// </summary>
    private void PersistWindowSize() {
        if(DataContext is not TranslationCreationViewModel viewModel) {
            return;
        }

        var bounds = WindowState == WindowState.Normal
            ? new Rect( Left, Top, Width, Height )
            : RestoreBounds;

        viewModel.WindowWidth = bounds.Width;
        viewModel.WindowHeight = bounds.Height;
    }

    /// <summary>
    /// 現在の行高から dictionary 領域比率を算出する。
    /// </summary>
    /// <returns>保存対象の dictionary 領域比率。</returns>
    private double CalculateDictionaryPaneRatio() {
        if(DictionaryDataGridRowDefinition.Height.IsStar && DictionaryDetailsRowDefinition.Height.IsStar && DictionaryDetailsRowDefinition.Height.Value > 0) {
            var starRatio = DictionaryDataGridRowDefinition.Height.Value / DictionaryDetailsRowDefinition.Height.Value;
            return TranslationCreationViewModel.NormalizeDictionaryPaneRatio( starRatio );
        }

        if(DictionaryDetailsRowDefinition.ActualHeight <= 0 || double.IsNaN( DictionaryDetailsRowDefinition.ActualHeight )) {
            return TranslationCreationViewModel.DefaultDictionaryPaneRatio;
        }

        var ratio = DictionaryDataGridRowDefinition.ActualHeight / DictionaryDetailsRowDefinition.ActualHeight;
        return TranslationCreationViewModel.NormalizeDictionaryPaneRatio( ratio );
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