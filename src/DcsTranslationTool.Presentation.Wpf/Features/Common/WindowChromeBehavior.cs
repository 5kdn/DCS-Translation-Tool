using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

using Microsoft.Xaml.Behaviors;

namespace DcsTranslationTool.Presentation.Wpf.Features.Common;

/// <summary>
/// 共通ウィンドウタイトルバーの操作を仲介する。
/// </summary>
public sealed class WindowChromeBehavior : Behavior<Window> {
    /// <summary>
    /// タイトルバー要素を取得または設定する。
    /// </summary>
    public FrameworkElement? TitleBar {
        get => (FrameworkElement?)GetValue( TitleBarProperty );
        set => SetValue( TitleBarProperty, value );
    }

    /// <summary>
    /// 最小化ボタンを取得または設定する。
    /// </summary>
    public ButtonBase? MinimizeButton {
        get => (ButtonBase?)GetValue( MinimizeButtonProperty );
        set => SetValue( MinimizeButtonProperty, value );
    }

    /// <summary>
    /// 最大化切替ボタンを取得または設定する。
    /// </summary>
    public ButtonBase? ToggleMaximizeButton {
        get => (ButtonBase?)GetValue( ToggleMaximizeButtonProperty );
        set => SetValue( ToggleMaximizeButtonProperty, value );
    }

    /// <summary>
    /// 閉じるボタンを取得または設定する。
    /// </summary>
    public ButtonBase? CloseButton {
        get => (ButtonBase?)GetValue( CloseButtonProperty );
        set => SetValue( CloseButtonProperty, value );
    }

    /// <summary>
    /// タイトルバー要素を保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty TitleBarProperty =
        DependencyProperty.Register(
            nameof( TitleBar ),
            typeof( FrameworkElement ),
            typeof( WindowChromeBehavior ),
            new PropertyMetadata( null, OnTitleBarChanged ) );

    /// <summary>
    /// 最小化ボタンを保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty MinimizeButtonProperty =
        DependencyProperty.Register(
            nameof( MinimizeButton ),
            typeof( ButtonBase ),
            typeof( WindowChromeBehavior ),
            new PropertyMetadata( null, OnMinimizeButtonChanged ) );

    /// <summary>
    /// 最大化切替ボタンを保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty ToggleMaximizeButtonProperty =
        DependencyProperty.Register(
            nameof( ToggleMaximizeButton ),
            typeof( ButtonBase ),
            typeof( WindowChromeBehavior ),
            new PropertyMetadata( null, OnToggleMaximizeButtonChanged ) );

    /// <summary>
    /// 閉じるボタンを保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty CloseButtonProperty =
        DependencyProperty.Register(
            nameof( CloseButton ),
            typeof( ButtonBase ),
            typeof( WindowChromeBehavior ),
            new PropertyMetadata( null, OnCloseButtonChanged ) );

    /// <summary>
    /// 関連ウィンドウへイベント購読を接続する。
    /// </summary>
    protected override void OnAttached() {
        base.OnAttached();
        AttachTitleBar( TitleBar );
        AttachMinimizeButton( MinimizeButton );
        AttachToggleMaximizeButton( ToggleMaximizeButton );
        AttachCloseButton( CloseButton );
    }

    /// <summary>
    /// 関連ウィンドウからイベント購読を解除する。
    /// </summary>
    protected override void OnDetaching() {
        DetachTitleBar( TitleBar );
        DetachMinimizeButton( MinimizeButton );
        DetachToggleMaximizeButton( ToggleMaximizeButton );
        DetachCloseButton( CloseButton );
        base.OnDetaching();
    }

    /// <summary>
    /// タイトルバー押下時にドラッグまたは最大化切替を行う。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void TitleBar_MouseLeftButtonDown( object sender, MouseButtonEventArgs e ) {
        if(AssociatedObject is null) {
            return;
        }

        if(e.ClickCount == 2) {
            ToggleMaximizeWindow();
            return;
        }

