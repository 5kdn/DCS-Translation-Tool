using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// アプリケーション設定サービスのインターフェース
/// </summary>
public interface IAppSettingsService : IDisposable, IAsyncDisposable {
    /// <summary>
    /// 設定モデルへの参照
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// 設定を保存する非同期操作
    /// </summary>
    /// <param name="cancellationToken">キャンセル要求</param>
    /// <returns>保存完了を示すタスク</returns>
    Task SaveAsync( CancellationToken cancellationToken = default );
}