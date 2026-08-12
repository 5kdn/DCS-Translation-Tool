using System.Windows;
using System.Windows.Controls;

using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Views;

/// <summary>
/// アプリケーション更新の取得進捗ダイアログを表示する。
/// </summary>
public partial class ApplicationUpdateProgressDialog : UserControl {
    /// <summary>
    /// ダイアログを初期化する。
    /// </summary>
    public ApplicationUpdateProgressDialog() => InitializeComponent();

    private async void OnLoaded( object sender, RoutedEventArgs e ) {
        if(this.DataContext is ApplicationUpdateProgressDialogParameters parameters) {
            await parameters.RunAsync( this );
        }
    }

    private void OnCancel( object sender, RoutedEventArgs e ) {
        if(this.DataContext is ApplicationUpdateProgressDialogParameters parameters) {
            parameters.Cancel();
        }
    }
}