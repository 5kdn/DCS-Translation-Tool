using System.Windows;

using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// WPF アプリケーションから現在のウィンドウ一覧を取得する。
/// </summary>
public sealed class ApplicationWindowProvider : IApplicationWindowProvider {
    /// <inheritdoc />
    public IReadOnlyList<Window> GetWindows() =>
        System.Windows.Application.Current?.Windows.Cast<Window>().ToArray() ?? [];
}