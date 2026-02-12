using System.IO;

using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// パス正規化とセーフティ判定を提供する。
/// </summary>
public sealed class PathSafetyGuard : IPathSafetyGuard {
    private static readonly string[] ZipLikeExtensions = [".miz", ".trk"];

    /// <inheritdoc/>
    public bool TryResolvePathWithinRoot( string rootFullPath, string rootWithSeparator, string relativePath, out string resolvedPath ) {
        resolvedPath = string.Empty;
        if(string.IsNullOrWhiteSpace( relativePath )) {
            return false;
        }

        var candidate = Path.GetFullPath(
            Path.Combine( rootFullPath, relativePath.Replace( '/', Path.DirectorySeparatorChar ) )
        );

        if(!candidate.StartsWith( rootWithSeparator, StringComparison.OrdinalIgnoreCase )) {
            return false;
        }

        resolvedPath = candidate;
        return true;
    }

    /// <inheritdoc/>
    public int GetRootSegmentSkipCount( string[] segments ) {
        if(segments.Length == 0) {
            return 0;
        }

        if(string.Equals( segments[0], "DCSWorld", StringComparison.OrdinalIgnoreCase )) {
            if(segments.Length >= 3 && string.Equals( segments[1], "Mods", StringComparison.OrdinalIgnoreCase )) {
                return 3;
            }
        }

        if(string.Equals( segments[0], "UserMissions", StringComparison.OrdinalIgnoreCase )) {
            return 1;
        }

        return 0;
    }

    /// <inheritdoc/>
    public bool IsZipLikeEntrySegment( string segment ) =>
        ZipLikeExtensions.Any( ext => segment.EndsWith( ext, StringComparison.OrdinalIgnoreCase ) );
}