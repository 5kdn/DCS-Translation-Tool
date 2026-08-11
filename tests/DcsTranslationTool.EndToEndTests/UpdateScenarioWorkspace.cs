using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DcsTranslationTool.EndToEndTests;

/// <summary>
/// 1件の更新E2Eシナリオで使用する隔離ディレクトリと検証処理を提供する。
/// </summary>
internal sealed class UpdateScenarioWorkspace : IDisposable {
    private static readonly JsonSerializerOptions IndentedJsonOptions = new() { WriteIndented = true };

    private readonly Dictionary<int, string> _ownedProcesses = [];
    private readonly JsonElement _expectedTargetManifest;
    private bool _succeeded;

    /// <summary>
    /// シナリオ用ワークスペースを作成する。
    /// </summary>
    /// <param name="configuration">生成済みE2Eアセット構成。</param>
    /// <param name="scenarioName">診断ディレクトリ名に使用するシナリオ名。</param>
    /// <param name="corruptDelta">差分マニフェストを破損させる場合はtrue。</param>
    public UpdateScenarioWorkspace(
        EndToEndTestConfiguration configuration,
        string scenarioName,
        bool corruptDelta
    ) {
        var runId = $"{scenarioName}-{Guid.NewGuid():N}";
        this.RootDirectory = Path.Combine( configuration.DiagnosticsDirectory, runId );
        this.ApplicationDirectory = Path.Combine( this.RootDirectory, "application" );
        this.AssetsDirectory = Path.Combine( this.RootDirectory, "assets" );
        this.UpdatesDirectory = Path.Combine( this.RootDirectory, "updates" );
        this.CacheDirectory = Path.Combine( this.RootDirectory, "local-app-data" );
        this.UserDataDirectory = Path.Combine( this.RootDirectory, "roaming-app-data" );
        this.ConfigurationPath = Path.Combine( this.RootDirectory, "e2e-config.json" );
        this.RunId = runId;
        this._expectedTargetManifest = ReadTargetManifest(
            configuration.TargetAssetsDirectory,
            configuration.RuntimeIdentifier );

        Directory.CreateDirectory( this.ApplicationDirectory );
        Directory.CreateDirectory( this.AssetsDirectory );
        Directory.CreateDirectory( this.UpdatesDirectory );
        Directory.CreateDirectory( this.CacheDirectory );
        Directory.CreateDirectory( this.UserDataDirectory );
        SeedApplicationSettings( this.UserDataDirectory, this.ApplicationDirectory );
        ZipFile.ExtractToDirectory( configuration.BasePackagePath, this.ApplicationDirectory );
        CopyDirectory( configuration.IntermediateAssetsDirectory, this.AssetsDirectory );
        CopyDirectory( configuration.TargetAssetsDirectory, this.AssetsDirectory );
        if(corruptDelta) {
            CorruptDeltaPackage( this.AssetsDirectory, configuration.RuntimeIdentifier );
        }

        SeedPreservedFiles( this.ApplicationDirectory );
    }

    /// <summary>シナリオルートを取得する。</summary>
    public string RootDirectory { get; }

    /// <summary>アプリケーション展開先を取得する。</summary>
    public string ApplicationDirectory { get; }

    /// <summary>ローカル配信するアセットディレクトリを取得する。</summary>
    public string AssetsDirectory { get; }

    /// <summary>更新作業ディレクトリを取得する。</summary>
    public string UpdatesDirectory { get; }

    /// <summary>キャッシュ保存先を取得する。</summary>
    public string CacheDirectory { get; }

    /// <summary>設定保存先を取得する。</summary>
    public string UserDataDirectory { get; }

    /// <summary>E2E構成ファイルパスを取得する。</summary>
    public string ConfigurationPath { get; }

    /// <summary>シナリオの実行IDを取得する。</summary>
    public string RunId { get; }

    /// <summary>展開先のアプリケーションEXEパスを取得する。</summary>
    public string ApplicationExecutablePath => Path.Combine( this.ApplicationDirectory, "DCS-Translation-Tool.exe" );

    /// <summary>
    /// 起動中のローカルReleaseサーバーを参照するE2E構成を書き込む。
    /// </summary>
    /// <param name="releaseApiBaseUri">Release APIのベースURI。</param>
    public void WriteApplicationConfiguration( Uri releaseApiBaseUri ) {
        var json = JsonSerializer.Serialize(
            new
            {
                releaseApiBaseUrl = releaseApiBaseUri.AbsoluteUri,
                updatesDirectory = this.UpdatesDirectory,
                cacheDirectory = this.CacheDirectory,
                userDataDirectory = this.UserDataDirectory,
                runId = this.RunId
            },
            IndentedJsonOptions );
        File.WriteAllText( this.ConfigurationPath, $"{json}\r\n", new UTF8Encoding( false ) );
    }

