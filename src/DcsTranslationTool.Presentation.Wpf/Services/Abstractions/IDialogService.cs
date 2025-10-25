using DcsTranslationTool.Presentation.Wpf.UI.Dialogs.Parameters;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 汎用的なダイアログ表示を提供するサービスを表現する。
/// </summary>
public interface IDialogService {
    /// <summary>
    /// ダイアログを表示して結果を取得する。
    /// </summary>
    /// <param name="parameters">表示に利用する引数。</param>
    /// <returns>承認された場合は <see langword="true"/>。</returns>
    Task<bool> ShowAsync( ConfirmationDialogParameters parameters );
}