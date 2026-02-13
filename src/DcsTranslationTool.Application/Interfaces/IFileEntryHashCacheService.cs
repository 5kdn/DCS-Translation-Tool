namespace DcsTranslationTool.Application.Interfaces;

/// <summary>
/// ローカルファイルのハッシュキャッシュを永続化して管理する。
/// </summary>
public interface IFileEntryHashCacheService {
    /// <summary>
    /// キャッシュの対象ルートを設定して初期化する。
    /// </summary>
    /// <param name="rootPath">翻訳ルートの絶対パス。</param>
    void ConfigureRoot( string rootPath );

    /// <summary>
    /// 属性一致時にキャッシュ済み SHA1 を取得する。
    /// </summary>
    /// <param name="relativePath">ルートからの相対パス。</param>
    /// <param name="fileSize">ファイルサイズ。</param>
    /// <param name="lastWriteUtc">最終更新日時（UTC）。</param>
    /// <param name="sha">取得した SHA1。</param>
    /// <returns>一致するキャッシュが存在する場合は <see langword="true"/> を返す。</returns>
    bool TryGetSha( string relativePath, long fileSize, DateTime lastWriteUtc, out string? sha );

    /// <summary>
    /// ファイル属性と SHA1 をキャッシュへ保存する。
    /// </summary>
    /// <param name="relativePath">ルートからの相対パス。</param>
    /// <param name="fileSize">ファイルサイズ。</param>
    /// <param name="lastWriteUtc">最終更新日時（UTC）。</param>
    /// <param name="sha">保存する SHA1。</param>
    void Upsert( string relativePath, long fileSize, DateTime lastWriteUtc, string? sha );

    /// <summary>
    /// 指定パスのキャッシュを削除する。
    /// </summary>
    /// <param name="relativePath">ルートからの相対パス。</param>
    void Remove( string relativePath );

    /// <summary>
    /// 現在存在しないファイルのキャッシュを一括削除する。
    /// </summary>
    /// <param name="existingRelativePaths">現在存在する相対パス集合。</param>
    void Prune( IReadOnlySet<string> existingRelativePaths );
}