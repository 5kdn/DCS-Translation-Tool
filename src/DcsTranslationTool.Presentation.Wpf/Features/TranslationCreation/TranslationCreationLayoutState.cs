namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// TranslationCreation のレイアウト既定値と正規化規則を表す。
/// </summary>
public static class TranslationCreationLayoutDefaults {
    /// <summary>
    /// 既定のウィンドウ幅を表す。
    /// </summary>
    public const double DefaultWindowWidth = 900;

    /// <summary>
    /// 既定のウィンドウ高さを表す。
    /// </summary>
    public const double DefaultWindowHeight = 760;

    /// <summary>
    /// ウィンドウ幅の最小値を表す。
    /// </summary>
    public const double MinWindowWidth = 900;

    /// <summary>
    /// ウィンドウ高さの最小値を表す。
    /// </summary>
    public const double MinWindowHeight = 760;

    /// <summary>
    /// dictionary 領域比率の既定値を表す。
    /// </summary>
    public const double DefaultDictionaryPaneRatio = 2;

    /// <summary>
    /// dictionary 領域比率の最小値を表す。
    /// </summary>
    public const double MinDictionaryPaneRatio = 0.2;

    /// <summary>
    /// dictionary 領域比率の最大値を表す。
    /// </summary>
    public const double MaxDictionaryPaneRatio = 8;

    /// <summary>
    /// dictionary 領域比率を有効範囲へ正規化する。
    /// </summary>
    /// <param name="ratio">検証対象の比率。</param>
    /// <returns>有効範囲内へ補正した比率を返す。</returns>
    public static double NormalizeDictionaryPaneRatio( double ratio ) {
        if(double.IsNaN( ratio ) || double.IsInfinity( ratio ) || ratio <= 0) {
            return DefaultDictionaryPaneRatio;
        }

        return Math.Clamp( ratio, MinDictionaryPaneRatio, MaxDictionaryPaneRatio );
    }

    /// <summary>
    /// ウィンドウサイズを有効範囲へ正規化する。
    /// </summary>
    /// <param name="value">検証対象のサイズ。</param>
    /// <param name="fallback">不正値時の既定サイズ。</param>
    /// <param name="minimum">許容する最小サイズ。</param>
    /// <returns>有効範囲内へ補正したサイズを返す。</returns>
    public static double NormalizeWindowLength( double value, double fallback, double minimum ) {
        if(double.IsNaN( value ) || double.IsInfinity( value ) || value <= 0) {
            return fallback;
        }

        return Math.Max( minimum, value );
    }
}

/// <summary>
/// TranslationCreation のレイアウト状態を表す。
/// </summary>
/// <param name="WindowWidth">ウィンドウ幅。</param>
/// <param name="WindowHeight">ウィンドウ高さ。</param>
/// <param name="DictionaryPaneRatio">dictionary 領域比率。</param>
/// <param name="IsDictionaryDetailsWrapEnabled">dictionary 詳細テキストの折り返し有無。</param>
public sealed record TranslationCreationLayoutState(
    double WindowWidth,
    double WindowHeight,
    double DictionaryPaneRatio,
    bool IsDictionaryDetailsWrapEnabled );