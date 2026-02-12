namespace DcsTranslationTool.Application.Results;

/// <summary>
/// Result の失敗分類を表す。
/// </summary>
public enum ResultErrorKind {
    /// <summary>
    /// 入力値検証に失敗したことを示す。
    /// </summary>
    Validation,

    /// <summary>
    /// 対象が見つからないことを示す。
    /// </summary>
    NotFound,

    /// <summary>
    /// 競合状態で処理できないことを示す。
    /// </summary>
    Conflict,

    /// <summary>
    /// 外部依存先の失敗を示す。
    /// </summary>
    External,

    /// <summary>
    /// 予期しない失敗を示す。
    /// </summary>
    Unexpected
}