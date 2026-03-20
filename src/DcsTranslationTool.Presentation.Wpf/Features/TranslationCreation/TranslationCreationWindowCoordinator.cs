using System.Windows;
using System.Windows.Controls;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreationView のウィンドウ状態同期を担う。
/// </summary>
/// <param name="window">対象ウィンドウ。</param>
/// <param name="dictionaryDataGridRowDefinition">dictionary 一覧行。</param>
/// <param name="dictionaryDetailsRowDefinition">dictionary 詳細行。</param>
/// <param name="selectedOriginalTextBox">Original 表示テキストボックス。</param>
/// <param name="selectedTranslatedTextBox">Translated 編集テキストボックス。</param>
internal sealed class TranslationCreationWindowCoordinator(
    Window window,
    RowDefinition dictionaryDataGridRowDefinition,
    RowDefinition dictionaryDetailsRowDefinition,
    TextBox? selectedOriginalTextBox,
    TextBox? selectedTranslatedTextBox ) {
    private const double DetailPaneBaseRatio = 1;

    /// <summary>
    /// ViewModel の状態をウィンドウへ反映する。
    /// </summary>
    /// <param name="viewModel">反映元 ViewModel。</param>
    internal void ApplyLayout( ITranslationCreationViewModel? viewModel ) {
        ApplyMinimumWindowSize();
        ApplyWindowSize( viewModel );
        ApplyDictionaryPaneRatio( viewModel );
        ApplyDictionaryDetailsTextWrapping( viewModel );
    }

    /// <summary>
    /// ウィンドウの最小サイズを反映する。
    /// </summary>
    internal void ApplyMinimumWindowSize() {
        window.MinWidth = TranslationCreationViewModel.MinWindowWidth;
        window.MinHeight = TranslationCreationViewModel.MinWindowHeight;
    }

    /// <summary>
    /// 現在のレイアウトを ViewModel へ保存する。
    /// </summary>
    /// <param name="viewModel">保存先 ViewModel。</param>
    internal void PersistLayout( ITranslationCreationViewModel? viewModel ) {
        if(viewModel is null) {
            return;
        }

        PersistWindowSize( viewModel );
        PersistDictionaryPaneRatio( viewModel );
    }

    /// <summary>
    /// 折り返し状態をテキストボックスへ反映する。
    /// </summary>
    /// <param name="viewModel">反映元 ViewModel。</param>
    internal void ApplyDictionaryDetailsTextWrapping( ITranslationCreationViewModel? viewModel ) {
        var textWrapping = viewModel?.IsDictionaryDetailsWrapEnabled != false
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;

        if(selectedOriginalTextBox is not null) {
            selectedOriginalTextBox.TextWrapping = textWrapping;
        }

        if(selectedTranslatedTextBox is not null) {
            selectedTranslatedTextBox.TextWrapping = textWrapping;
        }
    }

    private void ApplyWindowSize( ITranslationCreationViewModel? viewModel ) {
        var width = viewModel?.WindowWidth ?? TranslationCreationViewModel.DefaultWindowWidth;
        var height = viewModel?.WindowHeight ?? TranslationCreationViewModel.DefaultWindowHeight;

        window.Width = Math.Max( width, window.MinWidth );
        window.Height = Math.Max( height, window.MinHeight );
    }

    private void ApplyDictionaryPaneRatio( ITranslationCreationViewModel? viewModel ) {
        var ratio = viewModel?.DictionaryPaneRatio ?? TranslationCreationViewModel.DefaultDictionaryPaneRatio;
        dictionaryDataGridRowDefinition.Height = new GridLength( ratio, GridUnitType.Star );
        dictionaryDetailsRowDefinition.Height = new GridLength( DetailPaneBaseRatio, GridUnitType.Star );
    }

    private void PersistWindowSize( ITranslationCreationViewModel viewModel ) {
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect( window.Left, window.Top, window.Width, window.Height )
            : window.RestoreBounds;

        viewModel.WindowWidth = bounds.Width;
        viewModel.WindowHeight = bounds.Height;
    }

    private void PersistDictionaryPaneRatio( ITranslationCreationViewModel viewModel ) =>
        viewModel.DictionaryPaneRatio = CalculateDictionaryPaneRatio();

    private double CalculateDictionaryPaneRatio() {
        if(dictionaryDataGridRowDefinition.Height.IsStar && dictionaryDetailsRowDefinition.Height.IsStar && dictionaryDetailsRowDefinition.Height.Value > 0) {
            var starRatio = dictionaryDataGridRowDefinition.Height.Value / dictionaryDetailsRowDefinition.Height.Value;
            return TranslationCreationViewModel.NormalizeDictionaryPaneRatio( starRatio );
        }

        if(dictionaryDetailsRowDefinition.ActualHeight <= 0 || double.IsNaN( dictionaryDetailsRowDefinition.ActualHeight )) {
            return TranslationCreationViewModel.DefaultDictionaryPaneRatio;
        }

        var ratio = dictionaryDataGridRowDefinition.ActualHeight / dictionaryDetailsRowDefinition.ActualHeight;
        return TranslationCreationViewModel.NormalizeDictionaryPaneRatio( ratio );
    }
}