using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// 翻訳対象アーカイブの探索を提供する。
/// </summary>
public class TranslationArchiveDiscoveryService(
    ILoggingService logger
) : ITranslationArchiveDiscoveryService {
    /// <inheritdoc />
    public Task<IReadOnlyList<TranslationArchiveEntry>> DiscoverAsync(
        string? dcsWorldInstallDir,
        string? sourceUserMissionDir,
        CancellationToken cancellationToken = default
    ) {
        var results = new List<TranslationArchiveEntry>();

        AppendCategoryEntries(
            results,
            dcsWorldInstallDir,
            Path.Combine( "Mods", "aircraft" ),
            TranslationArchiveCategory.Aircraft,
            cancellationToken );

        AppendCategoryEntries(
            results,
            dcsWorldInstallDir,
            Path.Combine( "Mods", "campaigns" ),
            TranslationArchiveCategory.DlcCampaigns,
            cancellationToken );

        AppendCategoryEntries(
            results,
            sourceUserMissionDir,
            string.Empty,
            TranslationArchiveCategory.UserMissions,
            cancellationToken );

        var ordered = results
            .OrderBy( entry => entry.Category )
            .ThenBy( entry => entry.RelativePath, StringComparer.OrdinalIgnoreCase )
            .ToArray();

        logger.Info( $"翻訳対象アーカイブの探索が完了した。Count={ordered.Length}" );
        return Task.FromResult<IReadOnlyList<TranslationArchiveEntry>>( ordered );
    }

    /// <summary>
    /// カテゴリ単位の探索結果を収集する。
    /// </summary>
    /// <param name="results">追加先コレクション。</param>
    /// <param name="baseDir">カテゴリ基底ディレクトリ。</param>
    /// <param name="relativeRoot">基底ディレクトリ配下の探索ルート。</param>
    /// <param name="category">カテゴリ。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    private void AppendCategoryEntries(
        ICollection<TranslationArchiveEntry> results,
        string? baseDir,
        string relativeRoot,
        TranslationArchiveCategory category,
        CancellationToken cancellationToken
    ) {
        if(string.IsNullOrWhiteSpace( baseDir )) {
            logger.Info( $"探索対象ディレクトリが未設定のためスキップする。Category={category}" );
            return;
        }

        var categoryRoot = string.IsNullOrWhiteSpace( relativeRoot )
            ? baseDir
            : Path.Combine( baseDir, relativeRoot );

        if(!Directory.Exists( categoryRoot )) {
            logger.Warn( $"探索対象ディレクトリが存在しないためスキップする。Category={category}, Path={categoryRoot}" );
            return;
        }

        AppendDirectoryEntries( results, categoryRoot, categoryRoot, category, cancellationToken );
    }

    /// <summary>
    /// 指定ディレクトリ配下を再帰的に探索して結果を収集する。
    /// </summary>
    /// <param name="results">追加先コレクション。</param>
    /// <param name="categoryRoot">カテゴリルートディレクトリ。</param>
    /// <param name="currentDirectory">現在探索中のディレクトリ。</param>
    /// <param name="category">カテゴリ。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    private void AppendDirectoryEntries(
        ICollection<TranslationArchiveEntry> results,
        string categoryRoot,
        string currentDirectory,
        TranslationArchiveCategory category,
        CancellationToken cancellationToken
    ) {
        cancellationToken.ThrowIfCancellationRequested();

        IEnumerable<string> files;
        try {
            files = EnumerateFiles( currentDirectory ).ToArray();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            logger.Warn( $"探索対象ディレクトリのファイル列挙に失敗したため対象ディレクトリをスキップする。Category={category}, Path={currentDirectory}", ex );
            return;
        }

        foreach(var filePath in files.Where( IsArchivePath )) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                var relativePath = Path.GetRelativePath( categoryRoot, filePath )
                    .Replace( "\\", "/", StringComparison.Ordinal );
                var archiveType = GetArchiveType( filePath );
                results.Add( new TranslationArchiveEntry(
                    Path.GetFileName( filePath ),
                    filePath,
                    relativePath,
                    category,
                    archiveType ) );
            }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
                logger.Warn( $"アーカイブの検査に失敗したため対象をスキップする。Path={filePath}", ex );
            }
        }

        IEnumerable<string> directories;
        try {
            directories = EnumerateDirectories( currentDirectory ).ToArray();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            logger.Warn( $"探索対象ディレクトリの子ディレクトリ列挙に失敗したため対象ディレクトリをスキップする。Category={category}, Path={currentDirectory}", ex );
            return;
        }

        foreach(var directoryPath in directories) {
            AppendDirectoryEntries( results, categoryRoot, directoryPath, category, cancellationToken );
        }
    }

    /// <summary>
    /// 指定ディレクトリ直下のファイル一覧を取得する。
    /// </summary>
    /// <param name="directoryPath">探索対象ディレクトリ。</param>
    /// <returns>ファイル一覧。</returns>
    protected virtual IEnumerable<string> EnumerateFiles( string directoryPath ) =>
        Directory.EnumerateFiles( directoryPath, "*", SearchOption.TopDirectoryOnly );

    /// <summary>
    /// 指定ディレクトリ直下の子ディレクトリ一覧を取得する。
    /// </summary>
    /// <param name="directoryPath">探索対象ディレクトリ。</param>
    /// <returns>子ディレクトリ一覧。</returns>
    protected virtual IEnumerable<string> EnumerateDirectories( string directoryPath ) =>
        Directory.EnumerateDirectories( directoryPath, "*", SearchOption.TopDirectoryOnly );

    /// <summary>
    /// アーカイブ種別を判定する。
    /// </summary>
    /// <param name="archivePath">対象パス。</param>
    /// <returns>判定した種別。</returns>
    private static TranslationArchiveType GetArchiveType( string archivePath ) =>
        Path.GetExtension( archivePath ).ToLowerInvariant() switch
        {
            ".miz" => TranslationArchiveType.Miz,
            ".trk" => TranslationArchiveType.Trk,
            _ => throw new InvalidOperationException( $"未対応のアーカイブ拡張子である: {archivePath}" ),
        };

    /// <summary>
    /// 翻訳対象アーカイブかどうかを判定する。
    /// </summary>
    /// <param name="path">判定対象パス。</param>
    /// <returns>対象の場合は <see langword="true"/>。</returns>
    private static bool IsArchivePath( string path ) {
        var extension = Path.GetExtension( path );
        return extension.Equals( ".miz", StringComparison.OrdinalIgnoreCase )
            || extension.Equals( ".trk", StringComparison.OrdinalIgnoreCase );
    }
}