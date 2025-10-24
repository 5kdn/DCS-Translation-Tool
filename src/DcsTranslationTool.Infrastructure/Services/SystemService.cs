using System.Diagnostics;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// システム関連の操作を提供するサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
/// <param name="processLauncher">プロセス起動サービス。</param>
public sealed class SystemService( ILoggingService logger, IProcessLauncher processLauncher ) : ISystemService {
    /// <inheritdoc/>
    public void OpenInWebBrowser( string url ) {
        ArgumentException.ThrowIfNullOrWhiteSpace( url );

        try {
            var psi = new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            };
            processLauncher.Start( psi );
        }
        catch(Exception ex) {
            logger.Error( $"既定ブラウザーでの URL オープンに失敗した。Url={url}", ex );
            throw;
        }

        logger.Info( $"既定ブラウザーで URL を開いた。Url={url}" );
    }

    /// <inheritdoc/>
    public void OpenDirectory( string path ) {
        if(!Directory.Exists( path )) {
            if(File.Exists( path ) && Path.GetDirectoryName( path ) is string p) {
                path = p;
            }
            else {
                throw new DirectoryNotFoundException( path );
            }
        }
        var psi = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = path,
            UseShellExecute = true
        };
        try {
            processLauncher.Start( psi );
            logger.Info( $"エクスプローラーでディレクトリを開いた。Path={path}" );
        }
        catch(Exception ex) {
            logger.Error( $"エクスプローラーでのディレクトリオープンに失敗した。Path={path}", ex );
            throw;
        }
    }

}