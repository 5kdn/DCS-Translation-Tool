namespace DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Results;

/// <summary>
/// CreatePullRequestダイアログの完了状態と結果値。
/// </summary>
public record CreatePullRequestResult() {
    public bool IsOk;
    public string? PrUrl;
    public IReadOnlyList<Exception>? Errors;
}