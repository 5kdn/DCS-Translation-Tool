namespace DcsTranslationTool.Application.Models;

/// <summary>
/// 更新取得処理の進行段階を表す。
/// </summary>
public enum ApplicationUpdateProgressPhase {
    /// <summary>
    /// 更新アセットを取得している状態を表す。
    /// </summary>
    Downloading,

    /// <summary>
    /// 更新アセットを検証している状態を表す。
    /// </summary>
    Verifying,

    /// <summary>
    /// 更新先を再構築している状態を表す。
    /// </summary>
    Staging
}

/// <summary>
/// 更新取得処理の進捗を表す。
/// </summary>
/// <param name="Phase">現在の処理段階を受け取る。</param>
/// <param name="DownloadedBytes">取得済みバイト数を受け取る。</param>
/// <param name="TotalBytes">取得予定バイト数を受け取る。</param>
public sealed record ApplicationUpdateProgress(
    ApplicationUpdateProgressPhase Phase,
    long DownloadedBytes,
    long TotalBytes
) {
    /// <summary>
    /// 0 から 100 の進捗率を取得する。
    /// </summary>
    public double Percentage => TotalBytes <= 0
        ? 0
        : Math.Clamp( (double)DownloadedBytes / TotalBytes * 100, 0, 100 );
}

/// <summary>
/// 検証済みのステージ済み更新を表す。
/// </summary>
/// <param name="WorkspaceDirectory">更新作業ディレクトリを受け取る。</param>
/// <param name="StagingDirectory">完全な更新先ファイルのステージングディレクトリを受け取る。</param>
/// <param name="TargetVersion">更新先バージョンを受け取る。</param>
/// <param name="RuntimeIdentifier">更新対象 RID を受け取る。</param>
/// <param name="AssetKind">実際に利用したアセット種別を受け取る。</param>
/// <param name="ReleaseUrl">更新元リリース URL を受け取る。</param>
public sealed record ApplicationUpdateStageResult(
    string WorkspaceDirectory,
    string StagingDirectory,
    Version TargetVersion,
    string RuntimeIdentifier,
    ApplicationUpdateAssetKind AssetKind,
    string ReleaseUrl
);