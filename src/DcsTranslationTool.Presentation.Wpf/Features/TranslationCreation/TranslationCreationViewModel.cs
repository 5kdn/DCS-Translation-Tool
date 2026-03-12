using Caliburn.Micro;

using DcsTranslationTool.Resources;

namespace DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

/// <summary>
/// 翻訳作成ウィンドウの状態を管理する ViewModel である。
/// </summary>
/// <param name="archiveFullPath">翻訳対象のアーカイブ絶対パス。</param>
public sealed class TranslationCreationViewModel( string archiveFullPath ) : Screen {
    /// <summary>
    /// ウィンドウの表示名を取得する。
    /// </summary>
    public string WindowTitle { get; } = Strings_Translation.CreateTranslationWindowTitle;

    /// <summary>
    /// 選択中アーカイブの絶対パスを取得する。
    /// </summary>
    public string ArchiveFullPath { get; } = string.IsNullOrWhiteSpace( archiveFullPath )
        ? throw new ArgumentException( "アーカイブ絶対パスは必須です。", nameof( archiveFullPath ) )
        : archiveFullPath;
}