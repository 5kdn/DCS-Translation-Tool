using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Shared.Constants;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// GitHub Releases API を利用して更新確認を提供するサービスとする。
/// </summary>
/// <param name="applicationInfoService">ローカルアプリケーション情報サービスを受け取る。</param>
/// <param name="logger">ロギングサービスを受け取る。</param>
/// <param name="httpClient">外部から注入する HTTP クライアントを受け取る。</param>
public sealed class UpdateCheckService(
    IApplicationInfoService applicationInfoService,
    ILoggingService logger,
    HttpClient? httpClient = null
) : IUpdateCheckService {
    private const string GitHubApiBaseUrl = "https://api.github.com/";
    private const string GitHubMediaType = "application/vnd.github+json";
    private const string UserAgent = "DCS-Translation-Tool";

    private readonly HttpClient _httpClient = InitializeHttpClient( httpClient );

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdateAsync( CancellationToken cancellationToken = default ) {
        var currentVersion = NormalizeVersion( applicationInfoService.GetVersion() );
        var requestUri = $"repos/{ApplicationRepository.Owner}/{ApplicationRepository.Repo}/releases/latest";

        try {
            using var request = new HttpRequestMessage( HttpMethod.Get, requestUri );
            request.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( GitHubMediaType ) );

            using var response = await this._httpClient.SendAsync( request, cancellationToken ).ConfigureAwait( false );
            if(!response.IsSuccessStatusCode) {
                logger.Warn( $"更新確認 API 呼び出しに失敗した。StatusCode={(int)response.StatusCode}, Reason={response.ReasonPhrase}" );
                return UpdateCheckResult.NoUpdate;
            }

            var payload = await response.Content.ReadFromJsonAsync<GitHubReleasePayload>( cancellationToken ).ConfigureAwait( false );
            if(payload is null || string.IsNullOrWhiteSpace( payload.TagName )) {
                logger.Warn( "更新確認 API のレスポンスが不正なため通知を行わない。" );
                return UpdateCheckResult.NoUpdate;
            }

            if(payload is { Prerelease: true } or { Draft: true }) {
                logger.Info( $"プレリリースまたはドラフトを検出したため通知対象外とした。Tag={payload.TagName}" );
                return UpdateCheckResult.NoUpdate;
            }

            if(!TryNormalizeVersion( payload.TagName, out var latestVersion )) {
                logger.Warn( $"最新リリースのタグ解析に失敗した。Tag={payload.TagName}" );
                return UpdateCheckResult.NoUpdate;
            }

            if(latestVersion <= currentVersion) {
                logger.Info( $"更新不要と判定した。Current={currentVersion}, Latest={latestVersion}" );
                return UpdateCheckResult.NoUpdate;
            }

            var latestVersionLabel = NormalizeTagLabel( payload.TagName );
            var releaseUrl = ResolveReleaseUrl( payload.HtmlUrl, payload.TagName );
            logger.Info( $"更新を検出した。Current={currentVersion}, Latest={latestVersionLabel}" );
            return new UpdateCheckResult( true, latestVersionLabel, releaseUrl );
        }
        catch(TaskCanceledException ex) when(!cancellationToken.IsCancellationRequested) {
            logger.Warn( "更新確認 API 呼び出しがタイムアウトしたため通知を行わない。", ex );
            return UpdateCheckResult.NoUpdate;
        }
        catch(HttpRequestException ex) {
            logger.Warn( "更新確認 API 呼び出しで通信エラーが発生したため通知を行わない。", ex );
            return UpdateCheckResult.NoUpdate;
        }
        catch(Exception ex) {
            logger.Warn( "更新確認処理で予期しないエラーが発生したため通知を行わない。", ex );
            return UpdateCheckResult.NoUpdate;
        }
    }

    private static HttpClient InitializeHttpClient( HttpClient? httpClient ) {
        var client = httpClient ?? new HttpClient();
        client.BaseAddress ??= new Uri( GitHubApiBaseUrl );

        if(client.DefaultRequestHeaders.UserAgent.Count == 0) {
            client.DefaultRequestHeaders.UserAgent.Add( new ProductInfoHeaderValue( UserAgent, "1.0" ) );
        }

        if(client.DefaultRequestHeaders.Accept.All( header => header.MediaType != GitHubMediaType )) {
            client.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( GitHubMediaType ) );
        }

        return client;
    }

    private static string ResolveReleaseUrl( string? htmlUrl, string tagName ) {
        if(Uri.TryCreate( htmlUrl, UriKind.Absolute, out var releaseUrl )) {
            return releaseUrl.AbsoluteUri;
        }

        var trimmedTagName = tagName.Trim();
        return $"{ApplicationRepository.Url}/releases/tag/{Uri.EscapeDataString( trimmedTagName )}";
    }

    private static string NormalizeTagLabel( string tagName ) {
        var trimmed = tagName.Trim();
        return trimmed.StartsWith( "v", StringComparison.OrdinalIgnoreCase ) ? trimmed : $"v{trimmed}";
    }

    private static bool TryNormalizeVersion( string rawVersion, out Version version ) {
        var candidate = rawVersion.Trim();
        if(candidate.StartsWith( "v", StringComparison.OrdinalIgnoreCase )) {
            candidate = candidate[1..];
        }

        if(!Version.TryParse( candidate, out var parsedVersion )) {
            version = new Version( 0, 0, 0, 0 );
            return false;
        }

        version = NormalizeVersion( parsedVersion );
        return true;
    }

    private static Version NormalizeVersion( Version version ) {
        var build = version.Build < 0 ? 0 : version.Build;
        var revision = version.Revision < 0 ? 0 : version.Revision;
        return new Version( version.Major, version.Minor, build, revision );
    }

    private sealed record GitHubReleasePayload(
        [property: JsonPropertyName( "tag_name" )] string? TagName,
        [property: JsonPropertyName( "html_url" )] string? HtmlUrl,
        [property: JsonPropertyName( "draft" )] bool Draft,
        [property: JsonPropertyName( "prerelease" )] bool Prerelease
    );
}