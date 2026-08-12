using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.TestCommon.IO;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

/// <summary>
/// <see cref="ApplicationUpdatePackageService"/> の安全な展開と差分再構築を検証する。
/// </summary>
public sealed class ApplicationUpdatePackageServiceTests {
    [Fact]
    public void ExtractFullPackageはマニフェストと一致する完全版を展開する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ExtractFullPackageはマニフェストと一致する完全版を展開する ) );
        var packageSource = temporaryDirectory.GetPath( "source" );
        var destination = temporaryDirectory.GetPath( "destination" );
        Directory.CreateDirectory( packageSource );
        File.WriteAllText( Path.Combine( packageSource, "DCS-Translation-Tool.exe" ), "new executable" );
        File.WriteAllText( Path.Combine( packageSource, "library.dll" ), "new library" );
        WriteManifest( packageSource, "1.4.0", "win-x64" );
        var zipPath = temporaryDirectory.GetPath( "full.zip" );
        ZipFile.CreateFromDirectory( packageSource, zipPath );
        var sut = new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() );

        var result = sut.ExtractFullPackage( zipPath, destination, new Version( 1, 4, 0 ), "win-x64" );

        Assert.True( result.IsSuccess );
        Assert.Equal( "new executable", File.ReadAllText( Path.Combine( destination, "DCS-Translation-Tool.exe" ) ) );
        Assert.Equal( "new library", File.ReadAllText( Path.Combine( destination, "library.dll" ) ) );
    }

    [Fact]
    public void ExtractFullPackageは展開先外を指すエントリを拒否する() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ExtractFullPackageは展開先外を指すエントリを拒否する ) );
        var zipPath = temporaryDirectory.GetPath( "malicious.zip" );
        using(var archive = ZipFile.Open( zipPath, ZipArchiveMode.Create )) {
            var entry = archive.CreateEntry( "../outside.txt" );
            using var writer = new StreamWriter( entry.Open() );
            writer.Write( "malicious" );
        }

        var destination = temporaryDirectory.GetPath( "destination" );
        var sut = new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() );

        var result = sut.ExtractFullPackage( zipPath, destination, new Version( 1, 4, 0 ), "win-x64" );

        Assert.True( result.IsFailed );
        Assert.False( File.Exists( temporaryDirectory.GetPath( "outside.txt" ) ) );
    }

    [Fact]
    public void ReconstructDeltaPackageは変更追加削除を反映して未知ファイルを含めない() {
        using var temporaryDirectory = new TemporaryDirectory( nameof( ReconstructDeltaPackageは変更追加削除を反映して未知ファイルを含めない ) );
        var currentDirectory = temporaryDirectory.GetPath( "current" );
        Directory.CreateDirectory( currentDirectory );
        File.WriteAllText( Path.Combine( currentDirectory, "DCS-Translation-Tool.exe" ), "old executable" );
        File.WriteAllText( Path.Combine( currentDirectory, "removed.dll" ), "remove me" );
        File.WriteAllText( Path.Combine( currentDirectory, "user.txt" ), "keep outside package" );
        var currentManifest = WriteManifest( currentDirectory, "1.3.1", "win-x64", "user.txt" );

        var targetFiles = new Dictionary<string, string>
        {
            ["DCS-Translation-Tool.exe"] = "new executable",
            ["added.dll"] = "added"
        };
        var targetManifest = CreateManifest( "1.4.0", "win-x64", targetFiles );
        var deltaManifest = new ApplicationUpdateDeltaManifest(
            1,
            "DcsTranslationTool",
            "1.3.1",
            "1.4.0",
            "win-x64",
            ["DCS-Translation-Tool.exe", "added.dll"],
            ["removed.dll"],
            targetManifest );
        var deltaRoot = temporaryDirectory.GetPath( "delta" );
        Directory.CreateDirectory( Path.Combine( deltaRoot, "payload" ) );
        foreach(var (relativePath, content) in targetFiles) {
            File.WriteAllText( Path.Combine( deltaRoot, "payload", relativePath ), content );
        }
        File.WriteAllText(
            Path.Combine( deltaRoot, "delta-manifest.json" ),
            JsonSerializer.Serialize( deltaManifest, JsonOptions ) );
        var deltaZipPath = temporaryDirectory.GetPath( "delta.zip" );
        ZipFile.CreateFromDirectory( deltaRoot, deltaZipPath );
        var destination = temporaryDirectory.GetPath( "destination" );
        var sut = new ApplicationUpdatePackageService( Mock.Of<ILoggingService>() );

        var result = sut.ReconstructDeltaPackage(
            deltaZipPath,
            currentDirectory,
            destination,
            currentManifest,
            new Version( 1, 4, 0 ),
            "win-x64" );

        Assert.True( result.IsSuccess );
        Assert.Equal( "new executable", File.ReadAllText( Path.Combine( destination, "DCS-Translation-Tool.exe" ) ) );
        Assert.Equal( "added", File.ReadAllText( Path.Combine( destination, "added.dll" ) ) );
        Assert.False( File.Exists( Path.Combine( destination, "removed.dll" ) ) );
        Assert.False( File.Exists( Path.Combine( destination, "user.txt" ) ) );
    }

    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web )
    {
        WriteIndented = true
    };

    private static ApplicationUpdateManifest WriteManifest(
        string directory,
        string version,
        string runtimeIdentifier,
        params string[] excludedRelativePaths
    ) {
        var excluded = excludedRelativePaths.ToHashSet( StringComparer.OrdinalIgnoreCase );
        var files = Directory.GetFiles( directory, "*", SearchOption.AllDirectories )
            .Select( path => Path.GetRelativePath( directory, path ).Replace( '\\', '/' ) )
            .Where( path => !excluded.Contains( path ) )
            .ToDictionary( path => path, path => File.ReadAllText( Path.Combine( directory, path.Replace( '/', Path.DirectorySeparatorChar ) ) ) );
        var manifest = CreateManifest( version, runtimeIdentifier, files );
        File.WriteAllText( Path.Combine( directory, "update-manifest.json" ), JsonSerializer.Serialize( manifest, JsonOptions ) );
        return manifest;
    }

    private static ApplicationUpdateManifest CreateManifest(
        string version,
        string runtimeIdentifier,
        IReadOnlyDictionary<string, string> files
    ) => new(
        1,
        "DcsTranslationTool",
        version,
        runtimeIdentifier,
        "DCS-Translation-Tool.exe",
        [.. files.Select( pair => new ApplicationUpdateManifestFile(
            pair.Key,
            System.Text.Encoding.UTF8.GetByteCount( pair.Value ),
            Convert.ToHexString( SHA256.HashData( System.Text.Encoding.UTF8.GetBytes( pair.Value ) ) ).ToLowerInvariant() ) )] );
}