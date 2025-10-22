using DcsTranslationTool.Presentation.Wpf.UI.Enums;

namespace DcsTranslationTool.Presentation.Wpf.UI.Extensions;

public static class PullRequestChangeKindExtensions {
    /// <summary>
    /// ブランチ名に使用する変更種別を取得する。
    /// </summary>
    /// <param name="kind">対象の <see cref="Enums.PullRequestChangeKind"/></param>
    /// <returns></returns>
    public static string GetBranchString( this PullRequestChangeKind kind ) => kind switch
    {
        PullRequestChangeKind.ファイルの追加 => "AddFile",
        PullRequestChangeKind.ファイルの削除 => "DeleteFile",
        PullRequestChangeKind.バグ修正 => "FixBug",
        PullRequestChangeKind.誤字の修正 => "FixTypo",
        PullRequestChangeKind.その他の修正 => "OtherFixes",
        _ => throw new ArgumentOutOfRangeException(
            nameof( kind ),
            kind,
            $"Unexpected CategoryType value: {kind}" )
    };
}