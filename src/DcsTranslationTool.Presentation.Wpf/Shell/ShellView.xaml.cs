using System.Windows;

namespace DcsTranslationTool.Presentation.Wpf.Shell;
/// <summary>
/// MainView.xaml の相互作用ロジック
/// </summary>
public partial class ShellView : Window {
    public ShellView() {
        InitializeComponent();
    }

    private void CloseWindowButton_Click( object sender, RoutedEventArgs e ) => Close();
}