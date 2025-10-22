using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;

namespace DcsTranslationTool.Infrastructure.Providers;

/// <summary>
/// 環境変数を扱うプロバイダ。
/// </summary>
/// <param name="logger">ロギングサービス。</param>
public class EnvironmentProvider( ILoggingService logger ) : IEnvironmentProvider {
    /// <inheritdoc/>
    public string GetEnvironmentVariable( string variable ) {
        var value = Environment.GetEnvironmentVariable( variable ) ?? string.Empty;
        logger.Debug( $"環境変数を取得した。Name={variable}, HasValue={!string.IsNullOrEmpty( value )}" );
        return value;
    }

    /// <inheritdoc/>
    public string GetUserProfilePath() {
        var path = GetEnvironmentVariable( "UserProfile" );
        logger.Debug( "ユーザープロファイルパスを取得した。" );
        return path;
    }
}