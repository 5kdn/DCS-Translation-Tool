using DcsTranslationTool.Shared.Models;

using FluentResults;

namespace DcsTranslationTool.Application.Interfaces;
/// <summary>
/// ローカルディレクトリのファイル列挙と監視を行うサービスのインターフェース。
/// </summary>
public interface IFileEntryService : IDisposable {
    /// <summary>
    /// 指定ディレクトリ以下の全てのファイルを再帰的に取得する。
    /// <para>
    /// ファイルのみを対象とし、ディレクトリは対象としない。
    /// </para>
    /// </summary>
    /// <param name="path">探索するルートパス</param>
    /// <returns>フラットな <see cref="FileEntry"/> のコレクション</returns>
    Task<Result<IEnumerable<FileEntry>>> GetChildrenRecursiveAsync( string path );

    /// <summary>
    /// 指定パスの監視を開始するメソッド。
    /// </summary>
    /// <param name="path">監視対象のルートパス</param>
    void Watch( string path );

    /// <summary>
    /// 監視対象のファイルエントリを取得する非同期メソッド。
    /// </summary>
    /// <returns>取得したファイルエントリのコレクション</returns>
    Task<Result<IReadOnlyList<FileEntry>>> GetEntriesAsync();

    /// <summary>
    /// 監視対象のファイルエントリが変化したときに発火するイベント。
    /// </summary>
    event Func<IReadOnlyList<FileEntry>, Task> EntriesChanged;
}