using System.Windows;
using System.Windows.Controls;

using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Views;

/// <summary>
/// DownloadModifiedApplySelectionDialog.xaml の相互作用ロジックを提供する。
/// </summary>
public partial class DownloadModifiedApplySelectionDialog : UserControl {
    /// <summary>
    /// DownloadModifiedApplySelectionDialog の新しいインスタンスを初期化する。
    /// </summary>
    public DownloadModifiedApplySelectionDialog() {
        InitializeComponent();
    }

    /// <summary>
    /// 確定クリック時に現在の選択結果でダイアログを閉じる。
    /// </summary>
    /// <param name="sender">イベント発生元。</param>
    /// <param name="e">イベント引数。</param>
    private void OnConfirmClick( object sender, RoutedEventArgs e ) {
        _ = sender;
        _ = e;

        if(this.DataContext is not DownloadModifiedApplySelectionDialogParameters parameters) {
            DialogHost.CloseDialogCommand.Execute( null, this );
            return;
        }

        DialogHost.CloseDialogCommand.Execute( parameters.CreateResult(), this );
    }
}