    /// <summary>
    /// テストが所有するプロセスIDと実行ファイルパスを記録する。
    /// </summary>
    /// <param name="processId">記録するプロセスID。</param>
    /// <param name="executablePath">記録するプロセスの実行ファイルパス。</param>
    public void TrackProcess( int processId, string executablePath ) =>
        this._ownedProcesses[processId] = Path.GetFullPath( executablePath );

    /// <summary>
    /// サーバー要求履歴を診断ファイルへ保存する。
    /// </summary>
    /// <param name="requests">保存する要求パス。</param>
    public void WriteRequestLog( IEnumerable<string> requests ) =>
        File.WriteAllLines( Path.Combine( this.RootDirectory, "http-requests.log" ), requests, new UTF8Encoding( false ) );

    /// <summary>
    /// 更新後の管理対象ファイルをマニフェストに対して検証する。
    /// </summary>
    public void AssertTargetManifest() {
        var manifestPath = Path.Combine( this.ApplicationDirectory, "update-manifest.json" );
        using var document = JsonDocument.Parse( File.ReadAllText( manifestPath ) );
        var actualManifest = document.RootElement;
        foreach(var propertyName in new[]
        {
            "schemaVersion",
            "applicationId",
            "version",
            "runtimeIdentifier",
            "entryPoint"
        }) {
            Assert.Equal(
                this._expectedTargetManifest.GetProperty( propertyName ).GetRawText(),
                actualManifest.GetProperty( propertyName ).GetRawText() );
        }

        var expectedFiles = GetManifestFiles( this._expectedTargetManifest );
        var actualFiles = GetManifestFiles( actualManifest );
        Assert.Equal(
            expectedFiles.Keys.OrderBy( path => path, StringComparer.OrdinalIgnoreCase ),
            actualFiles.Keys.OrderBy( path => path, StringComparer.OrdinalIgnoreCase ) );
        foreach(var (relativePath, expectedFile) in expectedFiles) {
            var actualFile = actualFiles[relativePath];
            Assert.Equal( expectedFile.GetProperty( "size" ).GetInt64(), actualFile.GetProperty( "size" ).GetInt64() );
            Assert.Equal( expectedFile.GetProperty( "sha256" ).GetString(), actualFile.GetProperty( "sha256" ).GetString() );
            var path = Path.Combine( this.ApplicationDirectory, relativePath.Replace( '/', Path.DirectorySeparatorChar ) );
            Assert.True( File.Exists( path ), $"Managed file is missing: {relativePath}" );
            var info = new FileInfo( path );
            Assert.Equal( expectedFile.GetProperty( "size" ).GetInt64(), info.Length );
            var hash = Convert.ToHexString( SHA256.HashData( File.ReadAllBytes( path ) ) ).ToLowerInvariant();
            Assert.Equal( expectedFile.GetProperty( "sha256" ).GetString(), hash );
        }
    }

    /// <summary>
    /// 更新対象外の利用者ファイルが維持されたことを検証する。
    /// </summary>
    public void AssertPreservedFiles() {
        Assert.Equal( "user log", File.ReadAllText( Path.Combine( this.ApplicationDirectory, "Logs", "user.log" ) ) );
        Assert.Equal( "translation", File.ReadAllText( Path.Combine( this.ApplicationDirectory, "TranslateFile", "user.txt" ) ) );
        Assert.Equal( "unknown", File.ReadAllText( Path.Combine( this.ApplicationDirectory, "user-note.txt" ) ) );
    }

    /// <summary>
    /// シナリオが成功したことを記録し、破棄時の診断領域削除を許可する。
    /// </summary>
    public void MarkSucceeded() => this._succeeded = true;

    /// <summary>
    /// 記録済みPIDとシナリオ配下の実行ファイルから起動したプロセスを終了する。
    /// </summary>
    public void TerminateOwnedProcesses() {
        foreach(var (processId, executablePath) in this._ownedProcesses) {
            TryTerminateProcess( processId, path => PathsEqual( path, executablePath ) );
        }

        foreach(var process in FindWorkspaceProcesses()) {
            using(process) {
                TryTerminateProcess( process, path => IsPathWithinRoot( path, this.RootDirectory ) );
            }
        }
    }

