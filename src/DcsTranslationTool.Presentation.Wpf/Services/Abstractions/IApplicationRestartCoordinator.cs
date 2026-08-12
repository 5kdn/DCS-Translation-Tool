namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 更新再起動前の全ウィンドウ終了確認を提供する。
/// </summary>
public interface IApplicationRestartCoordinator {
    /// <summary>
    /// 未保存内容を持ち得る全ウィンドウへ終了可否を確認し、許可された場合だけ閉じる。
    /// </summary>
    /// <param name="cancellationToken">キャンセル通知を受け取る。</param>
    /// <returns>再起動へ進める場合は <see langword="true"/> を返す。</returns>
    Task<bool> PrepareForRestartAsync( CancellationToken cancellationToken = default );
}