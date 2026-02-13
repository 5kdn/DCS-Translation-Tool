using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// アプリケーション更新確認を提供するサービス契約とする。
/// </summary>
public interface IUpdateCheckService {
    /// <summary>
    /// 最新リリースを確認して更新可否を返す。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知を受け取る。</param>
    /// <returns>更新確認結果を返す。</returns>
    Task<UpdateCheckResult> CheckForUpdateAsync( CancellationToken cancellationToken = default );
}