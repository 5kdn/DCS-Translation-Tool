using System.Net;
using System.Text;
using System.Text.Json;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.IntegrationTests.Infrastructure.Services.WireMock;
using DcsTranslationTool.Shared.Models;

using Moq;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

/// <summary>
/// <see cref="ApiService"/> の HTTP 契約を WireMock で検証する。
/// </summary>
public sealed class ApiServiceWireMockTests {
    /// <summary>
    /// health エンドポイントを実 HTTP で正しく解釈することを検証する。
    /// </summary>
    [Fact]
    public async Task GetHealthAsyncはWireMockレスポンスを正しく解釈する() {
        using var server = new WireMockTestServer();
        var timestamp = DateTimeOffset.Parse( "2026-01-01T00:00:00Z" );
        server.Server
            .Given( Request.Create().WithPath( "/health" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        $$"""
                        {
                          "status": "ok",
                          "timestamp": "{{timestamp:O}}"
                        }
                        """ ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.GetHealthAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( ApiHealthStatus.Ok, result.Value.Status );
        Assert.Equal( timestamp, result.Value.Timestamp );

        var request = RequireSingleRequest( server, "/health" );
        Assert.Equal( "GET", request.Method );
        Assert.Equal( "/health", request.Path );
    }

    /// <summary>
    /// tree エンドポイントの JSON を実 HTTP で正しくマッピングすることを検証する。
    /// </summary>
    [Fact]
    public async Task GetTreeAsyncはWireMockレスポンスを正しくマッピングする() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/tree" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "success": true,
                          "message": "ok",
                          "data": [
                            {
                              "mode": "040000",
                              "path": "translations",
                              "sha": "dir-sha",
                              "type": "tree"
                            },
                            {
                              "mode": "100644",
                              "path": "translations/file.po",
                              "sha": "file-sha",
                              "type": "blob"
                            }
                          ]
                        }
                        """ ) );

        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Default,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };
        var sut = CreateSut( server.CreateHttpClient(), settings: settings );

        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Collection(
            result.Value,
            directory => {
                Assert.Equal( "translations", directory.Name );
                Assert.Equal( "translations", directory.Path );
                Assert.True( directory.IsDirectory );
                Assert.Equal( "dir-sha", directory.RepoSha );
            },
            file => {
                Assert.Equal( "file.po", file.Name );
                Assert.Equal( "translations/file.po", file.Path );
                Assert.False( file.IsDirectory );
                Assert.Equal( "file-sha", file.RepoSha );
            } );

        var request = RequireSingleRequest( server, "/tree" );
        Assert.Equal( "GET", request.Method );
        Assert.Equal( "/tree", request.Path );
    }

    /// <summary>
    /// 優先経路失敗時にフォールバック経路へ再試行し優先経路を更新することを検証する。
    /// </summary>
    [Fact]
    public async Task GetTreeAsyncはWireMockでフォールバック経路へ再試行する() {
        using var defaultServer = new WireMockTestServer();
        using var ipv4Server = new WireMockTestServer();
        using var ipv6Server = new WireMockTestServer();

        ipv4Server.Server
            .Given( Request.Create().WithPath( "/tree" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.InternalServerError )
                    .WithBody( "ipv4 failed" ) );

        defaultServer.Server
            .Given( Request.Create().WithPath( "/tree" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "success": true,
                          "message": "ok",
                          "data": [
                            {
                              "mode": "100644",
                              "path": "fallback/path.lua",
                              "sha": "fallback-sha",
                              "type": "blob"
                            }
                          ]
                        }
                        """ ) );

        var settings = new AppSettings
        {
            ApiPreferredRoute = ApiRoutePreference.Ipv4,
            ApiPreferredRouteValidUntilUtc = DateTimeOffset.UtcNow.AddMinutes( 30 ),
        };
        var sut = CreateSut(
            defaultServer.CreateHttpClient(),
            ipv4Server.CreateHttpClient(),
            ipv6Server.CreateHttpClient(),
            settings );

        var result = await sut.GetTreeAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( "fallback/path.lua", result.Value.Single().Path );
        Assert.Equal( ApiRoutePreference.Default, settings.ApiPreferredRoute );

        var defaultRequest = RequireSingleRequest( defaultServer, "/tree" );
        Assert.Equal( "/tree", defaultRequest.Path );
        var ipv4Request = RequireSingleRequest( ipv4Server, "/tree" );
        Assert.Equal( "/tree", ipv4Request.Path );
        Assert.DoesNotContain( ipv6Server.Server.LogEntries, entry => string.Equals( entry.RequestMessage?.Path, "/tree", StringComparison.Ordinal ) );
    }

    /// <summary>
    /// download-files の HTTP 要求とレスポンスヘッダー反映を検証する。
    /// </summary>
    [Fact]
    public async Task DownloadFilesAsyncはWireMockでHTTP契約を満たす() {
        using var server = new WireMockTestServer();
        var content = Encoding.UTF8.GetBytes( "zip-binary-content" );
        server.Server
            .Given( Request.Create().WithPath( "/download-files" ).UsingPost() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/zip" )
                    .WithHeader( "Content-Disposition", "attachment; filename=\"translations.zip\"" )
                    .WithHeader( "ETag", "\"zip-etag\"" )
                    .WithBody( content ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.DownloadFilesAsync(
            new ApiDownloadFilesRequest( [" path/one ", "path/two"], null ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( ["path/one", "path/two"], result.Value.Paths );
        Assert.Equal( content, result.Value.Content );
        Assert.Equal( "application/zip", result.Value.ContentType );
        Assert.Equal( "translations.zip", result.Value.FileName );
        Assert.Equal( "\"zip-etag\"", result.Value.ETag );
        Assert.False( result.Value.IsNotModified );

        var request = RequireSingleRequest( server, "/download-files" );
        Assert.Equal( "POST", request.Method );
        Assert.Equal( "/download-files", request.Path );
        Assert.Contains( request.AcceptHeaders, value => value.Contains( "application/zip", StringComparison.Ordinal ) );
        Assert.Contains( request.AcceptHeaders, value => value.Contains( "application/problem+json", StringComparison.Ordinal ) );
        Assert.Equal( ["path/one", "path/two"], GetRequestPaths( request.Body ) );
    }

    /// <summary>
    /// download-files の 304 応答をキャッシュヒットとして扱うことを検証する。
    /// </summary>
    [Fact]
    public async Task DownloadFilesAsyncはWireMockで304をキャッシュヒットとして扱う() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/download-files" ).UsingPost() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.NotModified )
                    .WithHeader( "ETag", "\"etag-value\"" ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.DownloadFilesAsync(
            new ApiDownloadFilesRequest( [" path/one ", "path/two"], "\"etag-value\"" ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value.IsNotModified );
        Assert.Empty( result.Value.Content );
        Assert.Equal( "\"etag-value\"", result.Value.ETag );

        var request = RequireSingleRequest( server, "/download-files" );
        Assert.Contains(
            request.Headers["If-None-Match"],
            value => value.Contains( "\"etag-value\"", StringComparison.Ordinal ) );
        Assert.Equal( ["path/one", "path/two"], GetRequestPaths( request.Body ) );
    }

    /// <summary>
    /// download-file-paths の HTTP 要求とレスポンス解釈を検証する。
    /// </summary>
    [Fact]
    public async Task DownloadFilePathsAsyncはWireMockでHTTP契約を満たす() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/download-file-paths" ).UsingPost() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithHeader( "ETag", "\"header-etag\"" )
                    .WithBody(
                        """
                        {
                          "files": [
                            { "url": "https://example.test/raw/file1", "path": "path/one" },
                            { "url": "https://example.test/raw/file2", "path": "path/two" }
                          ],
                          "etag": "\"body-etag\""
                        }
                        """ ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( [" path/one ", "path/two"], null ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Equal( "\"header-etag\"", result.Value.ETag );
        Assert.Collection(
            result.Value.Items,
            first => {
                Assert.Equal( "https://example.test/raw/file1", first.Url );
                Assert.Equal( "path/one", first.Path );
            },
            second => {
                Assert.Equal( "https://example.test/raw/file2", second.Url );
                Assert.Equal( "path/two", second.Path );
            } );

        var request = RequireSingleRequest( server, "/download-file-paths" );
        Assert.Equal( "POST", request.Method );
        Assert.Equal( "/download-file-paths", request.Path );
        Assert.Contains( request.AcceptHeaders, value => value.Contains( "application/json", StringComparison.Ordinal ) );
        Assert.Contains( request.AcceptHeaders, value => value.Contains( "application/problem+json", StringComparison.Ordinal ) );
        Assert.Equal( ["path/one", "path/two"], GetRequestPaths( request.Body ) );
    }

    /// <summary>
    /// download-file-paths の 304 応答をキャッシュヒットとして扱うことを検証する。
    /// </summary>
    [Fact]
    public async Task DownloadFilePathsAsyncはWireMockで304をキャッシュヒットとして扱う() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/download-file-paths" ).UsingPost() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.NotModified )
                    .WithHeader( "ETag", "\"etag-value\"" ) );

        var sut = CreateSut( server.CreateHttpClient() );

        var result = await sut.DownloadFilePathsAsync(
            new ApiDownloadFilePathsRequest( [" path/one ", "path/two"], "\"etag-value\"" ),
            TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.Empty( result.Value.Items );
        Assert.Equal( "\"etag-value\"", result.Value.ETag );

        var request = RequireSingleRequest( server, "/download-file-paths" );
        Assert.Contains(
            request.Headers["If-None-Match"],
            value => value.Contains( "\"etag-value\"", StringComparison.Ordinal ) );
        Assert.Equal( ["path/one", "path/two"], GetRequestPaths( request.Body ) );
    }

    /// <summary>
    /// create-pr の送信 JSON とレスポンス変換を検証する。
    /// </summary>
    [Fact]
    public async Task CreatePullRequestAsyncはWireMockでHTTP契約を満たす() {
        using var server = new WireMockTestServer();
        server.Server
            .Given( Request.Create().WithPath( "/create-pr" ).UsingPost() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( (int)HttpStatusCode.OK )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "success": true,
                          "message": "created",
                          "data": [
                            {
                              "branchName": "feature/test",
                              "commitSha": "abc123",
                              "prNumber": 42,
                              "prUrl": "https://example.test/pr/42",
                              "note": "done"
                            }
                          ]
                        }
                        """ ) );

        var sut = CreateSut( server.CreateHttpClient() );
        var request = new ApiCreatePullRequestRequest(
            "feature/test",
            "feat: add files",
            "Add files",
            "Body",
            [
                new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "content/upsert.txt", "c29tZS1jb250ZW50" ),
                new ApiPullRequestFile( ApiPullRequestFileOperation.Delete, "content/remove.txt", null ),
                new ApiPullRequestFile( ApiPullRequestFileOperation.Upsert, "   ", "ignored" ),
            ] );

        var result = await sut.CreatePullRequestAsync( request, TestContext.Current.CancellationToken );

        Assert.True( result.IsSuccess );
        Assert.True( result.Value.Success );
        Assert.Equal( "created", result.Value.Message );
        Assert.Single( result.Value.Entries );
        Assert.Equal( 42, result.Value.Entries[0].PullRequestNumber );
        Assert.Equal( new Uri( "https://example.test/pr/42" ), result.Value.Entries[0].PullRequestUrl );

        var received = RequireSingleRequest( server, "/create-pr" );
        Assert.Equal( "POST", received.Method );
        Assert.Equal( "/create-pr", received.Path );

        using var body = JsonDocument.Parse( received.Body );
        var root = body.RootElement;
        Assert.Equal( "feature/test", root.GetProperty( "branchName" ).GetString() );
        Assert.Equal( "feat: add files", root.GetProperty( "commitMessage" ).GetString() );
        Assert.Equal( "Add files", root.GetProperty( "prTitle" ).GetString() );
        Assert.Equal( "Body", root.GetProperty( "prBody" ).GetString() );

        var files = root.GetProperty( "files" ).EnumerateArray().ToArray();
        Assert.Equal( 2, files.Length );
        Assert.Equal( "upsert", files[0].GetProperty( "operation" ).GetString() );
        Assert.Equal( "content/upsert.txt", files[0].GetProperty( "path" ).GetString() );
        Assert.Equal( "c29tZS1jb250ZW50", files[0].GetProperty( "content" ).GetString() );
        Assert.Equal( "delete", files[1].GetProperty( "operation" ).GetString() );
        Assert.Equal( "content/remove.txt", files[1].GetProperty( "path" ).GetString() );
        Assert.False( files[1].TryGetProperty( "content", out _ ) );
    }

    /// <summary>
    /// テスト対象サービスを生成する。
    /// </summary>
    /// <param name="defaultClient">既定経路の HTTP クライアント。</param>
    /// <param name="ipv4Client">IPv4 優先経路の HTTP クライアント。</param>
    /// <param name="ipv6Client">IPv6 優先経路の HTTP クライアント。</param>
    /// <param name="settings">設定オブジェクト。</param>
    /// <returns>生成した API サービスを返す。</returns>
    private static ApiService CreateSut(
        HttpClient defaultClient,
        HttpClient? ipv4Client = null,
        HttpClient? ipv6Client = null,
        AppSettings? settings = null ) {
        var provider = new TreeHttpClientProvider( defaultClient, ipv4Client, ipv6Client );
        var appSettingsServiceMock = new Mock<IAppSettingsService>();
        appSettingsServiceMock.SetupGet( x => x.Settings ).Returns( settings ?? new AppSettings() );
        return new ApiService( appSettingsServiceMock.Object, Mock.Of<ILoggingService>(), provider );
    }

    /// <summary>
    /// 指定パスに一致する単一の受信リクエストを取得する。
    /// </summary>
    /// <param name="server">対象 WireMock サーバー。</param>
    /// <param name="path">対象パス。</param>
    /// <returns>受信リクエスト情報を返す。</returns>
    private static ReceivedRequest RequireSingleRequest( WireMockTestServer server, string path ) {
        Assert.Contains( server.Server.LogEntries, item => string.Equals( item.RequestMessage?.Path, path, StringComparison.Ordinal ) );
        var entry = server.Server.LogEntries.Last( item => string.Equals( item.RequestMessage?.Path, path, StringComparison.Ordinal ) );
        var requestMessage = entry.RequestMessage!;
        var headers = requestMessage.Headers is null
            ? new Dictionary<string, string[]>( StringComparer.OrdinalIgnoreCase )
            : requestMessage.Headers.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select( value => value?.ToString() ?? string.Empty ).ToArray(),
                StringComparer.OrdinalIgnoreCase );
        var acceptHeaders = headers.TryGetValue( "Accept", out var acceptValues )
            ? acceptValues
            : [];

        return new ReceivedRequest(
            requestMessage.Method?.ToString() ?? string.Empty,
            requestMessage.Path?.ToString() ?? string.Empty,
            requestMessage.Body?.ToString() ?? string.Empty,
            headers,
            acceptHeaders );
    }

    /// <summary>
    /// リクエスト本文から paths 配列を抽出する。
    /// </summary>
    /// <param name="body">JSON 本文。</param>
    /// <returns>抽出した paths 配列を返す。</returns>
    private static string[] GetRequestPaths( string body ) {
        using var document = JsonDocument.Parse( body );
        return [.. document.RootElement
            .GetProperty( "paths" )
            .EnumerateArray()
            .Select( element => element.GetString() )
            .Where( value => !string.IsNullOrWhiteSpace( value ) )
            .Select( value => value! )];
    }

    /// <summary>
    /// 受信リクエストを表す。
    /// </summary>
    /// <param name="Method">HTTP メソッド。</param>
    /// <param name="Path">要求パス。</param>
    /// <param name="Body">要求本文。</param>
    /// <param name="Headers">ヘッダー一覧。</param>
    /// <param name="AcceptHeaders">Accept ヘッダー一覧。</param>
    private sealed record ReceivedRequest(
        string Method,
        string Path,
        string Body,
        IReadOnlyDictionary<string, string[]> Headers,
        IReadOnlyList<string> AcceptHeaders );
}