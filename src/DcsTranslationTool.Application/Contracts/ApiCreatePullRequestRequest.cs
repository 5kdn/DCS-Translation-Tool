namespace DcsTranslationTool.Application.Contracts;

/// <summary>Pull Request作成要求を表現する</summary>
/// <param name="BranchName">作成するブランチ名を指定する</param>
/// <param name="CommitMessage">コミットメッセージを指定する</param>
/// <param name="PrTitle">Pull Requestのタイトルを指定する</param>
/// <param name="PrBody">Pull Requestの本文を指定する</param>
/// <param name="Files">ファイル操作の一覧を指定する</param>
public sealed record ApiCreatePullRequestRequest(
    string BranchName,
    string CommitMessage,
    string PrTitle,
    string PrBody,
    IReadOnlyList<ApiPullRequestFile> Files
);