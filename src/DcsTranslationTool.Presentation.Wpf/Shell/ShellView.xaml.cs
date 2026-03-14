using System.Windows;
using System.Windows.Navigation;
using System.Windows.Shell;

namespace DcsTranslationTool.Presentation.Wpf.Shell;
/// <summary>
/// MainView.xaml の相互作用ロジック
/// </summary>
public partial class ShellView : Window {
    public ShellView() {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void CloseWindowButton_Click( object sender, RoutedEventArgs e ) => Close();

    /// <summary>
    /// 初回表示後に Shell の最小サイズを同期する。
    /// </summary>
    private void OnLoaded( object sender, RoutedEventArgs e ) => UpdateMinimumShellSize();

    /// <summary>
    /// ページ遷移後に Shell の最小サイズを再同期する。
    /// </summary>
    private void RootFrame_Navigated( object sender, NavigationEventArgs e ) => UpdateMinimumShellSize();

    /// <summary>
    /// 現在のレイアウト要求に基づいて Shell の最小サイズを更新する。
    /// </summary>
    private void UpdateMinimumShellSize() {
        var desiredSize = MeasureMinimumContentSize( RootLayout );
        var windowChrome = WindowChrome.GetWindowChrome( this );
        var resizeBorderThickness = windowChrome?.ResizeBorderThickness ?? default;

        MinWidth = desiredSize.Width + resizeBorderThickness.Left + resizeBorderThickness.Right;
        MinHeight = desiredSize.Height + resizeBorderThickness.Top + resizeBorderThickness.Bottom;
    }

    /// <summary>
    /// 要素が必要とする最小コンテンツサイズを計測する。
    /// </summary>
    /// <param name="element">計測対象の要素。</param>
    /// <returns>無制約計測で得られた最小要求サイズ。</returns>
    internal static Size MeasureMinimumContentSize( FrameworkElement element ) {
        ArgumentNullException.ThrowIfNull( element );

        var restoreSize = new Size(
            Math.Max( element.ActualWidth, 0 ),
            Math.Max( element.ActualHeight, 0 ) );

        element.UpdateLayout();
        element.Measure( new Size( double.PositiveInfinity, double.PositiveInfinity ) );
        var desiredSize = element.DesiredSize;

        if(restoreSize.Width > 0 || restoreSize.Height > 0) {
            element.Measure( restoreSize );
        }

        return desiredSize;
    }
}