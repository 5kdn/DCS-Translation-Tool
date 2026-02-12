namespace DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

/// <summary>
/// パス正規化とルート外参照防止の判定を提供するサービス契約を表す。
/// </summary>
public interface IPathSafetyGuard {
    /// <summary>
    /// ルート配下に収まる絶対パスへ解決できるか判定する。
    /// </summary>
    /// <param name="rootFullPath">ルートディレクトリ絶対パス。</param>
    /// <param name="rootWithSeparator">末尾区切り文字付きルートパス。</param>
    /// <param name="relativePath">相対パス。</param>
    /// <param name="resolvedPath">解決後の絶対パス。</param>
    /// <returns>解決に成功した場合は <see langword="true"/>。</returns>
    bool TryResolvePathWithinRoot( string rootFullPath, string rootWithSeparator, string relativePath, out string resolvedPath );

    /// <summary>
    /// DCS リポジトリ構造に応じて先頭セグメントのスキップ数を返す。
    /// </summary>
    /// <param name="segments">パスセグメント。</param>
    /// <returns>スキップするセグメント数。</returns>
    int GetRootSegmentSkipCount( string[] segments );

    /// <summary>
    /// ZIP として扱う拡張子を含むセグメントか判定する。
    /// </summary>
    /// <param name="segment">判定対象セグメント。</param>
    /// <returns>ZIP として扱う場合は <see langword="true"/>。</returns>
    bool IsZipLikeEntrySegment( string segment );
}