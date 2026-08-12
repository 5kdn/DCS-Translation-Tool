using System.Windows;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// 現在のアプリケーションウィンドウ一覧を提供する。
/// </summary>
public interface IApplicationWindowProvider {
    /// <summary>
    /// 現在登録されているウィンドウのスナップショットを取得する。
    /// </summary>
    /// <returns>ウィンドウ一覧を返す。</returns>
    IReadOnlyList<Window> GetWindows();
}