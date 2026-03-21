using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TextBox 上に問題になりやすい空白文字の背景を重ね描画する Adorner である。
/// </summary>
/// <param name="textBox">対象 TextBox。</param>
internal sealed class TextBoxWhitespaceHighlightAdorner( TextBox textBox ) : Adorner( textBox ) {
    private static readonly SolidColorBrush HighlightBrush = CreateHighlightBrush();
    private readonly TextBox _textBox = textBox;

    /// <summary>
    /// 強調対象となる空白文字位置一覧を取得する。
    /// </summary>
    /// <param name="text">対象文字列。</param>
    /// <returns>強調対象となる空白文字位置一覧。</returns>
    internal static IReadOnlyList<int> GetHighlightIndices( string text ) {
        if(string.IsNullOrEmpty( text )) {
            return [];
        }

        var indices = new SortedSet<int>();
        var runStart = -1;

        for(var index = 0; index < text.Length; index++) {
            if(IsHighlightWhitespace( text[index] )) {
                runStart = runStart < 0 ? index : runStart;
                continue;
            }

            AddWhitespaceRunHighlights( text, runStart, index - 1, indices );
            runStart = -1;
        }

        AddWhitespaceRunHighlights( text, runStart, text.Length - 1, indices );
        return [.. indices];
    }

    /// <inheritdoc />
    protected override void OnRender( DrawingContext drawingContext ) {
        base.OnRender( drawingContext );

        if(string.IsNullOrEmpty( _textBox.Text ) || _textBox.ActualWidth <= 0 || _textBox.ActualHeight <= 0) {
            return;
        }

        var clipRect = GetClipRect();
        drawingContext.PushClip( new RectangleGeometry( clipRect ) );
        try {
            foreach(var highlightIndex in GetHighlightIndices( _textBox.Text )) {
                var characterRect = GetCharacterRect( highlightIndex );
                if(characterRect is null) {
                    continue;
                }

                drawingContext.DrawRectangle( HighlightBrush, null, characterRect.Value );
            }
        }
        finally {
            drawingContext.Pop();
        }
    }

    private static void AddWhitespaceRunHighlights( string text, int runStart, int runEnd, SortedSet<int> indices ) {
        if(runStart < 0 || runEnd < runStart) {
            return;
        }

        var runLength = runEnd - runStart + 1;
        var nextIndex = runEnd + 1;
        var isTrailingWhitespace = runEnd == text.Length - 1;
        var isBeforeNewline = nextIndex < text.Length && text[nextIndex] is '\r' or '\n';
        if(runLength < 2 && !isTrailingWhitespace && !isBeforeNewline) {
            return;
        }

        for(var index = runStart; index <= runEnd; index++) {
            indices.Add( index );
        }
    }

    private static bool IsHighlightWhitespace( char character ) =>
        character is ' ' or '\t' or '\u3000';

    private static SolidColorBrush CreateHighlightBrush() {
        var brush = new SolidColorBrush( Color.FromArgb( 112, 255, 193, 7 ) );
        brush.Freeze();
        return brush;
    }

    private Rect? GetCharacterRect( int characterIndex ) {
        var leadingRect = _textBox.GetRectFromCharacterIndex( characterIndex, trailingEdge: false );
        if(leadingRect.IsEmpty) {
            return null;
        }

        var trailingRect = _textBox.GetRectFromCharacterIndex( characterIndex, trailingEdge: true );
        var right = !trailingRect.IsEmpty && trailingRect.Right > leadingRect.Left
            ? trailingRect.Right
            : leadingRect.Right;
        var width = right - leadingRect.Left;
        if(width <= 0) {
            width = Math.Max( 1, leadingRect.Width );
        }

        return new Rect( leadingRect.Left, leadingRect.Top, width, leadingRect.Height );
    }

    private Rect GetClipRect() {
        var left = Math.Max( 0, _textBox.Padding.Left );
        var top = Math.Max( 0, _textBox.Padding.Top );
        var right = Math.Max( left, _textBox.ActualWidth - _textBox.Padding.Right );
        var bottom = Math.Max( top, _textBox.ActualHeight - _textBox.Padding.Bottom );
        return new Rect( new Point( left, top ), new Point( right, bottom ) );
    }
}