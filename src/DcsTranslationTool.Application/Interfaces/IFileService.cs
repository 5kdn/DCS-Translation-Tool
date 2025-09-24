namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// ファイル入出力を定義するサービスのインターフェース。
/// </summary>
public interface IFileService {
    /// <summary>
    /// 指定したパスからデータを読み込む。
    /// </summary>
    /// <typeparam name="T">読み込むデータ型</typeparam>
    /// <param name="folderPath">フォルダーパス</param>
    /// <param name="fileName">ファイル名</param>
    /// <returns>読み込んだデータ。存在しない場合は <see langword="null"/></returns>
    T? Read<T>( string folderPath, string fileName );

    /// <summary>
    /// 指定したパスにデータを保存する。
    /// </summary>
    /// <typeparam name="T">保存するデータ型</typeparam>
    /// <param name="folderPath">フォルダーパス</param>
    /// <param name="fileName">ファイル名</param>
    /// <param name="content">保存する内容</param>
    void Save<T>( string folderPath, string fileName, T content );

    /// <summary>
    /// 指定したファイルを削除する。
    /// </summary>
    /// <param name="folderPath">フォルダーパス</param>
    /// <param name="fileName">ファイル名</param>
    void Delete( string folderPath, string fileName );

    /// <summary>
    /// ファイルに指定した内容を非同期で保存する
    /// </summary>
    /// <param name="path">保存先のファイルパス</param>
    /// <param name="content">保存する内容</param>
    /// <exception cref="ArgumentException">path が null もしくは空の場合</exception>
    /// <exception cref="IOException">ファイルの保存に失敗した場合</exception>
    Task SaveAsync( string path, string content );

    /// <summary>
    /// ファイルに指定した内容を非同期で保存する
    /// </summary>
    /// <param name="path">保存先のファイルパス</param>
    /// <param name="content">保存する内容</param>
    /// <exception cref="ArgumentException">path が null もしくは空の場合</exception>
    /// <exception cref="IOException">ファイルの保存に失敗した場合</exception>
    Task SaveAsync( string path, byte[] content );

}