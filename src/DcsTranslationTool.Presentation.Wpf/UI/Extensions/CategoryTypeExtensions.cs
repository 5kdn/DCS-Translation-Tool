using DcsTranslationTool.Presentation.Wpf.UI.Enums;

namespace DcsTranslationTool.Presentation.Wpf.UI.Extensions;

/// <summary>
/// <see cref="CategoryType"/> の拡張メソッドを提供する
/// </summary>
public static class CategoryTypeExtensions {
    /// <summary>
    /// タブの表示名を取得する
    /// </summary>
    /// <param name="tabType">対象の <see cref="CategoryType"/></param>
    /// <returns>タブの表示名</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 未対応の <see cref="CategoryType"/> が指定された場合にスローされる
    /// </exception>
    public static string GetTabTitle( this CategoryType tabType ) => tabType switch
    {
        CategoryType.Aircraft => "Aircraft",
        CategoryType.DlcCampaigns => "DLC Campaigns",
        CategoryType.UserMissions => "User Missions",
        _ => throw new ArgumentOutOfRangeException(
            nameof( tabType ),
            tabType,
            $"Unexpected CategoryType value: {tabType}" )
    };

    /// <summary>
    /// タブに対応するリポジトリのディレクトリルートを取得する
    /// </summary>
    /// <param name="tabType">対象の <see cref="CategoryType"/></param>
    /// <returns>ディレクトリルートのパス</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 未対応の <see cref="CategoryType"/> が指定された場合にスローされる
    /// </exception>
    public static string[] GetRepoDirRoot( this CategoryType tabType ) => tabType switch
    {
        CategoryType.Aircraft => ["DCSWorld", "Mods", "aircraft"],
        CategoryType.DlcCampaigns => ["DCSWorld", "Mods", "campaigns"],
        CategoryType.UserMissions => ["UserMissions"],
        _ => throw new ArgumentOutOfRangeException(
            nameof( tabType ),
            tabType,
            $"Unexpected CategoryType value: {tabType}" )
    };
}