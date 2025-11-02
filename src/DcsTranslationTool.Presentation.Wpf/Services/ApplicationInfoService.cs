using System.Diagnostics;
using System.Reflection;

using DcsTranslationTool.Application.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// アプリケーション情報を提供するサービスとする。
/// </summary>
/// <param name="logger">ロギングサービスを受け取る。</param>
public sealed class ApplicationInfoService( ILoggingService logger ) : IApplicationInfoService {
    private static readonly Assembly TargetAssembly = typeof( App ).Assembly;

    /// <summary>
    /// DcsTranslationTool.Presentation.Wpf のバージョンを取得する。
    /// </summary>
    /// <returns>アプリケーションのバージョンを返す。</returns>
    public Version GetVersion() {
        try {
            logger.Debug( "DcsTranslationTool.Presentation.Wpf のバージョン情報を取得する。" );
            var assemblyLocation = TargetAssembly.Location;
            if(!string.IsNullOrWhiteSpace( assemblyLocation )) {
                var versionInfo = FileVersionInfo.GetVersionInfo( assemblyLocation );
                if(
                    !string.IsNullOrWhiteSpace( versionInfo.FileVersion ) &&
                    Version.TryParse( versionInfo.FileVersion, out var version )
                ) {
                    logger.Debug( $"FileVersionInfo からバージョンを解決した。Version={version}" );
                    return version;
                }
            }

            var assemblyVersion = TargetAssembly.GetName().Version;
            if(assemblyVersion is not null) {
                logger.Debug( $"AssemblyName からバージョンを解決した。Version={assemblyVersion}" );
                return assemblyVersion;
            }

            throw new NullReferenceException( "DcsTranslationTool.Presentation.Wpf のバージョン情報の取得に失敗した。" );
        }
        catch(Exception ex) {
            logger.Warn( "バージョン情報取得に失敗したため既定値を返す。", ex );
            return new Version( 0, 0, 0, 0 );
        }
    }
}