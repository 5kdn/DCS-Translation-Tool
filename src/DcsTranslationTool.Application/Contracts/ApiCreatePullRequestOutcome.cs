namespace DcsTranslationTool.Application.Contracts;

/// <summary>Pull Request作成処理の結果を表現する</summary>
/// <param name="Success">APIが成功を報告したかどうかを示す</param>
/// <param name="Message">APIが返却したメッセージを保持する</param>
/// <param name="Entries">APIが返却した詳細情報の一覧を保持する</param>
public sealed record ApiCreatePullRequestOutcome( bool Success, string? Message, IReadOnlyList<ApiCreatePullRequestEntry> Entries );