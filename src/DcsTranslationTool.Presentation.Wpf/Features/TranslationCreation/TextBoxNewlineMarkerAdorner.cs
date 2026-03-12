using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TextBox 上に改行マーカーを重ね描画する Adorner である。
/// </summary>
/// <param name="textBox">対象 TextBox。</param>
internal sealed class TextBoxNewlineMarkerAdorner( TextBox textBox ) : Adorner( textBox ) {
    private static readonly string MarkerText = @"\n";
    private readonly TextBox _textBox = textBox;

    /// <summary>
    /// 文字列中の改行開始位置一覧を取得する。
    /// </summary>
    /// <param name="text">対象文字列。</param>
    /// <returns>改行開始位置一覧。</returns>
    internal static IReadOnlyList<int> GetNewlineMarkerIndices( string text ) {
        if(string.IsNullOrEmpty( text )) {
            return [];
        }

        var indices = new List<int>();
        for(var index = 0; index < text.Length; index++) {
            if(text[index] == '\r') {
                indices.Add( index );
                if(index + 1 < text.Length && text[index + 1] == '\n') {
                    index++;
                }

                continue;
            }

            if(text[index] == '\n') {
                indices.Add( index );
            }
        }

        return indices;
    }

    /// <inheritdoc />
    protected override void OnRender( DrawingContext drawingContext ) {
        base.OnRender( drawingContext );

        if(string.IsNullOrEmpty( _textBox.Text ) || _textBox.ActualWidth <= 0 || _textBox.ActualHeight <= 0) {
            return;
        }

        var markerText = CreateMarkerText();
        var availableWidth = Math.Max( 0, _textBox.ActualWidth - _textBox.Padding.Right - markerText.WidthIncludingTrailingWhitespace - 4 );
        foreach(var newlineIndex in GetNewlineMarkerIndices( _textBox.Text )) {
            var markerOrigin = GetMarkerOrigin( newlineIndex, markerText.WidthIncludingTrailingWhitespace, availableWidth );
            if(markerOrigin is null) {
                continue;
            }

            drawingContext.DrawText( markerText, markerOrigin.Value );
        }
    }

    private FormattedText CreateMarkerText() {
        var foreground = _textBox.Foreground.CloneCurrentValue();
        foreground.Opacity = 0.55;

        return new FormattedText(
            MarkerText,
            CultureInfo.CurrentUICulture,
            _textBox.FlowDirection,
            new Typeface( _textBox.FontFamily, _textBox.FontStyle, _textBox.FontWeight, _textBox.FontStretch ),
            _textBox.FontSize,
            foreground,
            VisualTreeHelper.GetDpi( this ).PixelsPerDip );
    }

    private Point? GetMarkerOrigin( int newlineIndex, double markerWidth, double availableWidth ) {
        var lineIndex = _textBox.GetLineIndexFromCharacterIndex( newlineIndex );
        if(lineIndex < 0) {
            return null;
        }

        var lineStartIndex = _textBox.GetCharacterIndexFromLineIndex( lineIndex );
        var lineStartRect = _textBox.GetRectFromCharacterIndex( lineStartIndex, trailingEdge: false );
        if(lineStartRect.IsEmpty) {
            return null;
        }

        var x = lineStartRect.Left;
        var y = lineStartRect.Top;

        if(newlineIndex > lineStartIndex) {
            var previousCharacterRect = _textBox.GetRectFromCharacterIndex( newlineIndex - 1, trailingEdge: true );
            if(!previousCharacterRect.IsEmpty) {
                x = previousCharacterRect.Right;
                y = previousCharacterRect.Top;
            }
        }

        x = Math.Max( _textBox.Padding.Left, Math.Min( x, availableWidth ) );
        return new Point( x, y );
    }
}