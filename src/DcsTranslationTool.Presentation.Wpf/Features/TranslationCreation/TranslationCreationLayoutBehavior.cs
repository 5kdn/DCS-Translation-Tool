using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

using Microsoft.Xaml.Behaviors;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView のレイアウト反映と保存を仲介する。
/// </summary>
public sealed class TranslationCreationLayoutBehavior : Behavior<Window> {
    private TranslationCreationWindowCoordinator? _windowCoordinator;

    /// <summary>
    /// dictionary 一覧行定義を取得または設定する。
    /// </summary>
    public RowDefinition? DictionaryDataGridRowDefinition {
        get => (RowDefinition?)GetValue( DictionaryDataGridRowDefinitionProperty );
        set => SetValue( DictionaryDataGridRowDefinitionProperty, value );
    }

    /// <summary>
    /// dictionary 詳細行定義を取得または設定する。
    /// </summary>
    public RowDefinition? DictionaryDetailsRowDefinition {
        get => (RowDefinition?)GetValue( DictionaryDetailsRowDefinitionProperty );
        set => SetValue( DictionaryDetailsRowDefinitionProperty, value );
    }

    /// <summary>
    /// dictionary 領域分割用 GridSplitter を取得または設定する。
    /// </summary>
    public GridSplitter? DictionaryPaneGridSplitter {
        get => (GridSplitter?)GetValue( DictionaryPaneGridSplitterProperty );
        set => SetValue( DictionaryPaneGridSplitterProperty, value );
    }

    /// <summary>
    /// dictionary 一覧行定義を保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty DictionaryDataGridRowDefinitionProperty =
        DependencyProperty.Register(
            nameof( DictionaryDataGridRowDefinition ),
            typeof( RowDefinition ),
            typeof( TranslationCreationLayoutBehavior ),
            new PropertyMetadata( null, OnCoordinatorInputChanged ) );

    /// <summary>
    /// dictionary 詳細行定義を保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty DictionaryDetailsRowDefinitionProperty =
        DependencyProperty.Register(
            nameof( DictionaryDetailsRowDefinition ),
            typeof( RowDefinition ),
            typeof( TranslationCreationLayoutBehavior ),
            new PropertyMetadata( null, OnCoordinatorInputChanged ) );

    /// <summary>
    /// GridSplitter を保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty DictionaryPaneGridSplitterProperty =
        DependencyProperty.Register(
            nameof( DictionaryPaneGridSplitter ),
            typeof( GridSplitter ),
            typeof( TranslationCreationLayoutBehavior ),
            new PropertyMetadata( null, OnDictionaryPaneGridSplitterChanged ) );

    /// <summary>
    /// 関連ウィンドウへイベント購読を接続する。
    /// </summary>
    protected override void OnAttached() {
        base.OnAttached();
        EnsureWindowCoordinator();
        AssociatedObject.DataContextChanged += Window_DataContextChanged;
        AssociatedObject.Loaded += Window_Loaded;
        AssociatedObject.Closed += Window_Closed;
        AttachGridSplitter( DictionaryPaneGridSplitter );
    }

    /// <summary>
    /// 関連ウィンドウからイベント購読を解除する。
    /// </summary>
    protected override void OnDetaching() {
        DetachGridSplitter( DictionaryPaneGridSplitter );

        if(AssociatedObject is not null) {
            AssociatedObject.DataContextChanged -= Window_DataContextChanged;
            AssociatedObject.Loaded -= Window_Loaded;
            AssociatedObject.Closed -= Window_Closed;
        }

        base.OnDetaching();
    }

    /// <summary>
    /// DataContext 変更時にレイアウトを反映する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) =>
        _windowCoordinator?.ApplyLayout( e.NewValue as ITranslationCreationViewModel );

    /// <summary>
    /// Loaded 時に保存済みレイアウトを反映する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_Loaded( object sender, RoutedEventArgs e ) =>
        _windowCoordinator?.ApplyLayout( GetViewModel() );

    /// <summary>
    /// Closed 時に現在のレイアウトを保存する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_Closed( object? sender, EventArgs e ) =>
        _windowCoordinator?.PersistLayout( GetViewModel() );

    /// <summary>
    /// GridSplitter 操作完了時に pane 比率を保存する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void DictionaryPaneGridSplitter_DragCompleted( object sender, DragCompletedEventArgs e ) =>
        _windowCoordinator?.PersistLayout( GetViewModel() );

    /// <summary>
    /// 現在の DataContext から ViewModel を取得する。
    /// </summary>
    /// <returns>関連 ViewModel を返す。</returns>
    private ITranslationCreationViewModel? GetViewModel() => AssociatedObject?.DataContext as ITranslationCreationViewModel;

    /// <summary>
    /// GridSplitter への購読を開始する。
    /// </summary>
    /// <param name="gridSplitter">購読対象。</param>
    private void AttachGridSplitter( GridSplitter? gridSplitter ) {
        if(AssociatedObject is null || gridSplitter is null) {
            return;
        }

        gridSplitter.DragCompleted += DictionaryPaneGridSplitter_DragCompleted;
    }

    /// <summary>
    /// GridSplitter への購読を解除する。
    /// </summary>
    /// <param name="gridSplitter">解除対象。</param>
    private void DetachGridSplitter( GridSplitter? gridSplitter ) {
        if(gridSplitter is null) {
            return;
        }

        gridSplitter.DragCompleted -= DictionaryPaneGridSplitter_DragCompleted;
    }

    /// <summary>
    /// Coordinator の生成状態を更新する。
    /// </summary>
    private void EnsureWindowCoordinator() {
        if(AssociatedObject is null
           || DictionaryDataGridRowDefinition is null
           || DictionaryDetailsRowDefinition is null) {
            _windowCoordinator = null;
            return;
        }

        _windowCoordinator = new(
            AssociatedObject,
            DictionaryDataGridRowDefinition,
            DictionaryDetailsRowDefinition );
    }

    /// <summary>
    /// 行定義変更時に Coordinator を再生成する。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnCoordinatorInputChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) =>
        ((TranslationCreationLayoutBehavior)d).EnsureWindowCoordinator();

    /// <summary>
    /// GridSplitter 変更時に購読を差し替える。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnDictionaryPaneGridSplitterChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) {
        var behavior = (TranslationCreationLayoutBehavior)d;
        behavior.DetachGridSplitter( e.OldValue as GridSplitter );
        behavior.AttachGridSplitter( e.NewValue as GridSplitter );
    }
}