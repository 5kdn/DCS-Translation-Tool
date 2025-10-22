using System.Diagnostics;
using System.Reflection;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// アプリケーション情報を提供するサービス。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public sealed class ApplicationInfoService( ILoggingService logger ) : IApplicationInfoService {
    /// <summary>
    /// アプリケーションのバージョンを取得する。
    /// </summary>
    /// <returns>バージョン情報</returns>
    public Version GetVersion() {
        try {
            logger.Debug( "実行中アセンブリのバージョン情報を取得する。" );
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyLocation = assembly.Location;
            if(!string.IsNullOrWhiteSpace( assemblyLocation )) {
                var versionInfo = FileVersionInfo.GetVersionInfo(assemblyLocation);
                if(
                    !string.IsNullOrWhiteSpace( versionInfo.FileVersion ) &&
                    Version.TryParse( versionInfo.FileVersion, out var version )
                ) {
                    logger.Debug( $"FileVersionInfo からバージョンを解決した。Version={version}" );
                    return version;
                }
            }
            return assembly.GetName().Version ??
                throw new NullReferenceException( "アプリケーションのバージョン情報の取得に失敗しました。" );
        }
        catch(Exception ex) {
            logger.Warn( "アプリケーションのバージョン情報取得に失敗したため既定値を返す。", ex );
            return new Version( 0, 0, 0, 0 );
        }
    }
}