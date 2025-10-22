using DcsTranslationTool.Shared.Models;

namespace DcsTranslationTool.Application.Interfaces;

public interface IFileContentInspector {
    /// <summary>
    /// バイト配列を解析してテキスト/バイナリとエンコードを判定する。
    /// </summary>
    /// <param name="content">解析対象バイト列。</param>
    /// <returns>解析結果。</returns>
    FileContentInfo Inspect( byte[] content );
}