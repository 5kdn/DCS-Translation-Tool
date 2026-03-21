using System.Collections;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation の dictionary キーを自然順で比較する comparer である。
/// </summary>
internal sealed class TranslationCreationNaturalKeyComparer : IComparer<string>, IComparer {
    /// <summary>
    /// 既定インスタンスを取得する。
    /// </summary>
    internal static TranslationCreationNaturalKeyComparer Instance { get; } = new();

    /// <inheritdoc />
    public int Compare( string? x, string? y ) {
        if(ReferenceEquals( x, y )) {
            return 0;
        }

        if(x is null) {
            return -1;
        }

        if(y is null) {
            return 1;
        }

        var leftIndex = 0;
        var rightIndex = 0;

        while(leftIndex < x.Length && rightIndex < y.Length) {
            var leftIsDigit = char.IsDigit( x[leftIndex] );
            var rightIsDigit = char.IsDigit( y[rightIndex] );

            if(leftIsDigit && rightIsDigit) {
                var leftDigitEnd = ConsumeDigits( x, leftIndex );
                var rightDigitEnd = ConsumeDigits( y, rightIndex );
                var digitComparison = CompareNumericTokens(
                    x.AsSpan( leftIndex, leftDigitEnd - leftIndex ),
                    y.AsSpan( rightIndex, rightDigitEnd - rightIndex ) );
                if(digitComparison != 0) {
                    return digitComparison;
                }

                leftIndex = leftDigitEnd;
                rightIndex = rightDigitEnd;
                continue;
            }

            if(x[leftIndex] != y[rightIndex]) {
                return x[leftIndex].CompareTo( y[rightIndex] );
            }

            leftIndex++;
            rightIndex++;
        }

        if(leftIndex < x.Length) {
            return 1;
        }

        if(rightIndex < y.Length) {
            return -1;
        }

        return string.CompareOrdinal( x, y );
    }

    /// <inheritdoc />
    int IComparer.Compare( object? x, object? y ) => Compare( x as string, y as string );

    private static int ConsumeDigits( string value, int startIndex ) {
        var index = startIndex;
        while(index < value.Length && char.IsDigit( value[index] )) {
            index++;
        }

        return index;
    }

    private static int CompareNumericTokens( ReadOnlySpan<char> left, ReadOnlySpan<char> right ) {
        var leftTrimmed = TrimLeadingZeros( left );
        var rightTrimmed = TrimLeadingZeros( right );

        if(leftTrimmed.Length != rightTrimmed.Length) {
            return leftTrimmed.Length.CompareTo( rightTrimmed.Length );
        }

        var valueComparison = leftTrimmed.CompareTo( rightTrimmed, StringComparison.Ordinal );
        if(valueComparison != 0) {
            return valueComparison;
        }

        return left.Length.CompareTo( right.Length );
    }

    private static ReadOnlySpan<char> TrimLeadingZeros( ReadOnlySpan<char> value ) {
        var index = 0;
        while(index < value.Length - 1 && value[index] == '0') {
            index++;
        }

        return value[index..];
    }
}