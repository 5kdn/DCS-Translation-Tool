using DcsTranslationTool.Presentation.Wpf.Features.TranslationCreation;

namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// TranslationCreationViewModel の生成機能を提供するファクトリである。
/// </summary>
public interface ITranslationCreationViewModelFactory {
    /// <summary>
    /// TranslationCreationViewModel を生成する。
    /// </summary>
    /// <param name="archiveFullPath">翻訳対象アーカイブの絶対パス。</param>
    /// <returns>生成した ViewModel。</returns>
    ITranslationCreationViewModel Create( string archiveFullPath );
}