    /// <summary>
    /// シナリオ配下の実行ファイルから起動したプロセスが残っていないことを確認する。
    /// </summary>
    public void AssertNoOwnedProcesses() {
        var processes = FindWorkspaceProcesses();
        try {
            Assert.Empty( processes );
        }
        finally {
            foreach(var process in processes) {
                process.Dispose();
            }
        }
    }

    /// <inheritdoc />
    public void Dispose() {
        TerminateOwnedProcesses();

        if(this._succeeded && Directory.Exists( this.RootDirectory )) {
            Directory.Delete( this.RootDirectory, true );
        }
    }

    /// <summary>
    /// ディレクトリ内容を再帰的に複製する。
    /// </summary>
    /// <param name="sourceDirectory">複製元。</param>
    /// <param name="destinationDirectory">複製先。</param>
    private static void CopyDirectory( string sourceDirectory, string destinationDirectory ) {
        foreach(var sourcePath in Directory.EnumerateFiles( sourceDirectory, "*", SearchOption.AllDirectories )) {
            var relativePath = Path.GetRelativePath( sourceDirectory, sourcePath );
            var destinationPath = Path.Combine( destinationDirectory, relativePath );
            Directory.CreateDirectory( Path.GetDirectoryName( destinationPath )! );
            File.Copy( sourcePath, destinationPath, true );
        }
    }

