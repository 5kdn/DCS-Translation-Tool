using System.Windows;
using System.Windows.Input;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView.xaml の相互作用ロジックである。
/// </summary>
public partial class TranslationCreationView : Window {
    public TranslationCreationView() {
        InitializeComponent();
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
}