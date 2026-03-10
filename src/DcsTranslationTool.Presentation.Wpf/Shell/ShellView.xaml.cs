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
        RootLayout.UpdateLayout();

        var desiredSize = RootLayout.DesiredSize;
        var windowChrome = WindowChrome.GetWindowChrome( this );
        var resizeBorderThickness = windowChrome?.ResizeBorderThickness ?? default;

        MinWidth = desiredSize.Width + resizeBorderThickness.Left + resizeBorderThickness.Right;
        MinHeight = desiredSize.Height + resizeBorderThickness.Top + resizeBorderThickness.Bottom;
    }
}