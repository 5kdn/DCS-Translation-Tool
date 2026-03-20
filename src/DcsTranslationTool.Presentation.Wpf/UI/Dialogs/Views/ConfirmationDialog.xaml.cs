using System.Windows;
using System.Windows.Controls;

using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;
using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Views;

/// <summary>
/// ConfirmationDialog.xaml の相互作用ロジックを提供する。
/// </summary>
public partial class ConfirmationDialog : UserControl {
    /// <summary>
    /// ConfirmationDialog の新しいインスタンスを初期化する。
    /// </summary>
    public ConfirmationDialog() {
        InitializeComponent();
        this.DataContextChanged += OnDataContextChanged;
        this.Loaded += OnLoaded;
    }

    /// <summary>
    /// ダイアログ引数に応じてボタン割り当てを更新する。
    /// </summary>
    private void OnLoaded( object sender, RoutedEventArgs e ) => ApplyParameters();

    /// <summary>
    /// ダイアログ引数の変更時にボタン割り当てを更新する。
    /// </summary>
    private void OnDataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) => ApplyParameters();

    /// <summary>
    /// 現在の引数に応じて各ボタンを構成する。
    /// </summary>
    private void ApplyParameters() {
        if(this.DataContext is not ConfirmationDialogParameters parameters) {
            return;
        }

        ConfigureButton( LeftButton, parameters, 0, isLastVisibleButton: false );
        ConfigureButton( CenterButton, parameters, 1, isLastVisibleButton: false );
        ConfigureButton( RightButton, parameters, 2, isLastVisibleButton: true );
    }

    /// <summary>
    /// 指定した位置のボタンを構成する。
    /// </summary>
    /// <param name="button">構成対象のボタン。</param>
    /// <param name="parameters">ダイアログ引数。</param>
    /// <param name="index">表示順の位置。</param>
    /// <param name="isLastVisibleButton">右端ボタンかどうか。</param>
    private void ConfigureButton( Button button, ConfirmationDialogParameters parameters, int index, bool isLastVisibleButton ) {
        if(index >= parameters.ButtonOrder.Count) {
            HideButton( button );
            return;
        }

        var result = parameters.ButtonOrder[index];
        if(!TryResolveButtonContent( parameters, result, out var content, out var styleKey )) {
            HideButton( button );
            return;
        }

        button.Content = content;
        button.CommandParameter = result;
        button.Style = ResolveButtonStyle( styleKey );
        button.Visibility = Visibility.Visible;
        if(!isLastVisibleButton) {
            button.Margin = (Thickness)FindResource( "XSmallRightMargin" );
        }
    }

    /// <summary>
    /// ボタン表示内容を解決する。
    /// </summary>
    /// <param name="parameters">ダイアログ引数。</param>
    /// <param name="result">ボタン結果。</param>
    /// <param name="content">表示文言。</param>
    /// <param name="styleKey">スタイルキー。</param>
    /// <returns>表示する場合は <see langword="true"/> を返す。</returns>
    private static bool TryResolveButtonContent(
        ConfirmationDialogParameters parameters,
        ConfirmationDialogResult result,
        out string content,
        out string styleKey ) {
        switch(result) {
            case ConfirmationDialogResult.Confirm:
                content = parameters.ConfirmButtonText;
                styleKey = parameters.ConfirmButtonStyleKey;
                return !string.IsNullOrWhiteSpace( content );
            case ConfirmationDialogResult.Secondary:
                content = parameters.SecondaryButtonText;
                styleKey = parameters.SecondaryButtonStyleKey;
                return parameters.HasSecondaryButton;
            case ConfirmationDialogResult.Cancel:
                content = parameters.CancelButtonText;
                styleKey = parameters.CancelButtonStyleKey;
                return !string.IsNullOrWhiteSpace( content );
            default:
                content = string.Empty;
                styleKey = "MaterialDesignFlatButton";
                return false;
        }
    }

    /// <summary>
    /// 指定したスタイルキーに対応するスタイルを取得する。
    /// </summary>
    /// <param name="styleKey">スタイルキー。</param>
    /// <returns>解決したスタイル。</returns>
    private Style ResolveButtonStyle( string styleKey ) =>
        TryFindResource( styleKey ) as Style
        ?? TryFindResource( "MaterialDesignFlatButton" ) as Style
        ?? throw new InvalidOperationException( "MaterialDesignFlatButton スタイルが見つからない。" );

    /// <summary>
    /// ボタンを非表示にする。
    /// </summary>
    /// <param name="button">非表示にするボタン。</param>
    private static void HideButton( Button button ) {
        button.Visibility = Visibility.Collapsed;
        button.Content = null;
        button.CommandParameter = null;
        button.Style = null;
    }
}