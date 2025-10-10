using System.Windows;
using System.Windows.Controls;

using MaterialDesignThemes.Wpf;

namespace DcsTranslationTool.Presentation.Wpf.Features.Common;

/// <summary>
/// アイコンとテキストを持つアクションボタンのユーザーコントロール。
/// 内部ボタンのクリックを <see cref="Click"/> ルーティングイベントとして発火する。
/// </summary>
public partial class ActionButton : UserControl {
    /// <summary>
    /// クリック時に発火するバブル型ルーティングイベント。
    /// </summary>
    public static readonly RoutedEvent ClickEvent =
        EventManager.RegisterRoutedEvent(
            name: "Click",
            routingStrategy: RoutingStrategy.Bubble,
            handlerType: typeof(RoutedEventHandler),
            ownerType: typeof(ActionButton));

    /// <summary>
    /// クリックイベントの .NET イベントラッパー。
    /// </summary>
    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    /// <summary>
    /// ボタンの表示テキストを表す依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(
            name: nameof(Text),
            propertyType: typeof(string),
            ownerType: typeof(ActionButton),
            typeMetadata: new FrameworkPropertyMetadata(default(string), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// ボタンに表示するアイコン種別を表す依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(
            name: nameof(IconKind),
            propertyType: typeof(PackIconKind),
            ownerType: typeof(ActionButton),
            typeMetadata: new FrameworkPropertyMetadata(PackIconKind.None, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// ボタンの表示テキスト。
    /// </summary>
    public string? Text {
        get => (string?)GetValue( TextProperty );
        set => SetValue( TextProperty, value );
    }

    /// <summary>
    /// 表示する MaterialDesign アイコン種別。
    /// </summary>
    public PackIconKind IconKind {
        get => (PackIconKind)GetValue( IconKindProperty );
        set => SetValue( IconKindProperty, value );
    }

    /// <summary>
    /// 既定のコンストラクタ。
    /// </summary>
    public ActionButton() {
        InitializeComponent();

        // 内部ボタンの Click を自作 Click へ変換する
        // Unloaded 時にハンドラを外す
        ButtonElement.Click += OnInnerButtonClick;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// 内部ボタンクリック時に自作 Click ルーティングイベントを発火する。
    /// </summary>
    private void OnInnerButtonClick( object sender, RoutedEventArgs e ) {
        try {
            var args = new RoutedEventArgs(ClickEvent, this);
            RaiseEvent( args );
        }
        catch(Exception ex) {
            // 想定外の例外はアプリ全体へ伝播させる設計とし、ここでは握りつぶさない
            // 必要に応じてロギングを追加する
            System.Diagnostics.Trace.WriteLine( ex );
            throw;
        }
    }

    /// <summary>
    /// アンロード時にイベントハンドラを解除する。
    /// </summary>
    private void OnUnloaded( object? sender, RoutedEventArgs e ) {
        ButtonElement.Click -= OnInnerButtonClick;
        Unloaded -= OnUnloaded;
    }

    /// <summary>
    /// 進捗インジケータの表示可否を表す依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty IsIndicatorVisibleProperty =
        DependencyProperty.Register(
            name: nameof(IsIndicatorVisible),
            propertyType: typeof(bool),
            ownerType: typeof(ActionButton),
            typeMetadata: new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 進捗が不確定かどうかを表す依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty IsIndeterminateProperty =
        DependencyProperty.Register(
            name: nameof(IsIndeterminate),
            propertyType: typeof(bool),
            ownerType: typeof(ActionButton),
            typeMetadata: new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 進捗値(0〜100)を表す依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty ProgressValueProperty =
        DependencyProperty.Register(
            name: nameof(ProgressValue),
            propertyType: typeof(double),
            ownerType: typeof(ActionButton),
            typeMetadata: new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// 進捗インジケータの表示可否。
    /// </summary>
    public bool IsIndicatorVisible {
        get => (bool)GetValue( IsIndicatorVisibleProperty );
        set => SetValue( IsIndicatorVisibleProperty, value );
    }

    /// <summary>
    /// 不確定進捗かどうか。
    /// </summary>
    public bool IsIndeterminate {
        get => (bool)GetValue( IsIndeterminateProperty );
        set => SetValue( IsIndeterminateProperty, value );
    }

    /// <summary>
    /// 進捗値(0〜100)。
    /// </summary>
    public double ProgressValue {
        get => (double)GetValue( ProgressValueProperty );
        set => SetValue( ProgressValueProperty, value );
    }
}