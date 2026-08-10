using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Shared.Constants;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>
/// GitHub Releases API を利用して更新確認を提供するサービスとする。
/// </summary>
/// <param name="applicationInfoService">ローカルアプリケーション情報サービスを受け取る。</param>
/// <param name="logger">ロギングサービスを受け取る。</param>
/// <param name="httpClient">外部から注入する HTTP クライアントを受け取る。</param>
public sealed partial class UpdateCheckService(
    IApplicationInfoService applicationInfoService,
    ILoggingService logger,
    HttpClient? httpClient = null,
    string? runtimeIdentifier = null
) : IUpdateCheckService {
    private const string GitHubApiBaseUrl = "https://api.github.com/";
    private const string GitHubMediaType = "application/vnd.github+json";
    private const string UserAgent = "DCS-Translation-Tool";
    private const string AssetNamePrefix = "DCS-Translation-Tool";

    private readonly HttpClient _httpClient = InitializeHttpClient( httpClient );
    private readonly string? _runtimeIdentifier = runtimeIdentifier ?? ResolveRuntimeIdentifier();

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdateAsync( CancellationToken cancellationToken = default ) {
        var currentVersion = NormalizeVersion( applicationInfoService.GetVersion() );
        var requestUri = $"repos/{ApplicationRepository.Owner}/{ApplicationRepository.Repo}/releases/latest";

        if(string.IsNullOrWhiteSpace( this._runtimeIdentifier )) {
            logger.Warn( "実行中アーキテクチャに対応する更新 RID を解決できないため通知を行わない。" );
            return UpdateCheckResult.NoUpdate;
        }

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

            if(!TryNormalizeVersion( payload.TagName, out var latestVersion )) {
                logger.Warn( $"最新リリースのタグ解析に失敗した。Tag={payload.TagName}" );
                return UpdateCheckResult.NoUpdate;
            }

            if(payload is { Prerelease: true } or { Draft: true } or { Immutable: false }) {
                logger.Info( $"プレリリース、ドラフト、または非 Immutable Release を検出したため通知対象外とした。Tag={payload.TagName}" );
                return UpdateCheckResult.NoUpdate;
            }

            if(latestVersion <= currentVersion) {
                logger.Info( $"更新不要と判定した。Current={currentVersion}, Latest={latestVersion}" );
                return UpdateCheckResult.NoUpdate;
            }

            var latestVersionLabel = NormalizeTagLabel( payload.TagName );
            var releaseUrl = ResolveReleaseUrl( payload.HtmlUrl, payload.TagName );
            var fullAssetName = $"{AssetNamePrefix}-{FormatVersion( latestVersion )}-{this._runtimeIdentifier}.zip";
            var fullAsset = ResolveAsset(
                payload.Assets,
                fullAssetName,
                ApplicationUpdateAssetKind.Full,
                null,
                latestVersion );
            if(fullAsset is null) {
                logger.Warn( $"現在の RID に対応する完全版更新アセットがないため通知を行わない。RID={this._runtimeIdentifier}, Asset={fullAssetName}" );
                return UpdateCheckResult.NoUpdate;
            }

            var deltaAssetName = $"{AssetNamePrefix}-{FormatVersion( currentVersion )}-to-{FormatVersion( latestVersion )}-{this._runtimeIdentifier}.delta.zip";
            var deltaAsset = ResolveAsset(
                payload.Assets,
                deltaAssetName,
                ApplicationUpdateAssetKind.Delta,
                currentVersion,
                latestVersion );
            logger.Info( $"更新を検出した。Current={currentVersion}, Latest={latestVersionLabel}" );
            return new UpdateCheckResult(
                true,
                currentVersion,
                latestVersion,
                latestVersionLabel,
                releaseUrl,
                this._runtimeIdentifier,
                true,
                fullAsset,
                deltaAsset );
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
#if E2E_TESTING
        client.BaseAddress ??= new Uri( ApplicationUpdateE2eConfiguration.Load().ReleaseApiBaseUrl );
#else
        client.BaseAddress ??= new Uri( GitHubApiBaseUrl );
#endif

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

    private static ApplicationUpdateAsset? ResolveAsset(
        IReadOnlyList<GitHubReleaseAssetPayload>? assets,
        string expectedName,
        ApplicationUpdateAssetKind kind,
        Version? baseVersion,
        Version targetVersion
    ) {
        var asset = assets?.SingleOrDefault( item => string.Equals( item.Name, expectedName, StringComparison.Ordinal ) );
        var checksumName = $"{expectedName}.sha256";
        var checksumAsset = assets?.SingleOrDefault( item => string.Equals( item.Name, checksumName, StringComparison.Ordinal ) );
        if(asset is null || asset.Size <= 0 || !IsValidSha256Digest( asset.Digest ) ||
            checksumAsset is null || checksumAsset.Size <= 0 || !IsValidSha256Digest( checksumAsset.Digest )) {
            return null;
        }

        if(!Uri.TryCreate( asset.BrowserDownloadUrl, UriKind.Absolute, out var downloadUri ) ||
            !IsTrustedAssetUri( downloadUri ) ||
            !Uri.TryCreate( checksumAsset.BrowserDownloadUrl, UriKind.Absolute, out var checksumDownloadUri ) ||
            !IsTrustedAssetUri( checksumDownloadUri )) {
            return null;
        }

        return new ApplicationUpdateAsset(
            expectedName,
            downloadUri.AbsoluteUri,
            asset.Size,
            asset.Digest!,
            kind,
            baseVersion,
            targetVersion,
            new ApplicationUpdateChecksumAsset(
                checksumName,
                checksumDownloadUri.AbsoluteUri,
                checksumAsset.Size,
                checksumAsset.Digest! ) );
    }

    private static bool IsValidSha256Digest( string? digest ) =>
        !string.IsNullOrWhiteSpace( digest ) &&
        Sha256DigestRegex().IsMatch( digest );

    private static bool IsTrustedAssetUri( Uri uri ) {
        if(uri.Scheme == Uri.UriSchemeHttps &&
            string.Equals( uri.Host, "github.com", StringComparison.OrdinalIgnoreCase )) {
            return true;
        }

#if E2E_TESTING
        return uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
#else
        return false;
#endif
    }

    private static string FormatVersion( Version version ) => version.ToString( 3 );

    private static string? ResolveRuntimeIdentifier() => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X64 => "win-x64",
        Architecture.X86 => "win-x86",
        _ => null
    };

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

    /// <summary>
    /// GitHub assetのSHA-256 digestを検証する正規表現を取得する。
    /// </summary>
    /// <returns>生成済みの正規表現を返す。</returns>
    [GeneratedRegex( "^sha256:[0-9a-fA-F]{64}$", RegexOptions.CultureInvariant )]
    private static partial Regex Sha256DigestRegex();

    private sealed record GitHubReleasePayload(
        [property: JsonPropertyName( "tag_name" )] string? TagName,
        [property: JsonPropertyName( "html_url" )] string? HtmlUrl,
        [property: JsonPropertyName( "draft" )] bool Draft,
        [property: JsonPropertyName( "prerelease" )] bool Prerelease,
        [property: JsonPropertyName( "immutable" )] bool Immutable,
        [property: JsonPropertyName( "assets" )] IReadOnlyList<GitHubReleaseAssetPayload>? Assets
    );

    private sealed record GitHubReleaseAssetPayload(
        [property: JsonPropertyName( "name" )] string? Name,
        [property: JsonPropertyName( "browser_download_url" )] string? BrowserDownloadUrl,
        [property: JsonPropertyName( "size" )] long Size,
        [property: JsonPropertyName( "digest" )] string? Digest
    );
}