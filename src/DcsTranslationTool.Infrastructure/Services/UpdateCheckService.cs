using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json;
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
    private const int ReleasesPerPage = 100;
    private const int MaximumReleasePages = 10;

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

            IReadOnlyList<ApplicationUpdateAsset> deltaAssets = [];
            if(currentVersion.Major == latestVersion.Major) {
                deltaAssets = await this.ResolveDeltaChainAsync(
                    currentVersion,
                    latestVersion,
                    payload,
                    cancellationToken ).ConfigureAwait( false );
            }

            var result = new UpdateCheckResult(
                true,
                currentVersion,
                latestVersion,
                latestVersionLabel,
                releaseUrl,
                this._runtimeIdentifier,
                true,
                fullAsset,
                deltaAssets );
            logger.Info(
                $"更新を検出した。Current={currentVersion}, Latest={latestVersionLabel}, " +
                $"DeltaCount={deltaAssets.Count}, DeltaBytes={result.DeltaDownloadSize}, " +
                $"FullBytes={result.FullDownloadSize}, Preferred={result.PreferredAssetKind}, Reason={result.SelectionReason}" );
            return result;
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
        var matchingAssets = assets?
            .Where( item => string.Equals( item.Name, expectedName, StringComparison.Ordinal ) )
            .Take( 2 )
            .ToArray();
        var checksumName = $"{expectedName}.sha256";
        var matchingChecksumAssets = assets?
            .Where( item => string.Equals( item.Name, checksumName, StringComparison.Ordinal ) )
            .Take( 2 )
            .ToArray();
        if(matchingAssets?.Length != 1 || matchingChecksumAssets?.Length != 1) {
            return null;
        }

        var asset = matchingAssets[0];
        var checksumAsset = matchingChecksumAssets[0];
        if(asset.Size <= 0 || !IsValidSha256Digest( asset.Digest ) ||
            checksumAsset.Size <= 0 || !IsValidSha256Digest( checksumAsset.Digest )) {
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

    /// <summary>
    /// Release履歴から現在版と最新版を結ぶ連続した差分アセットを解決する。
    /// </summary>
    private async Task<IReadOnlyList<ApplicationUpdateAsset>> ResolveDeltaChainAsync(
        Version currentVersion,
        Version latestVersion,
        GitHubReleasePayload latestRelease,
        CancellationToken cancellationToken
    ) {
        var releasesByVersion = new Dictionary<string, VersionedRelease>( StringComparer.Ordinal )
        {
            [FormatVersion( latestVersion )] = new( latestVersion, latestRelease )
        };
        var currentReleaseFound = false;

        try {
            for(var page = 1; page <= MaximumReleasePages; page++) {
                var requestUri =
                    $"repos/{ApplicationRepository.Owner}/{ApplicationRepository.Repo}/releases?per_page={ReleasesPerPage}&page={page}";
                using var request = new HttpRequestMessage( HttpMethod.Get, requestUri );
                request.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( GitHubMediaType ) );
                using var response = await this._httpClient.SendAsync( request, cancellationToken ).ConfigureAwait( false );
                if(!response.IsSuccessStatusCode) {
                    logger.Warn( $"Release履歴の取得に失敗したため完全版を使用する。StatusCode={(int)response.StatusCode}, Page={page}" );
                    return [];
                }

                var releases = await response.Content
                    .ReadFromJsonAsync<IReadOnlyList<GitHubReleasePayload>>( cancellationToken )
                    .ConfigureAwait( false );
                if(releases is null) {
                    logger.Warn( "Release履歴のレスポンスが不正なため完全版を使用する。" );
                    return [];
                }

                foreach(var release in releases) {
                    if(string.IsNullOrWhiteSpace( release.TagName ) ||
                        !TryNormalizeVersion( release.TagName, out var releaseVersion ) ||
                        releaseVersion.Major != currentVersion.Major ||
                        releaseVersion < currentVersion || releaseVersion > latestVersion) {
                        continue;
                    }

                    var key = FormatVersion( releaseVersion );
                    if(release.Draft || release.Prerelease || !release.Immutable) {
                        logger.Info( $"更新区間内に信頼できないReleaseがあるため完全版を使用する。Version={key}" );
                        return [];
                    }

                    if(releaseVersion == currentVersion) {
                        currentReleaseFound = true;
                        continue;
                    }

                    if(releaseVersion == latestVersion) {
                        continue;
                    }

                    if(!releasesByVersion.TryAdd( key, new VersionedRelease( releaseVersion, release ) )) {
                        logger.Warn( $"同一バージョンのReleaseが複数存在するため完全版を使用する。Version={key}" );
                        return [];
                    }

                    if(releasesByVersion.Count > UpdateCheckResult.MaximumDeltaChainLength) {
                        logger.Info( $"差分チェーンが上限を超えるため完全版を使用する。Count={releasesByVersion.Count}" );
                        return [];
                    }
                }

                if(currentReleaseFound) {
                    break;
                }

                if(releases.Count < ReleasesPerPage) {
                    break;
                }

                if(page == MaximumReleasePages) {
                    logger.Warn( $"Release履歴が取得上限を超えたため完全版を使用する。Pages={MaximumReleasePages}" );
                    return [];
                }
            }
        }
        catch(TaskCanceledException ex) when(!cancellationToken.IsCancellationRequested) {
            logger.Warn( "Release履歴の取得がタイムアウトしたため完全版を使用する。", ex );
            return [];
        }
        catch(HttpRequestException ex) {
            logger.Warn( "Release履歴の取得に失敗したため完全版を使用する。", ex );
            return [];
        }
        catch(Exception ex) when(ex is JsonException or NotSupportedException) {
            logger.Warn( "Release履歴を解釈できないため完全版を使用する。", ex );
            return [];
        }

        if(!currentReleaseFound) {
            logger.Info( $"現在版の安定したImmutable Releaseを確認できないため完全版を使用する。Current={FormatVersion( currentVersion )}" );
            return [];
        }

        var orderedReleases = releasesByVersion.Values
            .OrderBy( item => item.Version )
            .ToArray();
        if(orderedReleases.Length is 0 or > UpdateCheckResult.MaximumDeltaChainLength) {
            return [];
        }

        var expectedBaseVersion = currentVersion;
        var deltaAssets = new List<ApplicationUpdateAsset>( orderedReleases.Length );
        foreach(var release in orderedReleases) {
            var deltaAsset = ResolveDeltaAsset( release.Release, expectedBaseVersion, release.Version );
            if(deltaAsset is null) {
                logger.Info(
                    $"連続する差分アセットがないため完全版を使用する。Base={FormatVersion( expectedBaseVersion )}, " +
                    $"Target={FormatVersion( release.Version )}" );
                return [];
            }

            deltaAssets.Add( deltaAsset );
            expectedBaseVersion = release.Version;
        }

        return deltaAssets;
    }

    /// <summary>
    /// 指定した基準版と更新先に対応するRID別の差分アセットを解決する。
    /// </summary>
    private ApplicationUpdateAsset? ResolveDeltaAsset(
        GitHubReleasePayload release,
        Version baseVersion,
        Version targetVersion
    ) {
        var expectedName =
            $"{AssetNamePrefix}-{FormatVersion( baseVersion )}-to-{FormatVersion( targetVersion )}-{this._runtimeIdentifier}.delta.zip";
        return ResolveAsset(
            release.Assets,
            expectedName,
            ApplicationUpdateAssetKind.Delta,
            baseVersion,
            targetVersion );
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

    /// <summary>
    /// 正規化済みバージョンとRelease情報の組を表す。
    /// </summary>
    private sealed record VersionedRelease( Version Version, GitHubReleasePayload Release );
}