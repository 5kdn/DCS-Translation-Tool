#if E2E_TESTING
using System.Text.Json;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// E2Eテスト専用の更新構成を表す。
/// </summary>
public sealed record ApplicationUpdateE2eConfiguration(
    string ReleaseApiBaseUrl,
    string UpdatesDirectory,
    string CacheDirectory,
    string UserDataDirectory,
    string RunId
) {
    private const string ConfigurationEnvironmentVariable = "DCS_TRANSLATION_TOOL_E2E_CONFIG";

    private static readonly JsonSerializerOptions JsonOptions = new( JsonSerializerDefaults.Web )
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// 環境変数で指定されたJSONファイルから構成を読み込む。
    /// </summary>
    /// <returns>検証済みのE2E構成を返す。</returns>
    /// <exception cref="InvalidOperationException">構成が指定されていないか不正な場合に発生する。</exception>
    public static ApplicationUpdateE2eConfiguration Load() {
        var configurationPath = Environment.GetEnvironmentVariable( ConfigurationEnvironmentVariable );
        if(string.IsNullOrWhiteSpace( configurationPath )) {
            throw new InvalidOperationException( $"{ConfigurationEnvironmentVariable} is required for an E2E build." );
        }

        var fullPath = Path.GetFullPath( configurationPath );
        var configuration = JsonSerializer.Deserialize<ApplicationUpdateE2eConfiguration>(
            File.ReadAllText( fullPath ),
            JsonOptions );
        if(configuration is null ||
            !Uri.TryCreate( configuration.ReleaseApiBaseUrl, UriKind.Absolute, out var releaseApiUri ) ||
            releaseApiUri.Scheme != Uri.UriSchemeHttp ||
            !releaseApiUri.IsLoopback ||
            string.IsNullOrWhiteSpace( configuration.UpdatesDirectory ) ||
            string.IsNullOrWhiteSpace( configuration.CacheDirectory ) ||
            string.IsNullOrWhiteSpace( configuration.UserDataDirectory ) ||
            string.IsNullOrWhiteSpace( configuration.RunId )) {
            throw new InvalidOperationException( "The E2E configuration is invalid." );
        }

        return configuration with
        {
            ReleaseApiBaseUrl = releaseApiUri.AbsoluteUri,
            UpdatesDirectory = Path.GetFullPath( configuration.UpdatesDirectory ),
            CacheDirectory = Path.GetFullPath( configuration.CacheDirectory ),
            UserDataDirectory = Path.GetFullPath( configuration.UserDataDirectory )
        };
    }
}
#endif