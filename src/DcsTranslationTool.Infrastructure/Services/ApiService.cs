using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Results;
using DcsTranslationTool.Infrastructure.Http.ApiClient.CreatePr;
using DcsTranslationTool.Infrastructure.Http.ApiClient.DownloadFilePaths;
using DcsTranslationTool.Infrastructure.Http.ApiClient.DownloadFiles;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

using DcsApiClient = DcsTranslationTool.Infrastructure.Http.ApiClient.ApiClient;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>APIクライアントを操作する</summary>
public class ApiService(
    ILoggingService loggingService,
    ITreeHttpClientProvider treeHttpClientProvider
) : IApiService {
    private const string DefaultBaseUrl = "https://dcs-translation-japanese-cloudflare-worker.dcs-translation-japanese.workers.dev/";
    private const int TreePrimaryTimeoutSeconds = 10;
    private const int TreeFallbackTimeoutSeconds = 15;
    private static readonly TimeSpan Ipv4PreferenceTtl = TimeSpan.FromDays( 1 );

    private static readonly JsonSerializerOptions SerializerOptions = new( JsonSerializerDefaults.Web )
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiClientContext _apiClientContext = InitializeApiClientContext( treeHttpClientProvider.DefaultClient );
    private readonly ILoggingService _logger = loggingService;
    private readonly ITreeHttpClientProvider _treeHttpClientProvider = treeHttpClientProvider;
    private readonly string _ipv4PathPrefix = treeHttpClientProvider.IsIpv4PreferredDedicated ? "ipv4" : "ipv4(shared)";
    private readonly object _treeRouteGate = new();
    private TreeRoutePreference _treeRoutePreference = TreeRoutePreference.None;
    private DateTimeOffset _treeRoutePreferenceUntilUtc = DateTimeOffset.MinValue;

    /// <summary>APIのヘルスチェックを実行して結果を返却する</summary>
    public async Task<Result<ApiHealth>> GetHealthAsync( CancellationToken cancellationToken = default ) {
        try {
            using var response = await _apiClientContext.HttpClient.GetAsync( "health", cancellationToken ).ConfigureAwait( false );
            if(!response.IsSuccessStatusCode)
                return Result.Fail( ResultErrorFactory.External( $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", "API_HTTP_ERROR" ) );

            var payload = await response.Content.ReadFromJsonAsync<HealthResponse>( SerializerOptions, cancellationToken ).ConfigureAwait( false );
            if(payload is null)
                return Result.Fail( ResultErrorFactory.External( "Response body was null.", "API_EMPTY_RESPONSE" ) );

            var status = payload.Status switch
            {
                string value when string.Equals( value, "ok", StringComparison.OrdinalIgnoreCase ) => ApiHealthStatus.Ok,
                _ => ApiHealthStatus.Unknown,
            };

            return Result.Ok( new ApiHealth( status, payload.Timestamp ) );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_HEALTH_EXCEPTION" ) );
        }
    }

    /// <summary>APIを呼び出してツリー情報を取得しコレクションとして返却する</summary>
    public async Task<Result<IReadOnlyList<FileEntry>>> GetTreeAsync( CancellationToken cancellationToken = default ) {
        var routePreference = GetTreeRoutePreference();
        if(routePreference == TreeRoutePreference.Ipv4) {
            var preferredAttempt = await ExecuteTreeRequestAsync(
                _treeHttpClientProvider.Ipv4PreferredClient,
                $"{_ipv4PathPrefix}-preferred",
                TimeSpan.FromSeconds( TreeFallbackTimeoutSeconds ),
                cancellationToken
            ).ConfigureAwait( false );
            LogTreeAttempt( preferredAttempt );
            if(preferredAttempt.Result.IsSuccess) {
                LogTreeSummary( preferredAttempt );
                return preferredAttempt.Result;
            }

            var fallbackAttempt = await ExecuteTreeRequestAsync(
                _apiClientContext.HttpClient,
                "default-retry",
                TimeSpan.FromSeconds( TreeFallbackTimeoutSeconds ),
                cancellationToken
            ).ConfigureAwait( false );
            LogTreeAttempt( fallbackAttempt );
            if(fallbackAttempt.Result.IsSuccess) {
                SetTreeRoutePreference( TreeRoutePreference.Default, "IPv4 優先から default へ復帰したため default 優先を記憶する。" );
                LogTreeSummary( fallbackAttempt );
                return fallbackAttempt.Result;
            }

            LogTreeSummary( fallbackAttempt );
            return fallbackAttempt.Result;
        }

        if(routePreference == TreeRoutePreference.Default) {
            var preferredAttempt = await ExecuteTreeRequestAsync(
                _apiClientContext.HttpClient,
                "default-preferred",
                TimeSpan.FromSeconds( TreePrimaryTimeoutSeconds ),
                cancellationToken
            ).ConfigureAwait( false );
            LogTreeAttempt( preferredAttempt );
            if(preferredAttempt.Result.IsSuccess) {
                LogTreeSummary( preferredAttempt );
                return preferredAttempt.Result;
            }

            var fallbackAttempt = await ExecuteTreeRequestAsync(
                _treeHttpClientProvider.Ipv4PreferredClient,
                $"{_ipv4PathPrefix}-retry",
                TimeSpan.FromSeconds( TreeFallbackTimeoutSeconds ),
                cancellationToken
            ).ConfigureAwait( false );
            LogTreeAttempt( fallbackAttempt );
            if(fallbackAttempt.Result.IsSuccess) {
                SetTreeRoutePreference( TreeRoutePreference.Ipv4, "default 優先から IPv4 へ復帰したため IPv4 優先を記憶する。" );
                LogTreeSummary( fallbackAttempt );
                return fallbackAttempt.Result;
            }

            LogTreeSummary( fallbackAttempt );
            return fallbackAttempt.Result;
        }

        var racedAttempt = await ExecuteTreeRaceWithoutPreferenceAsync( cancellationToken ).ConfigureAwait( false );
        LogTreeSummary( racedAttempt );
        return racedAttempt.Result;
    }

    /// <summary>
    /// 優先経路が未確定のときに default と IPv4 経路を並列実行して先着成功を採用する。
    /// </summary>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>採用した試行結果。</returns>
    private async Task<TreeRequestAttempt> ExecuteTreeRaceWithoutPreferenceAsync( CancellationToken cancellationToken ) {
        using var raceCts = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken );
        var defaultTask = ExecuteTreeRequestAsync(
            _apiClientContext.HttpClient,
            "default-race",
            TimeSpan.FromSeconds( TreePrimaryTimeoutSeconds ),
            raceCts.Token
        );
        var ipv4Task = ExecuteTreeRequestAsync(
            _treeHttpClientProvider.Ipv4PreferredClient,
            $"{_ipv4PathPrefix}-race",
            TimeSpan.FromSeconds( TreeFallbackTimeoutSeconds ),
            raceCts.Token
        );

        var firstCompletedTask = await Task.WhenAny( defaultTask, ipv4Task ).ConfigureAwait( false );
        var firstAttempt = await firstCompletedTask.ConfigureAwait( false );
        LogTreeAttempt( firstAttempt );
        if(firstAttempt.Result.IsSuccess) {
            SetTreeRoutePreferenceByPath( firstAttempt.PathLabel, "初回レースで勝者経路を記憶する。" );
            var loserTask = ReferenceEquals( firstCompletedTask, defaultTask ) ? ipv4Task : defaultTask;
            await CancelAndObserveRaceLoserAsync( loserTask, raceCts ).ConfigureAwait( false );
            _logger.Info( $"GetTreeAsync レース結果。Winner={firstAttempt.PathLabel}" );
            return firstAttempt;
        }

        var secondTask = ReferenceEquals( firstCompletedTask, defaultTask ) ? ipv4Task : defaultTask;
        var secondAttempt = await secondTask.ConfigureAwait( false );
        LogTreeAttempt( secondAttempt );
        if(secondAttempt.Result.IsSuccess) {
            SetTreeRoutePreferenceByPath( secondAttempt.PathLabel, "初回レースで勝者経路を記憶する。" );
            _logger.Info( $"GetTreeAsync レース結果。Winner={secondAttempt.PathLabel}" );
            return secondAttempt;
        }

        _logger.Warn( $"GetTreeAsync レース結果。両経路が失敗した。First={firstAttempt.PathLabel}, Second={secondAttempt.PathLabel}" );
        return secondAttempt;
    }

    /// <summary>
    /// APIを呼び出して複数ファイルをZIPでダウンロードする
    /// </summary>
    public async Task<Result<ApiDownloadFilesResult>> DownloadFilesAsync(
        ApiDownloadFilesRequest request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( request );

        var sanitizeResult = ValidateAndSanitizePaths( request.Paths );
        if(sanitizeResult.IsFailed)
            return Result.Fail( sanitizeResult.Errors );

        var sanitizedPaths = sanitizeResult.Value;

        try {
            var requestInfo = _apiClientContext.ApiClient.DownloadFiles.ToPostRequestInformation(
                new DownloadFilesPostRequestBody
                {
                    Paths = [.. sanitizedPaths],
                },
                config => {
                    if(!string.IsNullOrWhiteSpace( request.ETag ))
                        config.Headers.TryAdd( "If-None-Match", request.ETag );
                }
            );

            using var message = await _apiClientContext.RequestAdapter
                .ConvertToNativeRequestAsync<HttpRequestMessage>( requestInfo, cancellationToken )
                .ConfigureAwait( false );
            if(message is null)
                return Result.Fail( ResultErrorFactory.External( "Request message was null.", "API_REQUEST_BUILD_ERROR" ) );

            message.Headers.Accept.Clear();
            message.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/zip" ) );
            message.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/problem+json" ) );
            if(!string.IsNullOrWhiteSpace( request.ETag ))
                message.Headers.TryAddWithoutValidation( "If-None-Match", request.ETag );

            using var response = await _apiClientContext.HttpClient
                .SendAsync( message, HttpCompletionOption.ResponseHeadersRead, cancellationToken )
                .ConfigureAwait( false );

            if(response.StatusCode == HttpStatusCode.NotModified) {
                var cached = new ApiDownloadFilesResult(
                    sanitizedPaths,
                    [],
                    0,
                    null,
                    null,
                    response.Headers.ETag?.Tag,
                    true
                );

                return Result.Ok( cached );
            }

            var ensureResult = await EnsureSuccessStatusCodeAsync( response, cancellationToken ).ConfigureAwait( false );
            if(ensureResult.IsFailed)
                return Result.Fail( ensureResult.Errors );

            var content = await response.Content.ReadAsByteArrayAsync( cancellationToken ).ConfigureAwait( false );
            var size = response.Content.Headers.ContentLength ?? content.LongLength;
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim( '"' );
            var etag = response.Headers.ETag?.Tag;

            var result = new ApiDownloadFilesResult(
                sanitizedPaths,
                content,
                size,
                contentType,
                fileName,
                etag,
                false
            );

            return Result.Ok( result );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_DOWNLOAD_FILES_EXCEPTION" ) );
        }
    }

    /// <summary>APIを呼び出して複数ファイルのダウンロードリンクを取得する</summary>
    public async Task<Result<ApiDownloadFilePathsResult>> DownloadFilePathsAsync(
        ApiDownloadFilePathsRequest request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( request );

        var sanitizeResult = ValidateAndSanitizePaths( request.Paths );
        if(sanitizeResult.IsFailed)
            return Result.Fail( sanitizeResult.Errors );

        var sanitizedPaths = sanitizeResult.Value;

        try {
            var requestInfo = _apiClientContext.ApiClient.DownloadFilePaths.ToPostRequestInformation(
                new DownloadFilePathsPostRequestBody
                {
                    Paths = [.. sanitizedPaths],
                },
                config => {
                    config.Headers.TryAdd( "Accept", "application/problem+json" );
                    if(!string.IsNullOrWhiteSpace( request.ETag ))
                        config.Headers.TryAdd( "If-None-Match", request.ETag );
                }
            );

            using var message = await _apiClientContext.RequestAdapter
                .ConvertToNativeRequestAsync<HttpRequestMessage>( requestInfo, cancellationToken )
                .ConfigureAwait( false );
            if(message is null)
                return Result.Fail( ResultErrorFactory.External( "Request message was null.", "API_REQUEST_BUILD_ERROR" ) );

            message.Headers.Accept.Clear();
            message.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );
            message.Headers.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/problem+json" ) );
            if(!string.IsNullOrWhiteSpace( request.ETag ))
                message.Headers.TryAddWithoutValidation( "If-None-Match", request.ETag );

            using var response = await _apiClientContext.HttpClient
                .SendAsync( message, HttpCompletionOption.ResponseHeadersRead, cancellationToken )
                .ConfigureAwait( false );

            if(response.StatusCode == HttpStatusCode.NotModified) {
                var cached = new ApiDownloadFilePathsResult( [], response.Headers.ETag?.Tag );
                return Result.Ok( cached );
            }

            var ensureResult = await EnsureSuccessStatusCodeAsync( response, cancellationToken ).ConfigureAwait( false );
            if(ensureResult.IsFailed)
                return Result.Fail( ensureResult.Errors );

            var payloadResponse = await response.Content
                .ReadFromJsonAsync<DownloadFilePathsPostResponse>( SerializerOptions, cancellationToken )
                .ConfigureAwait( false );

            if(payloadResponse is null)
                return Result.Fail( ResultErrorFactory.External( "Response body was null.", "API_EMPTY_RESPONSE" ) );

            var items = payloadResponse.Files?
                .Where( file => file is not null && !string.IsNullOrWhiteSpace( file.Url ) && !string.IsNullOrWhiteSpace( file.Path ) )
                .Select( file => new ApiDownloadFilePathsItem( file.Url!, file.Path! ) )
                .ToArray()
                ?? [];

            var etag = response.Headers.ETag?.Tag ?? payloadResponse.Etag;
            var result = new ApiDownloadFilePathsResult( items, etag );

            return Result.Ok( result );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_DOWNLOAD_PATHS_EXCEPTION" ) );
        }

    }

    /// <summary>APIを呼び出してPull Requestを作成する</summary>
    public async Task<Result<ApiCreatePullRequestOutcome>> CreatePullRequestAsync(
        ApiCreatePullRequestRequest request,
        CancellationToken cancellationToken = default
    ) {
        ArgumentNullException.ThrowIfNull( request );

        try {
            var files = (request.Files ?? [])
                .Where( file => file is not null && !string.IsNullOrWhiteSpace( file.Path ) )
                .Select( ToFilePayload )
                .Where( payload => payload is not null )
                .Select( payload => payload! )
                .ToList();

            var requestInfo = _apiClientContext.ApiClient.CreatePr.ToPostRequestInformation(
                new CreatePrPostRequestBody
                {
                    BranchName = request.BranchName,
                    CommitMessage = request.CommitMessage,
                    PrTitle = request.PrTitle,
                    PrBody = request.PrBody,
                    Files = files,
                }
            );

            using var message = await _apiClientContext.RequestAdapter
                .ConvertToNativeRequestAsync<HttpRequestMessage>( requestInfo, cancellationToken )
                .ConfigureAwait( false );
            if(message is null)
                return Result.Fail( ResultErrorFactory.External( "Request message was null.", "API_REQUEST_BUILD_ERROR" ) );

            using var response = await _apiClientContext.HttpClient
                .SendAsync( message, HttpCompletionOption.ResponseHeadersRead, cancellationToken )
                .ConfigureAwait( false );

            if(!response.IsSuccessStatusCode)
                return Result.Fail( ResultErrorFactory.External( $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", "API_HTTP_ERROR" ) );

            var result = await response.Content
                .ReadFromJsonAsync<CreatePrPostResponse>( SerializerOptions, cancellationToken )
                .ConfigureAwait( false );

            if(result is null)
                return Result.Fail( ResultErrorFactory.External( "Response body was null.", "API_EMPTY_RESPONSE" ) );

            var entries = result.Data?
                .Select( ToCreatePrEntry )
                .Where( entry => entry is not null )
                .Select( entry => entry! )
                .ToArray()
                ?? [];

            var outcome = new ApiCreatePullRequestOutcome( result.Success ?? false, result.Message, entries );
            return Result.Ok( outcome );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_CREATE_PR_EXCEPTION" ) );
        }
    }

    /// <summary>
    /// 渡された <see cref="HttpClient"/> と Kiota クライアント群を初期化する。
    /// </summary>
    /// <param name="httpClient">外部から供給される <see cref="HttpClient"/>。</param>
    /// <returns>API呼び出しに必要な依存一式を返す。</returns>
    private static ApiClientContext InitializeApiClientContext( HttpClient httpClient ) {
        var client = httpClient;
        var requestAdapter = new HttpClientRequestAdapter( new AnonymousAuthenticationProvider(), httpClient: client )
        {
            BaseUrl = client.BaseAddress?.AbsoluteUri?.TrimEnd( '/' ) ?? DefaultBaseUrl.TrimEnd( '/' )
        };
        var apiClient = new DcsApiClient( requestAdapter );
        return new ApiClientContext( client, requestAdapter, apiClient );
    }

    /// <summary>パス一覧を検証し、空白除去済み配列へ正規化する。</summary>
    /// <param name="paths">検証対象のパス一覧。</param>
    /// <returns>成功時は正規化済みパス配列、失敗時は検証エラーを返す。</returns>
    private static Result<string[]> ValidateAndSanitizePaths( IReadOnlyList<string>? paths ) {
        if(paths is null || paths.Count == 0)
            return Result.Fail( ResultErrorFactory.Validation( "Paths には少なくとも1つの値が含まれている必要があります。", "API_PATHS_REQUIRED" ) );

        var sanitizedPaths = paths
            .Select( path => path?.Trim() )
            .Where( path => !string.IsNullOrWhiteSpace( path ) )
            .Select( path => path! )
            .ToArray();

        if(sanitizedPaths.Length == 0)
            return Result.Fail( ResultErrorFactory.Validation( "Paths には少なくとも1つの空でない値が含まれている必要があります。", "API_PATHS_EMPTY" ) );

        if(sanitizedPaths.Length > 500)
            return Result.Fail( ResultErrorFactory.Validation( "Paths には500個以下のアイテムを含める必要があります。", "API_PATHS_LIMIT" ) );

        return Result.Ok( sanitizedPaths );
    }

    /// <summary>HTTPレスポンスの失敗を共通フォーマットでエラー変換する。</summary>
    /// <param name="response">判定対象のレスポンス。</param>
    /// <param name="cancellationToken">キャンセル通知。</param>
    /// <returns>成功ステータス時は成功、失敗ステータス時はExternalエラーを返す。</returns>
    private static async Task<Result> EnsureSuccessStatusCodeAsync( HttpResponseMessage response, CancellationToken cancellationToken ) {
        if(response.IsSuccessStatusCode)
            return Result.Ok();

        var reason = response.ReasonPhrase ?? $"HTTP {(int)response.StatusCode}";
        if(response.Content is not null) {
            var body = await response.Content.ReadAsStringAsync( cancellationToken ).ConfigureAwait( false );
            if(!string.IsNullOrWhiteSpace( body ))
                reason = $"{reason} - {body}";
        }

        return Result.Fail( ResultErrorFactory.External( reason, "API_HTTP_ERROR" ) );
    }

    /// <summary>パスの末尾要素から名前を抽出する。</summary>
    /// <param name="path">スラッシュ区切りのパス。</param>
    /// <returns>末尾のセグメント。セグメントが無い場合は入力値を返す。</returns>
    private static string ExtractName( string path ) {
        var segments = path.Split( '/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries );
        return segments.Length switch
        {
            0 => path,
            _ => segments[^1],
        };
    }

    /// <summary>ツリーエントリがディレクトリかを判定する。</summary>
    /// <param name="item">ツリーエントリ。</param>
    /// <returns>ディレクトリなら true。</returns>
    private static bool IsDirectory( TreeEntryPayload item ) =>
        item.Mode?.Equals( "040000", StringComparison.OrdinalIgnoreCase ) == true
        || item.Type?.Equals( "tree", StringComparison.OrdinalIgnoreCase ) == true;

    /// <summary>
    /// Tree API を呼び出し、処理結果と内訳を返す。
    /// </summary>
    /// <param name="client">呼び出しに使用する HTTP クライアント。</param>
    /// <param name="pathLabel">呼び出し経路ラベル。</param>
    /// <param name="timeout">試行タイムアウト。</param>
    /// <param name="cancellationToken">呼び出し元キャンセルトークン。</param>
    /// <returns>試行結果。</returns>
    private static async Task<TreeRequestAttempt> ExecuteTreeRequestAsync(
        HttpClient client,
        string pathLabel,
        TimeSpan timeout,
        CancellationToken cancellationToken
    ) {
        var totalStopwatch = Stopwatch.StartNew();
        long httpElapsedMs = 0;
        long deserializeElapsedMs = 0;
        long mapElapsedMs = 0;
        var rawCount = 0;
        var mappedCount = 0;
        int? statusCode = null;

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource( cancellationToken );
        timeoutCts.CancelAfter( timeout );

        try {
            var httpStopwatch = Stopwatch.StartNew();
            using var response = await client.GetAsync( "tree", timeoutCts.Token ).ConfigureAwait( false );
            httpElapsedMs = httpStopwatch.ElapsedMilliseconds;
            statusCode = (int)response.StatusCode;

            if(!response.IsSuccessStatusCode) {
                var error = ResultErrorFactory.External( $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}", "API_HTTP_ERROR" );
                return new TreeRequestAttempt(
                    pathLabel,
                    Result.Fail<IReadOnlyList<FileEntry>>( error ),
                    httpElapsedMs,
                    deserializeElapsedMs,
                    mapElapsedMs,
                    totalStopwatch.ElapsedMilliseconds,
                    rawCount,
                    mappedCount,
                    statusCode,
                    false
                );
            }

            var deserializeStopwatch = Stopwatch.StartNew();
            var payload = await response.Content.ReadFromJsonAsync<TreeResponse>( SerializerOptions, timeoutCts.Token ).ConfigureAwait( false );
            deserializeElapsedMs = deserializeStopwatch.ElapsedMilliseconds;

            if(payload?.Data is not { Count: > 0 } data) {
                return new TreeRequestAttempt(
                    pathLabel,
                    Result.Ok<IReadOnlyList<FileEntry>>( [] ),
                    httpElapsedMs,
                    deserializeElapsedMs,
                    mapElapsedMs,
                    totalStopwatch.ElapsedMilliseconds,
                    rawCount,
                    mappedCount,
                    statusCode,
                    false
                );
            }

            rawCount = data.Count;
            var mapStopwatch = Stopwatch.StartNew();
            var entries = new List<FileEntry>( data.Count );
            foreach(var item in data) {
                if(item is null || string.IsNullOrWhiteSpace( item.Path )) continue;
                var name = ExtractName( item.Path );
                var isDirectory = IsDirectory( item );
                entries.Add( new FileEntry( name, item.Path, isDirectory, repoSha: item.Sha ) );
            }

            mapElapsedMs = mapStopwatch.ElapsedMilliseconds;
            mappedCount = entries.Count;
            return new TreeRequestAttempt(
                pathLabel,
                Result.Ok<IReadOnlyList<FileEntry>>( entries ),
                httpElapsedMs,
                deserializeElapsedMs,
                mapElapsedMs,
                totalStopwatch.ElapsedMilliseconds,
                rawCount,
                mappedCount,
                statusCode,
                false
            );
        }
        catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested) {
            var error = ResultErrorFactory.External( $"Tree API がタイムアウトした。TimeoutMs={(int)timeout.TotalMilliseconds}", "API_TIMEOUT" );
            return new TreeRequestAttempt(
                pathLabel,
                Result.Fail<IReadOnlyList<FileEntry>>( error ),
                httpElapsedMs,
                deserializeElapsedMs,
                mapElapsedMs,
                totalStopwatch.ElapsedMilliseconds,
                rawCount,
                mappedCount,
                statusCode,
                true
            );
        }
        catch(Exception ex) {
            var error = ResultErrorFactory.Unexpected( ex, "API_TREE_EXCEPTION" );
            return new TreeRequestAttempt(
                pathLabel,
                Result.Fail<IReadOnlyList<FileEntry>>( error ),
                httpElapsedMs,
                deserializeElapsedMs,
                mapElapsedMs,
                totalStopwatch.ElapsedMilliseconds,
                rawCount,
                mappedCount,
                statusCode,
                false
            );
        }
    }

    /// <summary>
    /// Tree API の試行結果詳細をデバッグログへ記録する。
    /// </summary>
    /// <param name="attempt">記録対象の試行結果。</param>
    private void LogTreeAttempt( TreeRequestAttempt attempt ) {
        _logger.Debug(
            $"GetTreeAsync 内訳。Path={attempt.PathLabel}, StatusCode={attempt.StatusCode?.ToString() ?? "null"}, TimedOut={attempt.TimedOut}, HttpElapsedMs={attempt.HttpElapsedMs}, DeserializeElapsedMs={attempt.DeserializeElapsedMs}, MapElapsedMs={attempt.MapElapsedMs}, RawCount={attempt.RawCount}, MappedCount={attempt.MappedCount}, TotalElapsedMs={attempt.TotalElapsedMs}"
        );
    }

    /// <summary>
    /// Tree API の最終結果を情報ログへ記録する。
    /// </summary>
    /// <param name="attempt">最終試行結果。</param>
    private void LogTreeSummary( TreeRequestAttempt attempt ) {
        if(attempt.Result.IsSuccess) {
            _logger.Info( $"GetTreeAsync が完了した。Path={attempt.PathLabel}, Count={attempt.MappedCount}, TotalElapsedMs={attempt.TotalElapsedMs}" );
            return;
        }

        var reason = attempt.Result.Errors.Count > 0
            ? attempt.Result.Errors[0].Message
            : "unknown";
        _logger.Info( $"GetTreeAsync が失敗した。Path={attempt.PathLabel}, TotalElapsedMs={attempt.TotalElapsedMs}, Reason={reason}" );
    }

    /// <summary>
    /// レース勝者に応じて経路優先を記憶する。
    /// </summary>
    /// <param name="pathLabel">勝者パスラベル。</param>
    /// <param name="reason">切り替え理由。</param>
    private void SetTreeRoutePreferenceByPath( string pathLabel, string reason ) {
        if(pathLabel.StartsWith( "ipv4", StringComparison.OrdinalIgnoreCase )) {
            SetTreeRoutePreference( TreeRoutePreference.Ipv4, reason );
            return;
        }

        SetTreeRoutePreference( TreeRoutePreference.Default, reason );
    }

    /// <summary>
    /// 経路優先を有効化する。
    /// </summary>
    /// <param name="preference">有効化する優先経路。</param>
    /// <param name="reason">切り替え理由。</param>
    private void SetTreeRoutePreference( TreeRoutePreference preference, string reason ) {
        lock(_treeRouteGate) {
            _treeRoutePreference = preference;
            _treeRoutePreferenceUntilUtc = DateTimeOffset.UtcNow.Add( Ipv4PreferenceTtl );
        }

        _logger.Warn( $"{reason} Preference={preference}, 有効期限={_treeRoutePreferenceUntilUtc:O}" );
    }

    /// <summary>
    /// レース勝利時に敗者リクエストを取り消して完了を待機する。
    /// </summary>
    /// <param name="loserTask">敗者タスク。</param>
    /// <param name="raceCts">レース用トークンソース。</param>
    /// <returns>待機タスク。</returns>
    private async Task CancelAndObserveRaceLoserAsync( Task<TreeRequestAttempt> loserTask, CancellationTokenSource raceCts ) {
        raceCts.Cancel();
        try {
            var loserResult = await loserTask.ConfigureAwait( false );
            LogTreeAttempt( loserResult );
        }
        catch(OperationCanceledException) {
        }
        catch(Exception ex) {
            _logger.Debug( "GetTreeAsync レース敗者の終了待機で例外を補足した。", ex );
        }
    }

    /// <summary>
    /// 現在有効な経路優先を取得する。
    /// </summary>
    /// <returns>現在の優先経路。</returns>
    private TreeRoutePreference GetTreeRoutePreference() {
        lock(_treeRouteGate) {
            if(DateTimeOffset.UtcNow > _treeRoutePreferenceUntilUtc) {
                _treeRoutePreference = TreeRoutePreference.None;
                _treeRoutePreferenceUntilUtc = DateTimeOffset.MinValue;
            }

            return _treeRoutePreference;
        }
    }

    /// <summary>API用のファイル変更ペイロードに変換する。</summary>
    /// <param name="file">PRに含めるファイル情報。</param>
    /// <returns>変換結果。対応外の操作は null。</returns>
    private static CreatePrPostRequestBody.CreatePrPostRequestBody_files? ToFilePayload( ApiPullRequestFile file ) {
        ArgumentNullException.ThrowIfNull( file );

        return file.Operation switch
        {
            ApiPullRequestFileOperation.Upsert => new CreatePrPostRequestBody.CreatePrPostRequestBody_files
            {
                CreatePrPostRequestBodyFilesMember1 = new CreatePrPostRequestBody_filesMember1
                {
                    Operation = CreatePrPostRequestBody_filesMember1_operation.Upsert,
                    Path = file.Path,
                    Content = file.Content,
                },
            },
            ApiPullRequestFileOperation.Delete => new CreatePrPostRequestBody.CreatePrPostRequestBody_files
            {
                CreatePrPostRequestBodyFilesMember2 = new CreatePrPostRequestBody_filesMember2
                {
                    Operation = CreatePrPostRequestBody_filesMember2_operation.Delete,
                    Path = file.Path,
                },
            },
            _ => null,
        };
    }

    /// <summary>PR作成レスポンスのエントリをアプリ内モデルに変換する。</summary>
    /// <param name="payload">レスポンスの生データ。</param>
    /// <returns>アプリ内エントリ。<paramref name="payload"/> が null の場合は null。</returns>
    private static ApiCreatePullRequestEntry? ToCreatePrEntry( CreatePrPostResponse_data? payload ) {
        if(payload is null) return null;

        Uri? prUrl = null;
        if(payload.PrUrl is { Length: > 0 } url && Uri.TryCreate( url, UriKind.Absolute, out var parsed )) prUrl = parsed;

        return new ApiCreatePullRequestEntry( payload.BranchName, payload.CommitSha, payload.PrNumber, prUrl, payload.Note );
    }

    /// <summary>ヘルスチェック応答のペイロード。</summary>
    /// <param name="Status">サービス状態。"ok" など。</param>
    /// <param name="Timestamp">サーバー時刻。</param>
    private sealed record HealthResponse(
        [property: JsonPropertyName( "status" )] string? Status,
        [property: JsonPropertyName( "timestamp" )] DateTimeOffset? Timestamp
    );

    /// <summary>ツリー取得APIの応答ペイロード。</summary>
    /// <param name="Success">処理成否。</param>
    /// <param name="Message">メッセージ。</param>
    /// <param name="Data">ツリーエントリ一覧。</param>
    private sealed record TreeResponse(
        [property: JsonPropertyName( "success" )] bool? Success,
        [property: JsonPropertyName( "message" )] string? Message,
        [property: JsonPropertyName( "data" )] List<TreeEntryPayload>? Data
    );

    /// <summary>ツリーの1要素を表すペイロード。</summary>
    /// <param name="Mode">ファイルモード。ディレクトリは "040000"。</param>
    /// <param name="Path">リポジトリ内パス。</param>
    /// <param name="Sha">GitオブジェクトSHA。</param>
    /// <param name="Size">サイズ（ファイルのみ）。</param>
    /// <param name="Type">種別。"blob" または "tree"。</param>
    private sealed record TreeEntryPayload(
        [property: JsonPropertyName( "mode" )] string? Mode,
        [property: JsonPropertyName( "path" )] string? Path,
        [property: JsonPropertyName( "sha" )] string? Sha,
        [property: JsonPropertyName( "size" )] int? Size,
        [property: JsonPropertyName( "type" )] string? Type
    );

    /// <summary>APIクライアント依存をまとめる。</summary>
    /// <param name="HttpClient">HTTP送信用クライアント。</param>
    /// <param name="RequestAdapter">KiotaのHTTP変換アダプター。</param>
    /// <param name="ApiClient">Kiota生成のAPIクライアント。</param>
    private sealed record ApiClientContext( HttpClient HttpClient, HttpClientRequestAdapter RequestAdapter, DcsApiClient ApiClient );

    /// <summary>
    /// Tree API 試行結果を保持する。
    /// </summary>
    /// <param name="PathLabel">試行経路ラベル。</param>
    /// <param name="Result">実行結果。</param>
    /// <param name="HttpElapsedMs">HTTP 区間の経過時間。</param>
    /// <param name="DeserializeElapsedMs">逆シリアライズ区間の経過時間。</param>
    /// <param name="MapElapsedMs">マッピング区間の経過時間。</param>
    /// <param name="TotalElapsedMs">全体経過時間。</param>
    /// <param name="RawCount">受信件数。</param>
    /// <param name="MappedCount">変換件数。</param>
    /// <param name="StatusCode">HTTP ステータスコード。</param>
    /// <param name="TimedOut">タイムアウトで失敗したかどうか。</param>
    private sealed record TreeRequestAttempt(
        string PathLabel,
        Result<IReadOnlyList<FileEntry>> Result,
        long HttpElapsedMs,
        long DeserializeElapsedMs,
        long MapElapsedMs,
        long TotalElapsedMs,
        int RawCount,
        int MappedCount,
        int? StatusCode,
        bool TimedOut
    );

    /// <summary>
    /// Tree API 呼び出しで記憶する経路優先を表す。
    /// </summary>
    private enum TreeRoutePreference {
        None,
        Default,
        Ipv4,
    }
}