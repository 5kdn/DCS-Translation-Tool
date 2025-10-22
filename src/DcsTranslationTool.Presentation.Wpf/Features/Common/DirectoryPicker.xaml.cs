using System.Windows;
using System.Windows.Controls;

namespace DcsTranslationTool.Presentation.Wpf.Features.Common;

/// <summary>
/// ディレクトリパス入力用の共通コンポーネント。
/// ラベル、パス入力 TextBox、参照ボタンを提供する。
/// </summary>
public partial class DirectoryPicker : UserControl {
    public DirectoryPicker() {
        InitializeComponent();
    }

    #region DependencyProperties

    /// <summary>
    /// ラベル表示テキスト
    /// </summary>
    public static readonly DependencyProperty LabelTextProperty =
        DependencyProperty.Register(
            nameof(LabelText),
            typeof(string),
            typeof(DirectoryPicker),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 説明文テキスト。null の場合は非表示
    /// </summary>
    public static readonly DependencyProperty ExplanationTextProperty =
        DependencyProperty.Register(
            nameof(ExplanationText),
            typeof(string),
            typeof(DirectoryPicker),
            new PropertyMetadata(null));

    /// <summary>
    /// ディレクトリパス文字列（TwoWay）
    /// </summary>
    public static readonly DependencyProperty PathProperty =
        DependencyProperty.Register(
            nameof(Path),
            typeof(string),
            typeof(DirectoryPicker),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 参照ボタンテキスト。既定は「参照」
    /// </summary>
    public static readonly DependencyProperty ButtonTextProperty =
        DependencyProperty.Register(
            nameof(ButtonText),
            typeof(string),
            typeof(DirectoryPicker),
            new PropertyMetadata("参照"));

    /// <summary>
    /// TextBox の読み取り専用可否
    /// </summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(DirectoryPicker),
            new PropertyMetadata(false));

    /// <summary>
    /// 呼び出し側で識別するためのキー
    /// </summary>
    public static readonly DependencyProperty KeyProperty =
        DependencyProperty.Register(
            nameof(Key),
            typeof(string),
            typeof(DirectoryPicker),
            new PropertyMetadata(string.Empty));

    #endregion

    #region Properties

    /// <summary>
    /// ラベル表示テキスト
    /// </summary>
    public string LabelText {
        get => (string)GetValue( LabelTextProperty );
        set => SetValue( LabelTextProperty, value );
    }

    /// <summary>
    /// 説明文テキスト
    /// </summary>
    public string? ExplanationText {
        get => (string?)GetValue( ExplanationTextProperty );
        set => SetValue( ExplanationTextProperty, value );
    }

    /// <summary>
    /// ディレクトリパス文字列（TwoWay）
    /// </summary>
    public string Path {
        get => (string)GetValue( PathProperty );
        set => SetValue( PathProperty, value );
    }

    /// <summary>
    /// 参照ボタンテキスト
    /// </summary>
    public string ButtonText {
        get => (string)GetValue( ButtonTextProperty );
        set => SetValue( ButtonTextProperty, value );
    }

    /// <summary>
    /// TextBox の読み取り専用可否
    /// </summary>
    public bool IsReadOnly {
        get => (bool)GetValue( IsReadOnlyProperty );
        set => SetValue( IsReadOnlyProperty, value );
    }

    /// <summary>
    /// 呼び出し側で識別するためのキー
    /// </summary>
    public string Key {
        get => (string)GetValue( KeyProperty );
        set => SetValue( KeyProperty, value );
    }
    #endregion
}