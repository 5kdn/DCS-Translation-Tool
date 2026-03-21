using DcsTranslationTool.Application.Models;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// 翻訳対象アーカイブの探索を提供する。
/// </summary>
public interface ITranslationArchiveDiscoveryService {
    /// <summary>
    /// 設定済みディレクトリから翻訳対象アーカイブを探索する。
    /// </summary>
    /// <param name="dcsWorldInstallDir">DCS World インストールディレクトリ。</param>
    /// <param name="sourceUserMissionDir">ユーザーミッションディレクトリ。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>探索結果の一覧。</returns>
    Task<IReadOnlyList<TranslationArchiveEntry>> DiscoverAsync(
        string? dcsWorldInstallDir,
        string? sourceUserMissionDir,
        CancellationToken cancellationToken = default
    );
}