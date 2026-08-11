using DcsTranslationTool.Application.Models;

using FluentResults;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// アプリケーション更新の取得、検証、適用開始を提供する。
/// </summary>
public interface IApplicationUpdateService {
    /// <summary>
    /// 更新アセットを取得して完全な更新先をステージングする。
    /// </summary>
    /// <param name="update">更新確認結果を受け取る。</param>
    /// <param name="progress">進捗通知を受け取る。</param>
    /// <param name="cancellationToken">キャンセル通知を受け取る。</param>
    /// <returns>ステージ済み更新を含む結果を返す。</returns>
    Task<Result<ApplicationUpdateStageResult>> DownloadAndStageAsync(
        UpdateCheckResult update,
        IProgress<ApplicationUpdateProgress>? progress = null,
        CancellationToken cancellationToken = default );

    /// <summary>
    /// 再起動待ちのステージ済み更新を取得する。
    /// </summary>
    /// <returns>保留中の更新を含む結果を返す。</returns>
    Result<ApplicationUpdateStageResult?> GetPendingUpdate();

    /// <summary>
    /// ステージ済み更新の作業領域を破棄する。
    /// </summary>
    /// <param name="stageResult">破棄するステージ済み更新を受け取る。</param>
    /// <returns>作業領域を破棄できた場合に成功を返す。</returns>
    Result DiscardStagedUpdate( ApplicationUpdateStageResult stageResult );

    /// <summary>
    /// 保留中ではない完了済み更新の作業領域を安全に削除する。
    /// </summary>
    /// <returns>クリーンアップ処理の結果を返す。</returns>
    Result CleanupCompletedUpdates();

    /// <summary>
    /// 現在の実行環境を更新専用プロセスとして複製し、適用開始を要求する。
    /// </summary>
    /// <param name="stageResult">適用するステージ済み更新を受け取る。</param>
    /// <param name="parentProcessId">終了を待機する現在プロセス ID を受け取る。</param>
    /// <returns>更新専用プロセスを開始できた場合に成功を返す。</returns>
    Result LaunchApplyProcess( ApplicationUpdateStageResult stageResult, int parentProcessId );

    /// <summary>
    /// 更新専用モードでステージ済み更新を置換し、失敗時にロールバックする。
    /// </summary>
    /// <param name="requestPath">適用要求 JSON の絶対パスを受け取る。</param>
    /// <param name="cancellationToken">キャンセル通知を受け取る。</param>
    /// <returns>適用結果を返す。</returns>
    Task<Result> ApplyPreparedUpdateAsync( string requestPath, CancellationToken cancellationToken = default );
}