    /// <summary>
    /// 更新先完全版ZIPから期待する更新マニフェストを読み取る。
    /// </summary>
    /// <param name="assetsDirectory">更新先アセットディレクトリ。</param>
    /// <param name="runtimeIdentifier">対象RID。</param>
    /// <returns>完全版ZIPに含まれる更新マニフェストを返す。</returns>
    private static JsonElement ReadTargetManifest( string assetsDirectory, string runtimeIdentifier ) {
        var packagePath = Path.Combine(
            assetsDirectory,
            $"DCS-Translation-Tool-1.2.0-{runtimeIdentifier}.zip" );
        using var archive = ZipFile.OpenRead( packagePath );
        var manifestEntry = archive.GetEntry( "update-manifest.json" )
            ?? throw new InvalidDataException( $"Target package manifest is missing: {packagePath}" );
        using var stream = manifestEntry.Open();
        using var document = JsonDocument.Parse( stream );
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 更新マニフェストの管理対象ファイルを相対パスで索引化する。
    /// </summary>
    /// <param name="manifest">索引化する更新マニフェスト。</param>
    /// <returns>管理対象パスとファイル要素の辞書を返す。</returns>
    private static Dictionary<string, JsonElement> GetManifestFiles( JsonElement manifest ) =>
        manifest.GetProperty( "files" )
            .EnumerateArray()
            .ToDictionary(
                file => file.GetProperty( "path" ).GetString()!,
                file => file,
                StringComparer.OrdinalIgnoreCase );

    /// <summary>
    /// 差分ZIPのマニフェストを意味的に破損させ、digestとsidecarを再生成する。
    /// </summary>
    /// <param name="assetsDirectory">アセットディレクトリ。</param>
    /// <param name="runtimeIdentifier">対象RID。</param>
    private static void CorruptDeltaPackage( string assetsDirectory, string runtimeIdentifier ) {
        var fileName = $"DCS-Translation-Tool-1.1.0-to-1.2.0-{runtimeIdentifier}.delta.zip";
        var packagePath = Path.Combine( assetsDirectory, fileName );
        using(var archive = ZipFile.Open( packagePath, ZipArchiveMode.Update )) {
            archive.GetEntry( "delta-manifest.json" )?.Delete();
            var entry = archive.CreateEntry( "delta-manifest.json" );
            using var writer = new StreamWriter( entry.Open(), new UTF8Encoding( false ) );
            writer.Write( "{}" );
        }

        var hash = Convert.ToHexString( SHA256.HashData( File.ReadAllBytes( packagePath ) ) ).ToLowerInvariant();
        File.WriteAllText( $"{packagePath}.sha256", $"{hash}  {fileName}\r\n", new UTF8Encoding( false ) );
    }

    /// <summary>
    /// 更新対象外として維持される利用者ファイルを配置する。
    /// </summary>
    /// <param name="applicationDirectory">アプリケーション展開先。</param>
    private static void SeedPreservedFiles( string applicationDirectory ) {
        Directory.CreateDirectory( Path.Combine( applicationDirectory, "Logs" ) );
        Directory.CreateDirectory( Path.Combine( applicationDirectory, "TranslateFile" ) );
        File.WriteAllText( Path.Combine( applicationDirectory, "Logs", "user.log" ), "user log" );
        File.WriteAllText( Path.Combine( applicationDirectory, "TranslateFile", "user.txt" ), "translation" );
        File.WriteAllText( Path.Combine( applicationDirectory, "user-note.txt" ), "unknown" );
    }

    /// <summary>
    /// 更新通知と競合する設定不足通知を発生させない最小設定を配置する。
    /// </summary>
    /// <param name="userDataDirectory">設定ファイルの保存先。</param>
    /// <param name="applicationDirectory">テスト用の有効なディレクトリ値。</param>
    private static void SeedApplicationSettings( string userDataDirectory, string applicationDirectory ) {
        var json = JsonSerializer.Serialize(
            new
            {
                SchemaVersion = 1,
                DcsWorldInstallDir = applicationDirectory,
                TranslateFileDir = Path.Combine( applicationDirectory, "TranslateFile" ),
                UseExternalAircraftInjectionDir = false,
                UseExternalCampaignInjectionDir = false
            },
            IndentedJsonOptions );
        File.WriteAllText(
            Path.Combine( userDataDirectory, "appsettings.json" ),
            $"{json}\r\n",
            new UTF8Encoding( false ) );
    }

    /// <summary>
    /// シナリオ配下の実行ファイルから起動しているプロセスを取得する。
    /// </summary>
    /// <returns>該当プロセスを返す。</returns>
    private List<Process> FindWorkspaceProcesses() {
        var processes = new List<Process>();
        foreach(var process in Process.GetProcessesByName( "DCS-Translation-Tool" )) {
            try {
                var executablePath = process.MainModule?.FileName;
                if(!string.IsNullOrWhiteSpace( executablePath ) && IsPathWithinRoot( executablePath, this.RootDirectory )) {
                    processes.Add( process );
                }
                else {
                    process.Dispose();
                }
            }
            catch(Exception ex) when(ex is InvalidOperationException or Win32Exception) {
                process.Dispose();
            }
        }

        return processes;
    }

    /// <summary>
    /// 指定パスがシナリオルート内にあるかを確認する。
    /// </summary>
    /// <param name="path">確認するパス。</param>
    /// <param name="root">シナリオルート。</param>
    /// <returns>ルート内の場合はtrueを返す。</returns>
    private static bool IsPathWithinRoot( string path, string root ) {
        var relativePath = Path.GetRelativePath( Path.GetFullPath( root ), Path.GetFullPath( path ) );
        return relativePath != ".." &&
            !relativePath.StartsWith( $"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal ) &&
            !Path.IsPathRooted( relativePath );
    }

    /// <summary>
    /// 所有プロセスが残っている場合に安全に終了する。
    /// </summary>
    /// <param name="processId">終了対象PID。</param>
    /// <param name="pathPredicate">終了を許可する実行ファイルパス条件。</param>
    private static void TryTerminateProcess( int processId, Func<string, bool> pathPredicate ) {
        try {
            using var process = Process.GetProcessById( processId );
            TryTerminateProcess( process, pathPredicate );
        }
        catch(ArgumentException) {
        }
        catch(InvalidOperationException) {
        }
    }

    /// <summary>
    /// 実行ファイルパスを直前に検証し、許可された所有プロセスだけを終了する。
    /// </summary>
    /// <param name="process">終了候補プロセス。</param>
    /// <param name="pathPredicate">終了を許可する実行ファイルパス条件。</param>
    private static void TryTerminateProcess( Process process, Func<string, bool> pathPredicate ) {
        try {
            var executablePath = process.MainModule?.FileName;
            if(string.IsNullOrWhiteSpace( executablePath ) || !pathPredicate( executablePath ) || process.HasExited) {
                return;
            }

            process.Kill( true );
            process.WaitForExit( 5000 );
        }
        catch(Exception ex) when(ex is InvalidOperationException or Win32Exception) {
        }
    }

    /// <summary>
    /// Windowsパスを大小文字を区別せず比較する。
    /// </summary>
    /// <param name="left">左辺パス。</param>
    /// <param name="right">右辺パス。</param>
    /// <returns>同一パスの場合はtrueを返す。</returns>
    private static bool PathsEqual( string left, string right ) =>
        string.Equals( Path.GetFullPath( left ), Path.GetFullPath( right ), StringComparison.OrdinalIgnoreCase );
}