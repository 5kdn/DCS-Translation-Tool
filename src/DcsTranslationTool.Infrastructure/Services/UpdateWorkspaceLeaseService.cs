using System.Text.RegularExpressions;

using DcsTranslationTool.Application.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// 更新作業領域の作成、使用中リース、およびクリーンアップを管理する。
/// </summary>
/// <param name="logger">ログ出力サービスを受け取る。</param>
/// <param name="updatesDirectory">更新作業ルートを受け取る。</param>
public sealed partial class UpdateWorkspaceLeaseService( ILoggingService logger, string updatesDirectory ) : IDisposable {
    private const string UpdatesRootGuardFileName = ".updates.lock";
    private const string WorkspaceLeaseFileName = ".in-use";

    private readonly string _updatesDirectory = Path.GetFullPath( updatesDirectory );
    private readonly Dictionary<string, FileStream> _workspaceLeases = new( StringComparer.OrdinalIgnoreCase );

    /// <summary>
    /// 更新作業ルートを取得する。
    /// </summary>
    public string UpdatesDirectory => this._updatesDirectory;

    /// <summary>
    /// 更新先とRIDに対応する一意な更新作業領域を作成し、親プロセス用リースを取得する。
    /// </summary>
    /// <param name="targetVersion">更新先バージョンを受け取る。</param>
    /// <param name="runtimeIdentifier">ランタイム識別子を受け取る。</param>
    /// <returns>作成した更新作業領域の絶対パスを返す。</returns>
    public string CreateWorkspaceDirectory( Version targetVersion, string runtimeIdentifier ) {
        ArgumentNullException.ThrowIfNull( targetVersion );
        ArgumentException.ThrowIfNullOrWhiteSpace( runtimeIdentifier );

        Directory.CreateDirectory( this._updatesDirectory );
        using var rootGuard = AcquireUpdatesRootGuard( this._updatesDirectory );
        var workspaceDirectory = Path.Combine(
            this._updatesDirectory,
            $"{targetVersion.ToString( 3 )}-{runtimeIdentifier}-{Guid.NewGuid():N}" );
        Directory.CreateDirectory( workspaceDirectory );
        this._workspaceLeases.Add( workspaceDirectory, OpenWorkspaceLease( workspaceDirectory ) );
        return workspaceDirectory;
    }

    /// <summary>
    /// 更新専用プロセス用の共有リースを取得する。
    /// </summary>
    /// <param name="workspaceDirectory">更新作業領域を受け取る。</param>
    /// <returns>破棄時にリースを解放するハンドルを返す。</returns>
    public IDisposable AcquireWorkspaceLease( string workspaceDirectory ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( workspaceDirectory );
        return OpenWorkspaceLease( Path.GetFullPath( workspaceDirectory ) );
    }

    /// <summary>
    /// 現在のプロセスが保持する更新作業領域のリースを解放する。
    /// </summary>
    /// <param name="workspaceDirectory">リースを解放する更新作業領域を受け取る。</param>
    public void ReleaseWorkspaceLease( string workspaceDirectory ) {
        var normalizedWorkspaceDirectory = Path.GetFullPath( workspaceDirectory );
        if(this._workspaceLeases.Remove( normalizedWorkspaceDirectory, out var lease )) {
            lease.Dispose();
        }
    }

    /// <summary>
    /// 更新作業領域が更新ルートに属し、命名規則と更新先が一致することを確認する。
    /// </summary>
    /// <param name="workspaceDirectory">確認する更新作業領域を受け取る。</param>
    /// <param name="targetVersion">期待する更新先バージョンを受け取る。</param>
    /// <param name="runtimeIdentifier">期待するランタイム識別子を受け取る。</param>
    /// <returns>更新サービスが所有する作業領域の場合はtrueを返す。</returns>
    public bool IsOwnedWorkspace( string workspaceDirectory, Version targetVersion, string runtimeIdentifier ) {
        var workspace = Path.GetFullPath( workspaceDirectory )
            .TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar );
        var parent = Path.GetDirectoryName( workspace );
        if(string.IsNullOrWhiteSpace( parent ) || !PathsEqual( parent, this._updatesDirectory )) {
            return false;
        }

