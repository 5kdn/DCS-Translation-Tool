using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TextBox へ空白強調表示を付与する添付機能。
/// </summary>
public static class TextBoxWhitespaceHighlight {
    private static readonly DependencyProperty StateProperty = DependencyProperty.RegisterAttached(
        "State",
        typeof( TextBoxWhitespaceHighlightState ),
        typeof( TextBoxWhitespaceHighlight ),
        new PropertyMetadata( null ) );

    /// <summary>
    /// 空白強調表示を有効化するかどうかを取得または設定する。
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof( bool ),
        typeof( TextBoxWhitespaceHighlight ),
        new PropertyMetadata( false, OnIsEnabledChanged ) );

    /// <summary>
    /// 空白強調表示を有効化するかどうかを取得する。
    /// </summary>
    /// <param name="element">対象要素。</param>
    /// <returns>有効かどうか。</returns>
    public static bool GetIsEnabled( DependencyObject element ) => (bool)element.GetValue( IsEnabledProperty );

    /// <summary>
    /// 空白強調表示を有効化するかどうかを設定する。
    /// </summary>
    /// <param name="element">対象要素。</param>
    /// <param name="value">有効かどうか。</param>
    public static void SetIsEnabled( DependencyObject element, bool value ) => element.SetValue( IsEnabledProperty, value );

    private static void OnIsEnabledChanged( DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e ) {
        if(dependencyObject is not TextBox textBox) {
            return;
        }

        if((bool)e.NewValue) {
            var state = new TextBoxWhitespaceHighlightState( textBox );
            textBox.SetValue( StateProperty, state );
            state.Attach();
            return;
        }

        if(textBox.GetValue( StateProperty ) is TextBoxWhitespaceHighlightState existingState) {
            existingState.Detach();
            textBox.ClearValue( StateProperty );
        }
    }

    private sealed class TextBoxWhitespaceHighlightState( TextBox textBox ) {
        private readonly TextBox _textBox = textBox;
        private TextBoxWhitespaceHighlightAdorner? _adorner;
        private bool _isRefreshScheduled;

        internal void Attach() {
            _textBox.Loaded += OnRefreshRequested;
            _textBox.Unloaded += OnUnloaded;
            _textBox.TextChanged += OnRefreshRequested;
            _textBox.SizeChanged += OnRefreshRequested;
            _textBox.SelectionChanged += OnRefreshRequested;
            _textBox.AddHandler( ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler( OnScrollChanged ) );
            RefreshAdorner();
            ScheduleRefresh();
        }

        internal void Detach() {
            _textBox.Loaded -= OnRefreshRequested;
            _textBox.Unloaded -= OnUnloaded;
            _textBox.TextChanged -= OnRefreshRequested;
            _textBox.SizeChanged -= OnRefreshRequested;
            _textBox.SelectionChanged -= OnRefreshRequested;
            _textBox.RemoveHandler( ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler( OnScrollChanged ) );
            RemoveAdorner();
        }

        private void OnRefreshRequested( object? sender, RoutedEventArgs e ) {
            RefreshAdorner();
            ScheduleRefresh();
        }

        private void OnRefreshRequested( object? sender, SizeChangedEventArgs e ) {
            RefreshAdorner();
            ScheduleRefresh();
        }

        private void OnScrollChanged( object? sender, ScrollChangedEventArgs e ) {
            RefreshAdorner();
            ScheduleRefresh();
        }

        private void OnUnloaded( object? sender, RoutedEventArgs e ) {
            _isRefreshScheduled = false;
            RemoveAdorner();
        }

        private void RefreshAdorner() {
            var adornerLayer = AdornerLayer.GetAdornerLayer( _textBox );
            if(adornerLayer is null) {
                return;
            }

            if(_adorner is null) {
                _adorner = new TextBoxWhitespaceHighlightAdorner( _textBox );
                adornerLayer.Add( _adorner );
            }

            _adorner.InvalidateVisual();
        }

        private void ScheduleRefresh() {
            if(_isRefreshScheduled) {
                return;
            }

            _isRefreshScheduled = true;
            _textBox.Dispatcher.BeginInvoke( DispatcherPriority.Render, new Action( () => {
                _isRefreshScheduled = false;

                if(!_textBox.IsLoaded || _adorner is null) {
                    return;
                }

                if(AdornerLayer.GetAdornerLayer( _textBox ) is null) {
                    return;
                }

                RefreshAdorner();
            } ) );
        }

        private void RemoveAdorner() {
            if(_adorner is null) {
                return;
            }

            var adornerLayer = AdornerLayer.GetAdornerLayer( _textBox );
            adornerLayer?.Remove( _adorner );
            _adorner = null;
        }
    }
}