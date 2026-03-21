using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

using Microsoft.Xaml.Behaviors;

namespace DcsTranslationTool.Presentation.Wpf.UnitTests.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView 用 Behavior の軽量ロジックを検証する。
/// </summary>
public sealed class TranslationCreationViewBehaviorsTests {
    [StaFact]
    public void DictionaryFilterBehaviorはVisibleDictionaryItemsVersion変更で一覧を再評価する() {
        var firstRow = CreateRow( "first" );
        var secondRow = CreateRow( "second" );
        var collectionViewSource = new CollectionViewSource { Source = new[] { firstRow, secondRow } };
        var viewModel = new TestTranslationCreationViewModel
        {
            ShouldIncludeRowPredicate = row => row.Key == "first"
        };
        var window = new Window
        {
            DataContext = viewModel
        };
        var behavior = new TranslationCreationDictionaryFilterBehavior
        {
            CollectionViewSource = collectionViewSource
        };

        Interaction.GetBehaviors( window ).Add( behavior );

        Assert.Single( collectionViewSource.View.Cast<object>() );

        viewModel.ShouldIncludeRowPredicate = static _ => true;
        viewModel.RaiseVisibleDictionaryItemsVersionChanged();

        Assert.Equal( 2, collectionViewSource.View.Cast<object>().Count() );
    }

    [StaFact]
    public void LayoutBehaviorはLoadedでレイアウトを反映しDragCompletedで比率を保存する() {
        var rowDefinition1 = new RowDefinition();
        var rowDefinition2 = new RowDefinition();
        var gridSplitter = new GridSplitter();
        var viewModel = new TestTranslationCreationViewModel
        {
            WindowWidth = 1440,
            WindowHeight = 960,
            DictionaryPaneRatio = 2.5
        };
        var window = new Window
        {
            DataContext = viewModel
        };
        var behavior = new TranslationCreationLayoutBehavior
        {
            DictionaryDataGridRowDefinition = rowDefinition1,
            DictionaryDetailsRowDefinition = rowDefinition2,
            DictionaryPaneGridSplitter = gridSplitter
        };

        Interaction.GetBehaviors( window ).Add( behavior );
        window.RaiseEvent( new RoutedEventArgs( FrameworkElement.LoadedEvent ) );

        Assert.Equal( 1440, window.Width );
        Assert.Equal( 960, window.Height );
        Assert.Equal( 2.5, rowDefinition1.Height.Value );
        Assert.Equal( 1, rowDefinition2.Height.Value );

        rowDefinition1.Height = new GridLength( 3, GridUnitType.Star );
        rowDefinition2.Height = new GridLength( 1, GridUnitType.Star );
        gridSplitter.RaiseEvent( new DragCompletedEventArgs( 0, 0, false ) { RoutedEvent = Thumb.DragCompletedEvent } );

        Assert.Equal( 3, viewModel.DictionaryPaneRatio );
    }

    [StaFact]
    public void WindowChromeBehaviorはボタン操作でWindowStateとCloseを反映する() {
        var titleBar = new Border();
        var minimizeButton = new Button();
        var toggleMaximizeButton = new Button();
        var closeButton = new Button();
        var window = new TestWindow();
        var behavior = new TranslationCreationWindowChromeBehavior
        {
            TitleBar = titleBar,
            MinimizeButton = minimizeButton,
            ToggleMaximizeButton = toggleMaximizeButton,
            CloseButton = closeButton
        };

        Interaction.GetBehaviors( window ).Add( behavior );

        minimizeButton.RaiseEvent( new RoutedEventArgs( ButtonBase.ClickEvent ) );
        Assert.Equal( WindowState.Minimized, window.WindowState );

        toggleMaximizeButton.RaiseEvent( new RoutedEventArgs( ButtonBase.ClickEvent ) );
        Assert.Equal( WindowState.Maximized, window.WindowState );

        toggleMaximizeButton.RaiseEvent( new RoutedEventArgs( ButtonBase.ClickEvent ) );
        Assert.Equal( WindowState.Normal, window.WindowState );

        closeButton.RaiseEvent( new RoutedEventArgs( ButtonBase.ClickEvent ) );
        Assert.True( window.WasClosed );
    }

    [StaFact]
    public void LifecycleBehaviorは起動時Close要求で確認なしにWindowを閉じる() {
        var viewModel = new TestTranslationCreationViewModel
        {
            ConfirmCloseAsyncFunc = () => throw new InvalidOperationException( "should not confirm" )
        };
        var window = new TestWindow
        {
            DataContext = viewModel
        };
        var behavior = new TranslationCreationLifecycleBehavior();

        Interaction.GetBehaviors( window ).Add( behavior );
        viewModel.RequestStartupClose();

        Assert.True( window.WasClosed );
        Assert.False( viewModel.ShouldCloseAfterStartup );
    }

