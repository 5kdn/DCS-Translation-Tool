using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

/// <summary>
/// <see cref="Features.CreatePullRequest.CreatePullRequestViewModel"/> に渡すパラメーター
/// </summary>
public record CreatePullRequestDialogParameters {

    /// <summary>
    /// PRカテゴリ
    /// </summary>
    public required string Category;

    /// <summary>
    /// PRサブカテゴリー
    /// </summary>
    /// <remarks>
    /// Aircraft: 機体名<br/>
    /// Campaigns: キャンペーン名<br/>
    /// UserMission: ミッション名<br/>
    /// UserCampaigns: キャンペーン: キャンペーン名<br/>
    /// </remarks>
    public required string SubCategory;

    /// <summary>
    /// リポジトリーにPushするアイテムのコレクション
    /// </summary>
    public required IEnumerable<CommitFile> CommitFiles;
}