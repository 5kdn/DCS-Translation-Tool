using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView.xaml の相互作用ロジックである。
/// </summary>
public partial class TranslationCreationView : Window {
    private const double DefaultWindowWidth = 1200;
    private const double DefaultWindowHeight = 900;
    private const double MinWindowWidth = 640;
    private const double MinWindowHeight = 480;
    private const double DefaultDictionaryPaneRatio = 2;
    private const double DetailPaneBaseRatio = 1;
    private const double MinDictionaryPaneRatio = 0.2;
    private const double MaxDictionaryPaneRatio = 8;

    public TranslationCreationView() {
        InitializeComponent();
        DataContextChanged += Window_DataContextChanged;
        Loaded += Window_Loaded;
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
        ApplyWindowSize();
        ApplyDictionaryPaneRatio();
    }

    private void Window_Loaded( object sender, RoutedEventArgs e ) {
        ApplyWindowSize();
        ApplyDictionaryPaneRatio();
    }

    private void Window_Closing( object? sender, CancelEventArgs e ) {
        PersistWindowSize();
        PersistDictionaryPaneRatio();
    }

    private void DictionaryPaneGridSplitter_DragCompleted( object sender, DragCompletedEventArgs e ) =>
        PersistDictionaryPaneRatio();

    /// <summary>
    /// 保存済みの dictionary 領域比率をレイアウトへ反映する。
    /// </summary>
    private void ApplyDictionaryPaneRatio() {
        var ratio = DataContext is TranslationCreationViewModel viewModel
            ? NormalizeDictionaryPaneRatio( viewModel.AppSettings.TranslationCreationDictionaryPaneRatio )
            : DefaultDictionaryPaneRatio;

        DictionaryDataGridRowDefinition.Height = new GridLength( ratio, GridUnitType.Star );
        DictionaryDetailsRowDefinition.Height = new GridLength( DetailPaneBaseRatio, GridUnitType.Star );
    }

    /// <summary>
    /// 保存済みのウィンドウサイズをレイアウトへ反映する。
    /// </summary>
    private void ApplyWindowSize() {
        var width = DataContext is TranslationCreationViewModel viewModel
            ? NormalizeWindowLength( viewModel.AppSettings.TranslationCreationWindowWidth, DefaultWindowWidth, MinWindowWidth )
            : DefaultWindowWidth;
        var height = DataContext is TranslationCreationViewModel sizedViewModel
            ? NormalizeWindowLength( sizedViewModel.AppSettings.TranslationCreationWindowHeight, DefaultWindowHeight, MinWindowHeight )
            : DefaultWindowHeight;

        Width = width;
        Height = height;
    }

    /// <summary>
    /// 現在の dictionary 領域比率を設定へ保存する。
    /// </summary>
    private void PersistDictionaryPaneRatio() {
        if(DataContext is not TranslationCreationViewModel viewModel) {
            return;
        }

        viewModel.AppSettings.TranslationCreationDictionaryPaneRatio = CalculateDictionaryPaneRatio();
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

        viewModel.AppSettings.TranslationCreationWindowWidth =
            NormalizeWindowLength( bounds.Width, DefaultWindowWidth, MinWindowWidth );
        viewModel.AppSettings.TranslationCreationWindowHeight =
            NormalizeWindowLength( bounds.Height, DefaultWindowHeight, MinWindowHeight );
    }

    /// <summary>
    /// 現在の行高から dictionary 領域比率を算出する。
    /// </summary>
    /// <returns>保存対象の dictionary 領域比率。</returns>
    private double CalculateDictionaryPaneRatio() {
        if(DictionaryDataGridRowDefinition.Height.IsStar && DictionaryDetailsRowDefinition.Height.IsStar && DictionaryDetailsRowDefinition.Height.Value > 0) {
            var starRatio = DictionaryDataGridRowDefinition.Height.Value / DictionaryDetailsRowDefinition.Height.Value;
            return NormalizeDictionaryPaneRatio( starRatio );
        }

        if(DictionaryDetailsRowDefinition.ActualHeight <= 0 || double.IsNaN( DictionaryDetailsRowDefinition.ActualHeight )) {
            return DefaultDictionaryPaneRatio;
        }

        var ratio = DictionaryDataGridRowDefinition.ActualHeight / DictionaryDetailsRowDefinition.ActualHeight;
        return NormalizeDictionaryPaneRatio( ratio );
    }

    /// <summary>
    /// dictionary 領域比率を有効範囲へ正規化する。
    /// </summary>
    /// <param name="ratio">検証対象の比率。</param>
    /// <returns>有効範囲内へ補正した比率。</returns>
    private static double NormalizeDictionaryPaneRatio( double ratio ) {
        if(double.IsNaN( ratio ) || double.IsInfinity( ratio ) || ratio <= 0) {
            return DefaultDictionaryPaneRatio;
        }

        return Math.Clamp( ratio, MinDictionaryPaneRatio, MaxDictionaryPaneRatio );
    }

    /// <summary>
    /// ウィンドウサイズを有効範囲へ正規化する。
    /// </summary>
    /// <param name="value">検証対象のサイズ。</param>
    /// <param name="fallback">不正値時の既定サイズ。</param>
    /// <param name="minimum">許容する最小サイズ。</param>
    /// <returns>有効範囲内へ補正したサイズ。</returns>
    private static double NormalizeWindowLength( double value, double fallback, double minimum ) {
        if(double.IsNaN( value ) || double.IsInfinity( value ) || value <= 0) {
            return fallback;
        }

        return Math.Max( minimum, value );
    }
}