    /// <summary>
    /// テスト用の dictionary 行を生成する。
    /// </summary>
    /// <param name="key">キー。</param>
    /// <returns>生成した行 ViewModel を返す。</returns>
    private static TranslationDictionaryItemRowViewModel CreateRow( string key ) =>
        new(
            new TranslationDictionaryItem( key, $"{key}-original" )
            {
                Translated = string.Empty,
                IsEnabled = true
            } );

    /// <summary>
    /// TranslationCreationView 用のテストダブルを表す。
    /// </summary>
    private sealed class TestTranslationCreationViewModel : ITranslationCreationViewModel {
        /// <summary>
        /// 変更通知イベントを表す。
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// ウィンドウ幅を取得または設定する。
        /// </summary>
        public double WindowWidth { get; set; }

        /// <summary>
        /// ウィンドウ高さを取得または設定する。
        /// </summary>
        public double WindowHeight { get; set; }

        /// <summary>
        /// dictionary 領域比率を取得または設定する。
        /// </summary>
        public double DictionaryPaneRatio { get; set; }

        /// <summary>
        /// dictionary 詳細折り返し設定を取得または設定する。
        /// </summary>
        public bool IsDictionaryDetailsWrapEnabled { get; set; }

        /// <summary>
        /// 可視行版数を取得する。
        /// </summary>
        public int VisibleDictionaryItemsVersion { get; private set; }

        /// <summary>
        /// 起動時クローズ要求があるかどうかを取得する。
        /// </summary>
        public bool ShouldCloseAfterStartup { get; private set; }

        /// <summary>
        /// 選択中行を取得する。
        /// </summary>
        public TranslationDictionaryItemRowViewModel? SelectedDictionaryItem { get; init; }

        /// <summary>
        /// 可視判定述語を取得または設定する。
        /// </summary>
        public Func<TranslationDictionaryItemRowViewModel, bool> ShouldIncludeRowPredicate { get; set; } = static _ => true;

        /// <summary>
        /// クローズ確認処理を取得または設定する。
        /// </summary>
        public Func<Task<bool>> ConfirmCloseAsyncFunc { get; set; } = () => Task.FromResult( true );

        /// <summary>
        /// 表示対象可否を判定する。
        /// </summary>
        /// <param name="row">判定対象の行。</param>
        /// <returns>表示対象に含める場合は <see langword="true"/> を返す。</returns>
        public bool ShouldIncludeRow( TranslationDictionaryItemRowViewModel row ) => ShouldIncludeRowPredicate( row );

        /// <summary>
        /// 選択を上へ移動する。
        /// </summary>
        /// <returns>常に <see langword="false"/> を返す。</returns>
        public bool MoveSelectionUp() => false;

        /// <summary>
        /// 選択を下へ移動する。
        /// </summary>
        /// <returns>常に <see langword="false"/> を返す。</returns>
        public bool MoveSelectionDown() => false;

        /// <summary>
        /// 表示後初期化を実行する。
        /// </summary>
        /// <param name="cancellationToken">キャンセルトークン。</param>
        /// <returns>完了済みタスクを返す。</returns>
        public Task HandleWindowLoadedAsync( CancellationToken cancellationToken = default ) => Task.CompletedTask;

        /// <summary>
        /// クローズ可否を確認する。
        /// </summary>
        /// <returns>常に <see langword="true"/> を返す。</returns>
        public Task<bool> ConfirmCloseAsync() => ConfirmCloseAsyncFunc();

        /// <summary>
        /// 起動時クローズ要求を消費する。
        /// </summary>
        public void AcknowledgeStartupCloseRequest() {
            ShouldCloseAfterStartup = false;
            PropertyChanged?.Invoke( this, new( nameof( ShouldCloseAfterStartup ) ) );
        }

        /// <summary>
        /// 可視行版数変更通知を発火する。
        /// </summary>
        public void RaiseVisibleDictionaryItemsVersionChanged() {
            VisibleDictionaryItemsVersion++;
            PropertyChanged?.Invoke( this, new( nameof( VisibleDictionaryItemsVersion ) ) );
        }

        /// <summary>
        /// 起動時クローズ要求を発火する。
        /// </summary>
        public void RequestStartupClose() {
            ShouldCloseAfterStartup = true;
            PropertyChanged?.Invoke( this, new( nameof( ShouldCloseAfterStartup ) ) );
        }
    }

    /// <summary>
    /// Close 呼び出し結果を観測できるテスト用 Window を表す。
    /// </summary>
    private sealed class TestWindow : Window {
        /// <summary>
        /// Closed が発火したかどうかを取得する。
        /// </summary>
        public bool WasClosed { get; private set; }

        /// <summary>
        /// Closed 発火時に状態を記録する。
        /// </summary>
        /// <param name="e">イベント引数。</param>
        protected override void OnClosed( EventArgs e ) {
            WasClosed = true;
            base.OnClosed( e );
        }
    }
}