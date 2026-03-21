using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

using Microsoft.Xaml.Behaviors;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView の dictionary 一覧フィルター再評価を仲介する。
/// </summary>
public sealed class TranslationCreationDictionaryFilterBehavior : Behavior<Window> {
    private ITranslationCreationViewModel? _subscribedViewModel;

    /// <summary>
    /// 対象 CollectionViewSource を取得または設定する。
    /// </summary>
    public CollectionViewSource? CollectionViewSource {
        get => (CollectionViewSource?)GetValue( CollectionViewSourceProperty );
        set => SetValue( CollectionViewSourceProperty, value );
    }

    /// <summary>
    /// 対象 CollectionViewSource を保持する依存関係プロパティ。
    /// </summary>
    public static readonly DependencyProperty CollectionViewSourceProperty =
        DependencyProperty.Register(
            nameof( CollectionViewSource ),
            typeof( CollectionViewSource ),
            typeof( TranslationCreationDictionaryFilterBehavior ),
            new PropertyMetadata( null, OnCollectionViewSourceChanged ) );

    /// <summary>
    /// 関連ウィンドウへイベント購読を接続する。
    /// </summary>
    protected override void OnAttached() {
        base.OnAttached();
        AttachCollectionViewSource( CollectionViewSource );
        AssociatedObject.DataContextChanged += Window_DataContextChanged;
        AssociatedObject.Closed += Window_Closed;
        SubscribeViewModel( GetViewModel() );
        RefreshFilteredDictionaryItems();
    }

    /// <summary>
    /// 関連ウィンドウからイベント購読を解除する。
    /// </summary>
    protected override void OnDetaching() {
        UnsubscribeViewModel( _subscribedViewModel );

        if(AssociatedObject is not null) {
            AssociatedObject.DataContextChanged -= Window_DataContextChanged;
            AssociatedObject.Closed -= Window_Closed;
        }

        DetachCollectionViewSource( CollectionViewSource );
        base.OnDetaching();
    }

    /// <summary>
    /// DataContext 変更時に購読とフィルター再評価を更新する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_DataContextChanged( object sender, DependencyPropertyChangedEventArgs e ) {
        UnsubscribeViewModel( e.OldValue as ITranslationCreationViewModel );
        SubscribeViewModel( e.NewValue as ITranslationCreationViewModel );
        RefreshFilteredDictionaryItems();
    }

    /// <summary>
    /// Closed 時に購読を解除する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void Window_Closed( object? sender, EventArgs e ) => UnsubscribeViewModel( _subscribedViewModel );

    /// <summary>
    /// Filter イベント時に表示対象可否を判定する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void CollectionViewSource_Filter( object sender, FilterEventArgs e ) {
        if(e.Item is not TranslationDictionaryItemRowViewModel row) {
            e.Accepted = false;
            return;
        }

        e.Accepted = GetViewModel()?.ShouldIncludeRow( row ) ?? true;
    }

    /// <summary>
    /// ViewModel の一覧関連変更時にフィルターを再評価する。
    /// </summary>
    /// <param name="sender">イベント送信元。</param>
    /// <param name="e">イベント引数。</param>
    private void ViewModel_PropertyChanged( object? sender, PropertyChangedEventArgs e ) {
        if(e.PropertyName == nameof( ITranslationCreationViewModel.VisibleDictionaryItemsVersion )) {
            RefreshFilteredDictionaryItems();
        }
    }

    /// <summary>
    /// 現在の DataContext から ViewModel を取得する。
    /// </summary>
    /// <returns>関連 ViewModel を返す。</returns>
    private ITranslationCreationViewModel? GetViewModel() => AssociatedObject?.DataContext as ITranslationCreationViewModel;

    /// <summary>
    /// ViewModel の変更通知購読を開始する。
    /// </summary>
    /// <param name="viewModel">購読対象の ViewModel。</param>
    private void SubscribeViewModel( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null || ReferenceEquals( _subscribedViewModel, viewModel )) {
            return;
        }

        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        _subscribedViewModel = viewModel;
    }

    /// <summary>
    /// ViewModel の変更通知購読を解除する。
    /// </summary>
    /// <param name="viewModel">解除対象の ViewModel。</param>
    private void UnsubscribeViewModel( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null || !ReferenceEquals( _subscribedViewModel, viewModel )) {
            return;
        }

        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _subscribedViewModel = null;
    }

    /// <summary>
    /// CollectionViewSource への購読を開始する。
    /// </summary>
    /// <param name="collectionViewSource">購読対象。</param>
    private void AttachCollectionViewSource( CollectionViewSource? collectionViewSource ) {
        if(AssociatedObject is null || collectionViewSource is null) {
            return;
        }

        collectionViewSource.Filter += CollectionViewSource_Filter;
    }

    /// <summary>
    /// CollectionViewSource への購読を解除する。
    /// </summary>
    /// <param name="collectionViewSource">解除対象。</param>
    private void DetachCollectionViewSource( CollectionViewSource? collectionViewSource ) {
        if(collectionViewSource is null) {
            return;
        }

        collectionViewSource.Filter -= CollectionViewSource_Filter;
    }

    /// <summary>
    /// dictionary 一覧のフィルターを再評価する。
    /// </summary>
    private void RefreshFilteredDictionaryItems() => CollectionViewSource?.View?.Refresh();

    /// <summary>
    /// CollectionViewSource 変更時に購読を差し替える。
    /// </summary>
    /// <param name="d">対象オブジェクト。</param>
    /// <param name="e">変更内容。</param>
    private static void OnCollectionViewSourceChanged( DependencyObject d, DependencyPropertyChangedEventArgs e ) {
        var behavior = (TranslationCreationDictionaryFilterBehavior)d;
        behavior.DetachCollectionViewSource( e.OldValue as CollectionViewSource );
        behavior.AttachCollectionViewSource( e.NewValue as CollectionViewSource );
        behavior.RefreshFilteredDictionaryItems();
    }
}