        AssociatedObject.DragMove();
    }

    /// <summary>
    /// 最小化ボタン押下時にウィンドウを最小化する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void MinimizeButton_Click( object sender, RoutedEventArgs e ) {
        if(AssociatedObject is null) {
            return;
        }

        AssociatedObject.WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// 最大化切替ボタン押下時にウィンドウ状態を切り替える。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void ToggleMaximizeButton_Click( object sender, RoutedEventArgs e ) => ToggleMaximizeWindow();

    /// <summary>
    /// 閉じるボタン押下時にウィンドウを閉じる。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void CloseButton_Click( object sender, RoutedEventArgs e ) => AssociatedObject?.Close();

    /// <summary>
    /// タイトルバー要素への購読を開始する。
    /// </summary>
    /// <param name="titleBar">購読対象。</param>
    private void AttachTitleBar( FrameworkElement? titleBar ) {
        if(AssociatedObject is null || titleBar is null) {
            return;
        }

        titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
    }

    /// <summary>
    /// タイトルバー要素への購読を解除する。
    /// </summary>
    /// <param name="titleBar">解除対象。</param>
    private void DetachTitleBar( FrameworkElement? titleBar ) {
        if(titleBar is null) {
            return;
        }

        titleBar.MouseLeftButtonDown -= TitleBar_MouseLeftButtonDown;
    }

    /// <summary>
    /// 最小化ボタンへの購読を開始する。
    /// </summary>
    /// <param name="button">購読対象。</param>
    private void AttachMinimizeButton( ButtonBase? button ) {
        if(AssociatedObject is null || button is null) {
            return;
        }

        button.Click += MinimizeButton_Click;
    }

    /// <summary>
    /// 最小化ボタンへの購読を解除する。
    /// </summary>
    /// <param name="button">解除対象。</param>
    private void DetachMinimizeButton( ButtonBase? button ) {
        if(button is null) {
            return;
        }

        button.Click -= MinimizeButton_Click;
    }

    /// <summary>
    /// 最大化切替ボタンへの購読を開始する。
    /// </summary>
    /// <param name="button">購読対象。</param>
    private void AttachToggleMaximizeButton( ButtonBase? button ) {
        if(AssociatedObject is null || button is null) {
            return;
        }

        button.Click += ToggleMaximizeButton_Click;
    }

    /// <summary>
    /// 最大化切替ボタンへの購読を解除する。
    /// </summary>
    /// <param name="button">解除対象。</param>
    private void DetachToggleMaximizeButton( ButtonBase? button ) {
        if(button is null) {
            return;
        }

        button.Click -= ToggleMaximizeButton_Click;
    }

    /// <summary>
    /// 閉じるボタンへの購読を開始する。
    /// </summary>
    /// <param name="button">購読対象。</param>
    private void AttachCloseButton( ButtonBase? button ) {
        if(AssociatedObject is null || button is null) {
            return;
        }

        button.Click += CloseButton_Click;
    }

    /// <summary>
    /// 閉じるボタンへの購読を解除する。
    /// </summary>
    /// <param name="button">解除対象。</param>
    private void DetachCloseButton( ButtonBase? button ) {
        if(button is null) {
            return;
        }

        button.Click -= CloseButton_Click;
    }

    /// <summary>
    /// ウィンドウの最大化状態を切り替える。
    /// </summary>
    private void ToggleMaximizeWindow() {
        if(AssociatedObject is null) {
            return;
        }

        AssociatedObject.WindowState = AssociatedObject.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>
    /// タイトルバー要素変更時の購読を更新する。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnTitleBarChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) {
        var behavior = (WindowChromeBehavior)d;
        behavior.DetachTitleBar( e.OldValue as FrameworkElement );
        behavior.AttachTitleBar( e.NewValue as FrameworkElement );
    }

    /// <summary>
    /// 最小化ボタン変更時の購読を更新する。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnMinimizeButtonChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) {
        var behavior = (WindowChromeBehavior)d;
        behavior.DetachMinimizeButton( e.OldValue as ButtonBase );
        behavior.AttachMinimizeButton( e.NewValue as ButtonBase );
    }

    /// <summary>
    /// 最大化切替ボタン変更時の購読を更新する。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnToggleMaximizeButtonChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) {
        var behavior = (WindowChromeBehavior)d;
        behavior.DetachToggleMaximizeButton( e.OldValue as ButtonBase );
        behavior.AttachToggleMaximizeButton( e.NewValue as ButtonBase );
    }

    /// <summary>
    /// 閉じるボタン変更時の購読を更新する。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnCloseButtonChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) {
        var behavior = (WindowChromeBehavior)d;
        behavior.DetachCloseButton( e.OldValue as ButtonBase );
        behavior.AttachCloseButton( e.NewValue as ButtonBase );
    }
}