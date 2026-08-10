using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Application.Results;

using FluentResults;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// アプリケーション更新パッケージの安全な展開、検証、差分再構築を提供する。
/// </summary>
/// <param name="logger">ロギングサービスを受け取る。</param>
public sealed partial class ApplicationUpdatePackageService( ILoggingService logger ) {
    /// <summary>
    /// 更新マニフェストのファイル名を表す。
    /// </summary>
    public const string ManifestFileName = "update-manifest.json";

    /// <summary>
    /// 差分マニフェストのファイル名を表す。
    /// </summary>
    public const string DeltaManifestFileName = "delta-manifest.json";

    private const string ApplicationId = "DcsTranslationTool";
    private const string EntryPoint = "DCS-Translation-Tool.exe";
    private const int CurrentSchemaVersion = 1;
    private const int MaxEntryCount = 20_000;
    private const long MaxUncompressedBytes = 2L * 1024 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web )
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>
    /// 完全版パッケージを展開してマニフェストにより検証する。
    /// </summary>
    /// <param name="zipPath">完全版 ZIP の絶対パスを受け取る。</param>
    /// <param name="destinationDirectory">一意な展開先ディレクトリを受け取る。</param>
    /// <param name="targetVersion">期待する更新先バージョンを受け取る。</param>
    /// <param name="runtimeIdentifier">期待する RID を受け取る。</param>
    /// <returns>検証済みマニフェストを含む結果を返す。</returns>
    public Result<ApplicationUpdateManifest> ExtractFullPackage(
        string zipPath,
        string destinationDirectory,
        Version targetVersion,
        string runtimeIdentifier
    ) {
        var extractResult = ExtractArchive( zipPath, destinationDirectory );
        if(extractResult.IsFailed) {
            return extractResult.ToResult<ApplicationUpdateManifest>();
        }

        var manifestResult = ReadManifest( Path.Combine( destinationDirectory, ManifestFileName ) );
        if(manifestResult.IsFailed) {
            return manifestResult;
        }

        var validationResult = ValidateManifest(
            destinationDirectory,
            manifestResult.Value,
            targetVersion,
            runtimeIdentifier,
            requireExactFileSet: true );
        return validationResult.IsSuccess
            ? Result.Ok( manifestResult.Value )
            : validationResult.ToResult<ApplicationUpdateManifest>();
    }

    /// <summary>
    /// 現在の配布物へ差分を反映して更新先の完全なステージングを構築する。
    /// </summary>
    /// <param name="deltaZipPath">差分 ZIP の絶対パスを受け取る。</param>
    /// <param name="currentDirectory">現在の配布ルートを受け取る。</param>
    /// <param name="destinationDirectory">一意な再構築先を受け取る。</param>
    /// <param name="currentManifest">現在の配布マニフェストを受け取る。</param>
    /// <param name="targetVersion">期待する更新先バージョンを受け取る。</param>
    /// <param name="runtimeIdentifier">期待する RID を受け取る。</param>
    /// <returns>検証済み更新先マニフェストを含む結果を返す。</returns>
    public Result<ApplicationUpdateManifest> ReconstructDeltaPackage(
        string deltaZipPath,
        string currentDirectory,
        string destinationDirectory,
        ApplicationUpdateManifest currentManifest,
        Version targetVersion,
        string runtimeIdentifier
    ) {
        var currentVersionResult = ParseVersion( currentManifest.Version, "現在のマニフェストバージョンが不正です。" );
        if(currentVersionResult.IsFailed) {
            return currentVersionResult.ToResult<ApplicationUpdateManifest>();
        }

        var currentValidation = ValidateManifest(
            currentDirectory,
            currentManifest,
            currentVersionResult.Value,
            runtimeIdentifier,
            requireExactFileSet: false );
        if(currentValidation.IsFailed) {
            return currentValidation.ToResult<ApplicationUpdateManifest>();
        }

        var deltaDirectory = Path.Combine(
            Path.GetDirectoryName( Path.GetFullPath( destinationDirectory ) ) ?? Path.GetTempPath(),
            $"delta-{Guid.NewGuid():N}" );
        try {
            var extractResult = ExtractArchive( deltaZipPath, deltaDirectory );
            if(extractResult.IsFailed) {
                return extractResult.ToResult<ApplicationUpdateManifest>();
            }

            var deltaManifestResult = ReadDeltaManifest( Path.Combine( deltaDirectory, DeltaManifestFileName ) );
            if(deltaManifestResult.IsFailed) {
                return deltaManifestResult.ToResult<ApplicationUpdateManifest>();
            }

            var deltaManifest = deltaManifestResult.Value;
            var deltaValidation = ValidateDeltaManifest(
                deltaDirectory,
                deltaManifest,
                currentManifest,
                targetVersion,
                runtimeIdentifier );
            if(deltaValidation.IsFailed) {
                return deltaValidation.ToResult<ApplicationUpdateManifest>();
            }

            Directory.CreateDirectory( destinationDirectory );
            foreach(var file in currentManifest.Files) {
                var sourcePathResult = ResolveManagedPath( currentDirectory, file.Path );
                var destinationPathResult = ResolveManagedPath( destinationDirectory, file.Path );
                if(sourcePathResult.IsFailed || destinationPathResult.IsFailed) {
                    return Result.Fail( ResultErrorFactory.Validation( "現在のマニフェストに安全でないパスが含まれています。", "UPDATE_MANIFEST_PATH_INVALID" ) );
                }

                CopyFile( sourcePathResult.Value, destinationPathResult.Value, overwrite: false );
            }

            var currentPaths = currentManifest.Files.Select( file => NormalizeRelativePath( file.Path ) )
                .ToHashSet( StringComparer.OrdinalIgnoreCase );
            foreach(var removedPath in deltaManifest.RemovedFiles) {
                var normalizedPath = NormalizeRelativePath( removedPath );
                if(!currentPaths.Contains( normalizedPath )) {
                    return Result.Fail( ResultErrorFactory.Validation( $"管理対象外の削除パスが指定されています: {removedPath}", "UPDATE_DELTA_DELETE_UNMANAGED" ) );
                }

                var destinationPathResult = ResolveManagedPath( destinationDirectory, normalizedPath );
                if(destinationPathResult.IsFailed) {
                    return destinationPathResult.ToResult<ApplicationUpdateManifest>();
                }

                if(File.Exists( destinationPathResult.Value )) {
                    File.Delete( destinationPathResult.Value );
                }
            }

            foreach(var changedPath in deltaManifest.AddedOrChangedFiles) {
                var normalizedPath = NormalizeRelativePath( changedPath );
                var payloadPathResult = ResolveManagedPath( Path.Combine( deltaDirectory, "payload" ), normalizedPath );
                var destinationPathResult = ResolveManagedPath( destinationDirectory, normalizedPath );
                if(payloadPathResult.IsFailed || destinationPathResult.IsFailed) {
                    return Result.Fail( ResultErrorFactory.Validation( "差分に安全でないパスが含まれています。", "UPDATE_DELTA_PATH_INVALID" ) );
                }

                CopyFile( payloadPathResult.Value, destinationPathResult.Value, overwrite: true );
            }

            File.WriteAllText(
                Path.Combine( destinationDirectory, ManifestFileName ),
                JsonSerializer.Serialize( deltaManifest.TargetManifest, JsonOptions ) );
            var finalValidation = ValidateManifest(
                destinationDirectory,
                deltaManifest.TargetManifest,
                targetVersion,
                runtimeIdentifier,
                requireExactFileSet: true );
            return finalValidation.IsSuccess
                ? Result.Ok( deltaManifest.TargetManifest )
                : finalValidation.ToResult<ApplicationUpdateManifest>();
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
            logger.Error( "差分更新パッケージの再構築に失敗した。", ex );
            return Result.Fail( ResultErrorFactory.External( "差分更新パッケージを再構築できませんでした。", "UPDATE_DELTA_RECONSTRUCTION_FAILED", ex ) );
        }
        finally {
            TryDeleteDirectory( deltaDirectory );
        }
    }

    /// <summary>
    /// 配布ルートから更新マニフェストを読み込む。
    /// </summary>
    /// <param name="applicationDirectory">配布ルートを受け取る。</param>
    /// <returns>読み込んだマニフェストを含む結果を返す。</returns>
    [SuppressMessage( "Performance", "CA1822:Mark members as static", Justification = "更新パッケージ操作を提供するサービスのインスタンスAPIとして公開するため。" )]
    public Result<ApplicationUpdateManifest> ReadInstalledManifest( string applicationDirectory ) =>
        ReadManifest( Path.Combine( applicationDirectory, ManifestFileName ) );

    /// <summary>
    /// 配布ディレクトリをマニフェストにより検証する。
    /// </summary>
    /// <param name="directory">検証対象ディレクトリを受け取る。</param>
    /// <param name="manifest">検証に利用するマニフェストを受け取る。</param>
    /// <param name="expectedVersion">期待するバージョンを受け取る。</param>
    /// <param name="runtimeIdentifier">期待する RID を受け取る。</param>
    /// <param name="requireExactFileSet">未知の追加ファイルを拒否するかどうかを受け取る。</param>
    /// <returns>検証結果を返す。</returns>
    [SuppressMessage( "Performance", "CA1822:Mark members as static", Justification = "更新パッケージ操作を提供するサービスのインスタンスAPIとして公開するため。" )]
    public Result ValidateManifest(
        string directory,
        ApplicationUpdateManifest manifest,
        Version expectedVersion,
        string runtimeIdentifier,
        bool requireExactFileSet
    ) {
        if(string.IsNullOrWhiteSpace( manifest.ApplicationId ) ||
            string.IsNullOrWhiteSpace( manifest.Version ) ||
            string.IsNullOrWhiteSpace( manifest.RuntimeIdentifier ) ||
            string.IsNullOrWhiteSpace( manifest.EntryPoint ) ||
            manifest.Files is null) {
            return Result.Fail( ResultErrorFactory.Validation( "更新マニフェストの必須項目が不足しています。", "UPDATE_MANIFEST_REQUIRED_FIELD_MISSING" ) );
        }

        if(manifest.SchemaVersion != CurrentSchemaVersion ||
            !string.Equals( manifest.ApplicationId, ApplicationId, StringComparison.Ordinal ) ||
            !string.Equals( manifest.RuntimeIdentifier, runtimeIdentifier, StringComparison.Ordinal ) ||
            !string.Equals( NormalizeRelativePath( manifest.EntryPoint ), EntryPoint, StringComparison.OrdinalIgnoreCase )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新マニフェストの識別情報が一致しません。", "UPDATE_MANIFEST_IDENTITY_MISMATCH" ) );
        }

        var manifestVersionResult = ParseVersion( manifest.Version, "更新マニフェストのバージョンが不正です。" );
        if(manifestVersionResult.IsFailed || NormalizeVersion( manifestVersionResult.Value ) != NormalizeVersion( expectedVersion )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新マニフェストのバージョンが一致しません。", "UPDATE_MANIFEST_VERSION_MISMATCH" ) );
        }

        var manifestPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        foreach(var file in manifest.Files) {
            if(file is null || string.IsNullOrWhiteSpace( file.Path ) || string.IsNullOrWhiteSpace( file.Sha256 )) {
                return Result.Fail( ResultErrorFactory.Validation( "更新マニフェストのファイル情報に必須項目がありません。", "UPDATE_MANIFEST_FILE_INVALID" ) );
            }

            var normalizedPath = NormalizeRelativePath( file.Path );
            if(!manifestPaths.Add( normalizedPath ) ||
                IsReservedPath( normalizedPath ) ||
                file.Size < 0 ||
                !Sha256Regex().IsMatch( file.Sha256 )) {
                return Result.Fail( ResultErrorFactory.Validation( $"更新マニフェストのファイル情報が不正です: {file.Path}", "UPDATE_MANIFEST_FILE_INVALID" ) );
            }

            var filePathResult = ResolveManagedPath( directory, normalizedPath );
            if(filePathResult.IsFailed || !File.Exists( filePathResult.Value )) {
                return Result.Fail( ResultErrorFactory.Validation( $"更新ファイルが存在しません: {file.Path}", "UPDATE_MANIFEST_FILE_MISSING" ) );
            }

            var fileInfo = new FileInfo( filePathResult.Value );
            if(fileInfo.Length != file.Size || !string.Equals( CalculateSha256( filePathResult.Value ), file.Sha256, StringComparison.Ordinal )) {
                return Result.Fail( ResultErrorFactory.Validation( $"更新ファイルの整合性が一致しません: {file.Path}", "UPDATE_MANIFEST_HASH_MISMATCH" ) );
            }
        }

        if(!manifestPaths.Contains( EntryPoint )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新マニフェストにエントリポイントが含まれていません。", "UPDATE_MANIFEST_ENTRY_POINT_MISSING" ) );
        }

        if(requireExactFileSet) {
            var actualPaths = Directory.GetFiles( directory, "*", SearchOption.AllDirectories )
                .Select( path => NormalizeRelativePath( Path.GetRelativePath( directory, path ) ) )
                .Where( path => !string.Equals( path, ManifestFileName, StringComparison.OrdinalIgnoreCase ) )
                .ToHashSet( StringComparer.OrdinalIgnoreCase );
            if(!actualPaths.SetEquals( manifestPaths )) {
                return Result.Fail( ResultErrorFactory.Validation( "更新パッケージにマニフェスト未掲載のファイルがあります。", "UPDATE_PACKAGE_FILE_SET_MISMATCH" ) );
            }
        }

        return Result.Ok();
    }

    private static Result ExtractArchive( string zipPath, string destinationDirectory ) {
        if(!File.Exists( zipPath )) {
            return Result.Fail( ResultErrorFactory.NotFound( "更新 ZIP が存在しません。", "UPDATE_ZIP_NOT_FOUND" ) );
        }

        if(Directory.Exists( destinationDirectory ) && Directory.EnumerateFileSystemEntries( destinationDirectory ).Any()) {
            return Result.Fail( ResultErrorFactory.Conflict( "更新 ZIP の展開先が空ではありません。", "UPDATE_DESTINATION_NOT_EMPTY" ) );
        }

        try {
            Directory.CreateDirectory( destinationDirectory );
            using var archive = ZipFile.OpenRead( zipPath );
            if(archive.Entries.Count > MaxEntryCount || archive.Entries.Sum( entry => entry.Length ) > MaxUncompressedBytes) {
                return Result.Fail( ResultErrorFactory.Validation( "更新 ZIP の展開サイズが上限を超えています。", "UPDATE_ZIP_LIMIT_EXCEEDED" ) );
            }

            var seenPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
            foreach(var entry in archive.Entries) {
                var normalizedPath = NormalizeRelativePath( entry.FullName );
                if(string.IsNullOrWhiteSpace( normalizedPath )) {
                    continue;
                }

                if(!seenPaths.Add( normalizedPath )) {
                    return Result.Fail( ResultErrorFactory.Validation( $"更新 ZIP に重複パスがあります: {entry.FullName}", "UPDATE_ZIP_DUPLICATE_PATH" ) );
                }

                var destinationPathResult = ResolveManagedPath( destinationDirectory, normalizedPath );
                if(destinationPathResult.IsFailed) {
                    return destinationPathResult.ToResult();
                }

                if(entry.FullName.EndsWith( '/' ) || entry.FullName.EndsWith( '\\' )) {
                    Directory.CreateDirectory( destinationPathResult.Value );
                    continue;
                }

                var parent = Path.GetDirectoryName( destinationPathResult.Value );
                if(!string.IsNullOrWhiteSpace( parent )) {
                    Directory.CreateDirectory( parent );
                }

                using var source = entry.Open();
                using var destination = new FileStream( destinationPathResult.Value, FileMode.CreateNew, FileAccess.Write, FileShare.None );
                source.CopyTo( destination );
            }

            return Result.Ok();
        }
        catch(Exception ex) when(ex is InvalidDataException or IOException or UnauthorizedAccessException) {
            return Result.Fail( ResultErrorFactory.External( "更新 ZIP を安全に展開できませんでした。", "UPDATE_ZIP_EXTRACTION_FAILED", ex ) );
        }
    }

    private static Result ValidateDeltaManifest(
        string deltaDirectory,
        ApplicationUpdateDeltaManifest deltaManifest,
        ApplicationUpdateManifest currentManifest,
        Version targetVersion,
        string runtimeIdentifier
    ) {
        if(string.IsNullOrWhiteSpace( deltaManifest.ApplicationId ) ||
            string.IsNullOrWhiteSpace( deltaManifest.BaseVersion ) ||
            string.IsNullOrWhiteSpace( deltaManifest.TargetVersion ) ||
            string.IsNullOrWhiteSpace( deltaManifest.RuntimeIdentifier ) ||
            deltaManifest.AddedOrChangedFiles is null ||
            deltaManifest.RemovedFiles is null ||
            deltaManifest.TargetManifest is null ||
            string.IsNullOrWhiteSpace( deltaManifest.TargetManifest.ApplicationId ) ||
            string.IsNullOrWhiteSpace( deltaManifest.TargetManifest.Version ) ||
            string.IsNullOrWhiteSpace( deltaManifest.TargetManifest.RuntimeIdentifier ) ||
            string.IsNullOrWhiteSpace( deltaManifest.TargetManifest.EntryPoint ) ||
            deltaManifest.TargetManifest.Files is null ||
            deltaManifest.TargetManifest.Files.Any( file => file is null || string.IsNullOrWhiteSpace( file.Path ) || string.IsNullOrWhiteSpace( file.Sha256 ) )) {
            return Result.Fail( ResultErrorFactory.Validation( "差分マニフェストの必須項目が不足しています。", "UPDATE_DELTA_REQUIRED_FIELD_MISSING" ) );
        }

        if(deltaManifest.SchemaVersion != CurrentSchemaVersion ||
            !string.Equals( deltaManifest.ApplicationId, ApplicationId, StringComparison.Ordinal ) ||
            !string.Equals( deltaManifest.RuntimeIdentifier, runtimeIdentifier, StringComparison.Ordinal ) ||
            !string.Equals( deltaManifest.BaseVersion, currentManifest.Version, StringComparison.Ordinal ) ||
            !Version.TryParse( deltaManifest.TargetVersion, out var parsedTargetVersion ) ||
            NormalizeVersion( parsedTargetVersion ) != NormalizeVersion( targetVersion )) {
            return Result.Fail( ResultErrorFactory.Validation( "差分マニフェストの識別情報が一致しません。", "UPDATE_DELTA_IDENTITY_MISMATCH" ) );
        }

        var targetPaths = deltaManifest.TargetManifest.Files
            .Select( file => NormalizeRelativePath( file.Path ) )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );
        var changedPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        foreach(var path in deltaManifest.AddedOrChangedFiles) {
            if(string.IsNullOrWhiteSpace( path )) {
                return Result.Fail( ResultErrorFactory.Validation( "差分の変更パスが不正です。", "UPDATE_DELTA_CHANGED_PATH_INVALID" ) );
            }

            var normalizedPath = NormalizeRelativePath( path );
            if(!changedPaths.Add( normalizedPath ) || !targetPaths.Contains( normalizedPath ) || IsReservedPath( normalizedPath )) {
                return Result.Fail( ResultErrorFactory.Validation( $"差分の変更パスが不正です: {path}", "UPDATE_DELTA_CHANGED_PATH_INVALID" ) );
            }
        }

        var removedPaths = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
        foreach(var path in deltaManifest.RemovedFiles) {
            if(string.IsNullOrWhiteSpace( path )) {
                return Result.Fail( ResultErrorFactory.Validation( "差分の削除パスが不正です。", "UPDATE_DELTA_REMOVED_PATH_INVALID" ) );
            }

            var normalizedPath = NormalizeRelativePath( path );
            if(!removedPaths.Add( normalizedPath ) || targetPaths.Contains( normalizedPath ) || IsReservedPath( normalizedPath )) {
                return Result.Fail( ResultErrorFactory.Validation( $"差分の削除パスが不正です: {path}", "UPDATE_DELTA_REMOVED_PATH_INVALID" ) );
            }
        }

        var actualPayloadPaths = Directory.Exists( Path.Combine( deltaDirectory, "payload" ) )
            ? Directory.GetFiles( Path.Combine( deltaDirectory, "payload" ), "*", SearchOption.AllDirectories )
                .Select( path => NormalizeRelativePath( Path.GetRelativePath( Path.Combine( deltaDirectory, "payload" ), path ) ) )
                .ToHashSet( StringComparer.OrdinalIgnoreCase )
            : [];
        if(!actualPayloadPaths.SetEquals( changedPaths )) {
            return Result.Fail( ResultErrorFactory.Validation( "差分 payload と変更一覧が一致しません。", "UPDATE_DELTA_PAYLOAD_MISMATCH" ) );
        }

        var actualArchivePaths = Directory.GetFiles( deltaDirectory, "*", SearchOption.AllDirectories )
            .Select( path => NormalizeRelativePath( Path.GetRelativePath( deltaDirectory, path ) ) )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );
        var expectedArchivePaths = changedPaths.Select( path => $"payload/{path}" )
            .Append( DeltaManifestFileName )
            .ToHashSet( StringComparer.OrdinalIgnoreCase );
        return actualArchivePaths.SetEquals( expectedArchivePaths )
            ? Result.Ok()
            : Result.Fail( ResultErrorFactory.Validation( "差分 ZIP に未掲載ファイルがあります。", "UPDATE_DELTA_FILE_SET_MISMATCH" ) );
    }

    private static Result<ApplicationUpdateManifest> ReadManifest( string manifestPath ) =>
        ReadJson<ApplicationUpdateManifest>( manifestPath, "更新マニフェストを読み込めませんでした。", "UPDATE_MANIFEST_READ_FAILED" );

    private static Result<ApplicationUpdateDeltaManifest> ReadDeltaManifest( string manifestPath ) =>
        ReadJson<ApplicationUpdateDeltaManifest>( manifestPath, "差分マニフェストを読み込めませんでした。", "UPDATE_DELTA_MANIFEST_READ_FAILED" );

    private static Result<T> ReadJson<T>( string path, string message, string code ) where T : class {
        if(!File.Exists( path )) {
            return Result.Fail( ResultErrorFactory.NotFound( message, code ) );
        }

        try {
            var value = JsonSerializer.Deserialize<T>( File.ReadAllText( path ), JsonOptions );
            return value is null
                ? Result.Fail( ResultErrorFactory.Validation( message, code ) )
                : Result.Ok( value );
        }
        catch(Exception ex) when(ex is JsonException or IOException) {
            return Result.Fail( ResultErrorFactory.External( message, code, ex ) );
        }
    }

    private static Result<Version> ParseVersion( string value, string message ) =>
        Version.TryParse( value, out var version )
            ? Result.Ok( version )
            : Result.Fail( ResultErrorFactory.Validation( message, "UPDATE_VERSION_INVALID" ) );

    private static Result<string> ResolveManagedPath( string rootDirectory, string relativePath ) {
        var normalizedPath = NormalizeRelativePath( relativePath );
        if(string.IsNullOrWhiteSpace( normalizedPath ) ||
            Path.IsPathRooted( relativePath ) ||
            normalizedPath.Split( '/', StringSplitOptions.RemoveEmptyEntries ).Any( segment => segment is "." or ".." || segment.Contains( ':', StringComparison.Ordinal ) )) {
            return Result.Fail( ResultErrorFactory.Validation( $"安全でない更新パスです: {relativePath}", "UPDATE_PATH_UNSAFE" ) );
        }

        var rootFullPath = Path.GetFullPath( rootDirectory );
        var rootWithSeparator = rootFullPath.EndsWith( Path.DirectorySeparatorChar )
            ? rootFullPath
            : rootFullPath + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath( Path.Combine( rootFullPath, normalizedPath.Replace( '/', Path.DirectorySeparatorChar ) ) );
        if(!fullPath.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
            return Result.Fail( ResultErrorFactory.Validation( $"展開先外を指す更新パスです: {relativePath}", "UPDATE_PATH_OUTSIDE_ROOT" ) );
        }

        if(Directory.Exists( rootFullPath ) && File.GetAttributes( rootFullPath ).HasFlag( FileAttributes.ReparsePoint )) {
            return Result.Fail( ResultErrorFactory.Validation( "更新ルートに再解析ポイントを使用できません。", "UPDATE_ROOT_REPARSE_POINT" ) );
        }

        var currentPath = rootFullPath;
        foreach(var segment in normalizedPath.Split( '/', StringSplitOptions.RemoveEmptyEntries )) {
            currentPath = Path.Combine( currentPath, segment );
            if((File.Exists( currentPath ) || Directory.Exists( currentPath )) &&
                File.GetAttributes( currentPath ).HasFlag( FileAttributes.ReparsePoint )) {
                return Result.Fail( ResultErrorFactory.Validation( $"更新パスに再解析ポイントが含まれています: {relativePath}", "UPDATE_PATH_REPARSE_POINT" ) );
            }
        }

        return Result.Ok( fullPath );
    }

    private static bool IsReservedPath( string normalizedPath ) =>
        string.Equals( normalizedPath, ManifestFileName, StringComparison.OrdinalIgnoreCase ) ||
        string.Equals( normalizedPath, "Logs", StringComparison.OrdinalIgnoreCase ) ||
        normalizedPath.StartsWith( "Logs/", StringComparison.OrdinalIgnoreCase ) ||
        string.Equals( normalizedPath, "TranslateFile", StringComparison.OrdinalIgnoreCase ) ||
        normalizedPath.StartsWith( "TranslateFile/", StringComparison.OrdinalIgnoreCase ) ||
        string.Equals( normalizedPath, ".dcs-translation-cache", StringComparison.OrdinalIgnoreCase ) ||
        normalizedPath.StartsWith( ".dcs-translation-cache/", StringComparison.OrdinalIgnoreCase );

    private static string NormalizeRelativePath( string path ) =>
        path.Replace( '\\', '/' ).TrimStart( '/' ).TrimEnd( '/' );

    private static Version NormalizeVersion( Version version ) =>
        new( version.Major, version.Minor, Math.Max( version.Build, 0 ), Math.Max( version.Revision, 0 ) );

    /// <summary>
    /// 小文字16進数のSHA-256文字列を検証する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex( "^[0-9a-f]{64}$", RegexOptions.CultureInvariant )]
    private static partial Regex Sha256Regex();

    private static string CalculateSha256( string path ) {
        using var stream = File.OpenRead( path );
        return Convert.ToHexString( SHA256.HashData( stream ) ).ToLowerInvariant();
    }

    private static void CopyFile( string sourcePath, string destinationPath, bool overwrite ) {
        var parent = Path.GetDirectoryName( destinationPath );
        if(!string.IsNullOrWhiteSpace( parent )) {
            Directory.CreateDirectory( parent );
        }

        File.Copy( sourcePath, destinationPath, overwrite );
    }

    private static void TryDeleteDirectory( string path ) {
        try {
            if(Directory.Exists( path )) {
                Directory.Delete( path, true );
            }
        }
        catch(IOException) { }
        catch(UnauthorizedAccessException) { }
    }
}