using System.IO.Compression;

using DcsTranslationTool.Application.Enums;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// 翻訳対象アーカイブの探索を提供する。
/// </summary>
public sealed class TranslationArchiveDiscoveryService(
    ILoggingService logger
) : ITranslationArchiveDiscoveryService {
    private static readonly string DictionaryEntryPath = "l10n/default/dictionary";

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

        IEnumerable<string> archivePaths;
        try {
            archivePaths = Directory
                .EnumerateFiles( categoryRoot, "*", SearchOption.AllDirectories )
                .Where( path => IsArchivePath( path ) )
                .ToArray();
        }
        catch(Exception ex) {
            logger.Warn( $"探索対象ディレクトリの列挙に失敗したためカテゴリをスキップする。Category={category}, Path={categoryRoot}", ex );
            return;
        }

        foreach(var archivePath in archivePaths) {
            cancellationToken.ThrowIfCancellationRequested();

            try {
                if(!HasDictionaryEntry( archivePath )) {
                    continue;
                }

                var relativePath = Path.GetRelativePath( categoryRoot, archivePath )
                    .Replace( "\\", "/", StringComparison.Ordinal );
                var archiveType = GetArchiveType( archivePath );
                results.Add( new TranslationArchiveEntry(
                    Path.GetFileName( archivePath ),
                    archivePath,
                    relativePath,
                    category,
                    archiveType,
                    true ) );
            }
            catch(Exception ex) when(ex is InvalidDataException or IOException or UnauthorizedAccessException) {
                logger.Warn( $"アーカイブの検査に失敗したため対象をスキップする。Path={archivePath}", ex );
            }
        }
    }

    /// <summary>
    /// アーカイブに dictionary エントリが存在するかどうかを判定する。
    /// </summary>
    /// <param name="archivePath">検査対象アーカイブ。</param>
    /// <returns>存在する場合は <see langword="true"/>。</returns>
    private static bool HasDictionaryEntry( string archivePath ) {
        using var archive = ZipFile.OpenRead( archivePath );
        return archive.Entries.Any( entry =>
            string.Equals(
                entry.FullName.Replace( "\\", "/", StringComparison.Ordinal ),
                DictionaryEntryPath,
                StringComparison.OrdinalIgnoreCase ) );
    }

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