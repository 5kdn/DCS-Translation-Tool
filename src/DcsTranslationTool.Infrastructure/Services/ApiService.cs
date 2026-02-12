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
using DcsTranslationTool.Infrastructure.Http.ApiClient.Health;
using DcsTranslationTool.Shared.Models;

using FluentResults;

using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

using DcsApiClient = DcsTranslationTool.Infrastructure.Http.ApiClient.ApiClient;

namespace DcsTranslationTool.Infrastructure.Services;

/// <summary>APIクライアントを操作する</summary>
public class ApiService( HttpClient? httpClient = null ) : IApiService {
    private const string DefaultBaseUrl = "https://dcs-translation-japanese-cloudflare-worker.dcs-translation-japanese.workers.dev/";

    private static readonly JsonSerializerOptions SerializerOptions = new( JsonSerializerDefaults.Web )
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly ApiClientContext _apiClientContext = InitializeApiClientContext( httpClient );

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
        try {
            using var resp = await _apiClientContext.HttpClient.GetAsync( "tree", cancellationToken ).ConfigureAwait( false );
            if(!resp.IsSuccessStatusCode)
                return Result.Fail( ResultErrorFactory.External( $"HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}", "API_HTTP_ERROR" ) );

            var payload = await resp.Content.ReadFromJsonAsync<TreeResponse>( SerializerOptions, cancellationToken ).ConfigureAwait( false );
            if(payload?.Data is not { Count: > 0 } data)
                return Result.Ok<IReadOnlyList<FileEntry>>( Array.Empty<FileEntry>() );

            var entries = new List<FileEntry>( data.Count );
            foreach(var item in data) {
                if(item is null || string.IsNullOrWhiteSpace( item.Path )) continue;
                var name = ExtractName( item.Path );
                var isDirectory = IsDirectory( item );
                entries.Add( new FileEntry( name, item.Path, isDirectory, repoSha: item.Sha ) );
            }
            return Result.Ok<IReadOnlyList<FileEntry>>( entries );
        }
        catch(Exception ex) {
            return Result.Fail( ResultErrorFactory.Unexpected( ex, "API_TREE_EXCEPTION" ) );
        }
    }

    /// <summary>APIを呼び出して複数ファイルをZIPでダウンロードする</summary>
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
                    Array.Empty<byte>(),
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
                var cached = new ApiDownloadFilePathsResult( Array.Empty<ApiDownloadFilePathsItem>(), response.Headers.ETag?.Tag );
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
            var files = (request.Files ?? Array.Empty<ApiPullRequestFile>())
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
    /// <param name="httpClient">外部から供給される <see cref="HttpClient"/>。任意。</param>
    /// <returns>API呼び出しに必要な依存一式を返す。</returns>
    private static ApiClientContext InitializeApiClientContext( HttpClient? httpClient ) {
        var client = InitializeClient( httpClient );
        var requestAdapter = new HttpClientRequestAdapter( new AnonymousAuthenticationProvider(), httpClient: client );
        requestAdapter.BaseUrl = client.BaseAddress?.AbsoluteUri?.TrimEnd( '/' ) ?? DefaultBaseUrl.TrimEnd( '/' );
        var apiClient = new DcsApiClient( requestAdapter );
        return new ApiClientContext( client, requestAdapter, apiClient );
    }

    /// <summary>
    /// 渡された <see cref="HttpClient"/> を初期化する。未指定時は新規生成する。
    /// ベースURLとAcceptヘッダーを既定値に整える。
    /// </summary>
    /// <param name="httpClient">外部から供給される <see cref="HttpClient"/>。任意。</param>
    /// <returns>初期化済み <see cref="HttpClient"/>。</returns>
    private static HttpClient InitializeClient( HttpClient? httpClient ) {
        var client = httpClient ?? new HttpClient();

        client.BaseAddress ??= new Uri( DefaultBaseUrl );

        if(client.DefaultRequestHeaders.Accept.All( header => header.MediaType != "application/json" ))
            client.DefaultRequestHeaders.Accept.Add( new MediaTypeWithQualityHeaderValue( "application/json" ) );

        return client;
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
}