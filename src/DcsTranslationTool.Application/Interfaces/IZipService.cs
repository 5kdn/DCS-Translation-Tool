using FluentResults;

namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// zip ファイル操作を提供するサービスインターフェース。
/// </summary>
public interface IZipService {
    /// <summary>
    /// zip のエントリ一覧を取得する。
    /// </summary>
    /// <param name="zipFilePath">対象の zip ファイルパス</param>
    /// <returns>エントリパス一覧を含む <see cref="Result"/>。失敗時はエラー情報を含む</returns>
    Result<IReadOnlyList<string>> GetEntries( string zipFilePath );

    /// <summary>
    /// ファイルを zip に追加する。
    /// </summary>
    /// <param name="zipFilePath">対象の zip ファイルパス</param>
    /// <param name="entryPath">zip 内でのパス</param>
    /// <param name="filePath">追加するファイルパス</param>
    /// <returns>操作結果（成功／失敗とエラー情報）を含む<see cref="Result"/></returns>
    Result AddEntry( string zipFilePath, string entryPath, string filePath );

    /// <summary>
    /// バイト列を zip に追加する。
    /// </summary>
    /// <param name="zipFilePath">対象の zip ファイルパス</param>
    /// <param name="entryPath">zip 内でのパス</param>
    /// <param name="data">追加する byte[] データ</param>
    /// <returns>操作結果（成功／失敗とエラー情報）を含む<see cref="Result"/></returns>
    Result AddEntry( string zipFilePath, string entryPath, byte[] data );

    /// <summary>
    /// zip からエントリを削除する。
    /// </summary>
    /// <param name="zipFilePath">対象の zip ファイルパス</param>
    /// <param name="targetPath">削除するファイルまたはディレクトリパス</param>
    /// <returns>操作結果（成功／失敗とエラー情報）を含む</returns>
    Result DeleteEntry( string zipFilePath, string targetPath );
}