        var match = WorkspaceDirectoryNameRegex().Match( Path.GetFileName( workspace ) );
        return match.Success &&
            Version.TryParse( match.Groups["version"].Value, out var workspaceVersion ) &&
            workspaceVersion.Major == targetVersion.Major &&
            workspaceVersion.Minor == targetVersion.Minor &&
            workspaceVersion.Build == targetVersion.Build &&
            string.Equals( match.Groups["rid"].Value, runtimeIdentifier, StringComparison.OrdinalIgnoreCase );
    }

    /// <summary>
    /// 完了済みの更新作業領域を削除し、他プロセスが保持するリースは尊重する。
    /// </summary>
    public void CleanupCompletedWorkspaces( string? preservedWorkspaceDirectory = null ) {
        if(!Directory.Exists( this._updatesDirectory )) {
            return;
        }

        using var rootGuard = AcquireUpdatesRootGuard( this._updatesDirectory );
        if(ContainsReparsePoint( this._updatesDirectory )) {
            throw new InvalidDataException( "更新作業ルートに再解析ポイントを使用できません。" );
        }

        foreach(var workspace in Directory.GetDirectories( this._updatesDirectory, "*", SearchOption.TopDirectoryOnly )) {
            if(!string.IsNullOrWhiteSpace( preservedWorkspaceDirectory ) && PathsEqual( workspace, preservedWorkspaceDirectory )) {
                continue;
            }

            if(!WorkspaceDirectoryNameRegex().IsMatch( Path.GetFileName( workspace ) )) {
                continue;
            }

            if(ContainsReparsePointInTree( workspace )) {
                logger.Warn( $"再解析ポイントを含む更新作業領域を削除しない。Workspace={workspace}" );
                continue;
            }

            if(IsWorkspaceInUse( workspace )) {
                logger.Info( $"使用中の更新作業領域をクリーンアップしない。Workspace={workspace}" );
                continue;
            }

            TryDeleteDirectory( workspace );
        }
    }

    /// <summary>
    /// 更新作業領域またはその配下に再解析ポイントが含まれることを確認する。
    /// </summary>
    /// <param name="root">確認するルートを受け取る。</param>
    /// <returns>再解析ポイントが含まれる場合はtrueを返す。</returns>
    public bool ContainsReparsePointInTree( string root ) => ContainsReparsePointInTreeCore( root );

    /// <inheritdoc />
    public void Dispose() {
        foreach(var lease in this._workspaceLeases.Values) {
            lease.Dispose();
        }
        this._workspaceLeases.Clear();
    }

    /// <summary>
    /// 更新作業ルートの作成とクリーンアップを直列化する排他ハンドルを取得する。
    /// </summary>
    /// <param name="updatesDirectory">更新作業ルートを受け取る。</param>
    /// <returns>排他ハンドルを返す。</returns>
    private static FileStream AcquireUpdatesRootGuard( string updatesDirectory ) => new(
        Path.Combine( updatesDirectory, UpdatesRootGuardFileName ),
        FileMode.OpenOrCreate,
        FileAccess.Read,
        FileShare.None );

    /// <summary>
    /// 更新作業領域が使用中であることを示す共有リースを取得する。
    /// </summary>
    /// <param name="workspaceDirectory">更新作業領域を受け取る。</param>
    /// <returns>共有リースのハンドルを返す。</returns>
    private static FileStream OpenWorkspaceLease( string workspaceDirectory ) => new(
        Path.Combine( workspaceDirectory, WorkspaceLeaseFileName ),
        FileMode.OpenOrCreate,
        FileAccess.Read,
        FileShare.Read );

    /// <summary>
    /// 更新作業領域が別プロセスで使用中かを確認する。
    /// </summary>
    /// <param name="workspaceDirectory">確認する更新作業領域を受け取る。</param>
    /// <returns>使用中の場合はtrueを返す。</returns>
    private static bool IsWorkspaceInUse( string workspaceDirectory ) {
        var leasePath = Path.Combine( workspaceDirectory, WorkspaceLeaseFileName );
        if(!File.Exists( leasePath )) {
            return false;
        }

        try {
            using var lease = new FileStream( leasePath, FileMode.Open, FileAccess.Read, FileShare.None );
            return false;
        }
        catch(IOException) {
            return true;
        }
        catch(UnauthorizedAccessException) {
            return true;
        }
    }

    private static bool ContainsReparsePoint( string path ) {
        var fullPath = Path.GetFullPath( path );
        if(!Directory.Exists( fullPath ) && !File.Exists( fullPath )) {
            return false;
        }

        return File.GetAttributes( fullPath ).HasFlag( FileAttributes.ReparsePoint );
    }

    private static bool ContainsReparsePointInTreeCore( string root ) {
        if(ContainsReparsePoint( root )) {
            return true;
        }

        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push( Path.GetFullPath( root ) );
        while(pendingDirectories.TryPop( out var directory )) {
            foreach(var path in Directory.GetFileSystemEntries( directory )) {
                if(ContainsReparsePoint( path )) {
                    return true;
                }
                if(Directory.Exists( path )) {
                    pendingDirectories.Push( path );
                }
            }
        }

        return false;
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

    private static bool PathsEqual( string left, string right ) =>
        string.Equals(
            Path.GetFullPath( left ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ),
            Path.GetFullPath( right ).TrimEnd( Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar ),
            StringComparison.OrdinalIgnoreCase );

    /// <summary>
    /// 更新ワークスペース名を検証する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex(
        "^(?<version>[0-9]+\\.[0-9]+\\.[0-9]+)-(?<rid>win-x64|win-x86)-[0-9a-f]{32}$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase )]
    private static partial Regex WorkspaceDirectoryNameRegex();
}