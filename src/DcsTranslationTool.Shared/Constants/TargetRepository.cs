namespace DcsTranslationTool.Shared.Constants;
/// <summary>
/// GitHub リポジトリに関する固定情報を保持するクラス。
/// <para>
/// このクラスは DcsTranslationTool が利用する GitHub リポジトリのオーナー名、リポジトリ名を提供する。
/// </para>
/// </summary>
public static class TargetRepository {
    private const string _owner = "5kdn";
    private const string _repo = "DCS-Translation-Japanese";
    private const string _distributionControlPolicyUrl = "https://github.com/5kdn/DCS-Translation-Japanese/blob/master/DISTRIBUTION_POLICY.md";

    /// <summary>
    /// GitHub リポジトリの所有者名。
    /// </summary>
    /// <returns>
    /// リポジトリの所有者。
    /// </returns>
    public static string Owner => _owner;

    /// <summary>
    /// GitHub リポジトリ名。
    /// </summary>
    /// <returns>
    /// リポジトリ名。
    /// </returns>
    public static string Repo => _repo;

    /// <summary>
    /// 流通ポリシーのURL
    /// </summary>
    public static string DistributionControlPolicyUrl => _distributionControlPolicyUrl;

    public static string DefaultBranch => "master";

    public static string Url => $"https://github.com/{Owner}/{Repo}";
}