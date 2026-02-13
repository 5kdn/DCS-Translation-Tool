namespace DcsTranslationTool.Shared.Constants;

/// <summary>
/// アプリ本体リポジトリに関する固定情報を保持する。
/// </summary>
public static class ApplicationRepository {
    private const string _owner = "5kdn";
    private const string _repo = "DCS-Translation-Tool";

    /// <summary>
    /// GitHub リポジトリの所有者名を返す。
    /// </summary>
    public static string Owner => _owner;

    /// <summary>
    /// GitHub リポジトリ名を返す。
    /// </summary>
    public static string Repo => _repo;

    /// <summary>
    /// GitHub リポジトリ URL を返す。
    /// </summary>
    public static string Url => $"https://github.com/{Owner}/{Repo}";
}