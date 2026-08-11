using System.Net;
using System.Text;
using System.Text.Json;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Application.Models;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.TestCommon.Http;

using Moq;

namespace DcsTranslationTool.Infrastructure.UnitTests.Services;

/// <summary>
/// <see cref="UpdateCheckService"/> の更新判定挙動を検証する。
/// </summary>
public sealed class UpdateCheckServiceTests {
    [Fact]
    public async Task CheckForUpdateAsyncは最新版が存在する場合に更新ありを返す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoServiceMock = CreateAppInfoServiceMock();
        var requestedPaths = new List<string>();
        var httpClient = CreateClient( (request, _) => {
            requestedPaths.Add( request.RequestUri!.PathAndQuery );
            if(!request.RequestUri.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )) {
                return Task.FromResult( CreateJsonResponse( JsonSerializer.Serialize( new[]
                {
                    CreateReleasePayload( "1.3.1" )
                } ) ) );
            }

            Assert.Equal( "/repos/5kdn/DCS-Translation-Tool/releases/latest", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent(
                """
                {
                  "tag_name": "v1.4.0",
                  "html_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
                  "draft": false,
                  "prerelease": false,
                  "immutable": true,
                  "assets": [
                    {
                      "name": "DCS-Translation-Tool-1.4.0-win-x64.zip",
                      "browser_download_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/download/v1.4.0/DCS-Translation-Tool-1.4.0-win-x64.zip",
                      "size": 120000000,
                      "digest": "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"
                    },
                    {
                      "name": "DCS-Translation-Tool-1.4.0-win-x64.zip.sha256",
                      "browser_download_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/download/v1.4.0/DCS-Translation-Tool-1.4.0-win-x64.zip.sha256",
                      "size": 120,
                      "digest": "sha256:cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                    },
                    {
                      "name": "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip",
                      "browser_download_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/download/v1.4.0/DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip",
                      "size": 20000000,
                      "digest": "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                    },
                    {
                      "name": "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip.sha256",
                      "browser_download_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/download/v1.4.0/DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip.sha256",
                      "size": 120,
                      "digest": "sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
                    }
                  ]
                }
                """,
                Encoding.UTF8,
                "application/json" )
            } );
        } );

        var sut = new UpdateCheckService( appInfoServiceMock.Object, loggerMock.Object, httpClient, "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsUpdateAvailable );
        Assert.Equal( "v1.4.0", result.LatestVersionLabel );
        Assert.Equal( "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0", result.ReleaseUrl );
        Assert.True( result.IsImmutable );
        Assert.Equal( "win-x64", result.RuntimeIdentifier );
        Assert.Equal( "DCS-Translation-Tool-1.4.0-win-x64.zip", result.FullAsset?.Name );
        Assert.Equal( "DCS-Translation-Tool-1.4.0-win-x64.zip.sha256", result.FullAsset?.Checksum.Name );
        Assert.Equal( "DCS-Translation-Tool-1.3.1-to-1.4.0-win-x64.delta.zip", Assert.Single( result.DeltaAssets ).Name );
        Assert.Equal( ApplicationUpdateAssetKind.Delta, result.PreferredAssetKind );
        Assert.Contains( "/repos/5kdn/DCS-Translation-Tool/releases?per_page=100&page=1", requestedPaths );
    }

    /// <summary>
    /// 複数の安定版Releaseから連続した差分チェーンを構築することを確認する。
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsyncは複数Releaseから差分チェーンを構築する() {
        var requestedPaths = new List<string>();
        var latest = CreateReleasePayload(
            "2.2.0",
            CreateFullAssetPayload( "2.2.0", 900, 100 ),
            CreateDeltaAssetPayload( "2.1.0", "2.2.0", 300, 50 ) );
        var intermediate = CreateReleasePayload(
            "2.1.0",
            CreateFullAssetPayload( "2.1.0", 900, 100 ),
            CreateDeltaAssetPayload( "2.0.0", "2.1.0", 300, 50 ) );
        var current = CreateReleasePayload( "2.0.0" );
        var httpClient = CreateClient( (request, _) => {
            requestedPaths.Add( request.RequestUri!.PathAndQuery );
            var json = request.RequestUri.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )
                ? JsonSerializer.Serialize( latest )
                : JsonSerializer.Serialize( new[] { latest, intermediate, current } );
            return Task.FromResult( CreateJsonResponse( json ) );
        } );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 0, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Equal( 2, result.DeltaAssets.Count );
        Assert.Equal( "2.0.0", result.DeltaAssets[0].BaseVersion?.ToString( 3 ) );
        Assert.Equal( "2.1.0", result.DeltaAssets[0].TargetVersion.ToString( 3 ) );
        Assert.Equal( "2.2.0", result.DeltaAssets[1].TargetVersion.ToString( 3 ) );
        Assert.True( result.IsDeltaPreferred );
        Assert.Contains( "/repos/5kdn/DCS-Translation-Tool/releases?per_page=100&page=1", requestedPaths );
    }

    /// <summary>
    /// 現在版Releaseを履歴で確認できない場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsyncは現在版Releaseが履歴にない場合に完全版を返す() {
        var latest = CreateReleasePayload(
            "2.1.0",
            CreateFullAssetPayload( "2.1.0", 900, 100 ),
            CreateDeltaAssetPayload( "2.0.0", "2.1.0", 100, 50 ) );
        var httpClient = CreateClient( (request, _) => Task.FromResult( CreateJsonResponse(
            JsonSerializer.Serialize(
                request.RequestUri!.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )
                    ? latest
                    : new[] { latest } ) ) ) );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 0, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Empty( result.DeltaAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.PreferredAssetKind );
    }

    /// <summary>
    /// 更新区間内に信頼できないReleaseがある場合に完全版を選択することを確認する。
    /// </summary>
    /// <param name="prerelease">途中のReleaseがprereleaseかを示す。</param>
    /// <param name="immutable">途中のReleaseがimmutableかを示す。</param>
    [Theory]
    [InlineData( false, false )]
    [InlineData( true, true )]
    public async Task CheckForUpdateAsyncは更新区間内に信頼できないReleaseがある場合に完全版を返す(
        bool prerelease,
        bool immutable
    ) {
        var latest = CreateReleasePayload(
            "2.2.0",
            CreateFullAssetPayload( "2.2.0", 900, 100 ),
            CreateDeltaAssetPayload( "2.0.0", "2.2.0", 100, 50 ) );
        var untrusted = CreateReleasePayload( "2.1.0", false, prerelease, immutable );
        var current = CreateReleasePayload( "2.0.0" );
        var httpClient = CreateClient( (request, _) => Task.FromResult( CreateJsonResponse(
            JsonSerializer.Serialize(
                request.RequestUri!.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )
                    ? latest
                    : new[] { latest, untrusted, current } ) ) ) );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 0, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Empty( result.DeltaAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.PreferredAssetKind );
    }

    /// <summary>
    /// 利用可能な隣接差分アセットがない場合に完全版を選択することを確認する。
    /// </summary>
    /// <param name="includeWrongRidAsset">異なるRIDの差分アセットを含めるかを示す。</param>
    [Theory]
    [InlineData( false )]
    [InlineData( true )]
    public async Task CheckForUpdateAsyncは利用可能な隣接差分がない場合に完全版を返す( bool includeWrongRidAsset ) {
        var latest = CreateReleasePayload(
            "2.2.0",
            CreateFullAssetPayload( "2.2.0", 900, 100 ),
            CreateDeltaAssetPayload( "2.1.0", "2.2.0", 100, 50 ) );
        var intermediateAssets = includeWrongRidAsset
            ? new[]
            {
                CreateAssetPayload(
                    "2.1.0",
                    "DCS-Translation-Tool-2.0.0-to-2.1.0-win-x86.delta.zip",
                    100,
                    50 )
            }
            : [];
        var intermediate = CreateReleasePayload( "2.1.0", intermediateAssets );
        var current = CreateReleasePayload( "2.0.0" );
        var httpClient = CreateClient( (request, _) => Task.FromResult( CreateJsonResponse(
            JsonSerializer.Serialize(
                request.RequestUri!.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )
                    ? latest
                    : new[] { latest, intermediate, current } ) ) ) );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 0, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Empty( result.DeltaAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.PreferredAssetKind );
    }

    /// <summary>
    /// 差分アセットのdigestが不正な場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsyncは差分digestが不正な場合に完全版を返す() {
        var deltaName = "DCS-Translation-Tool-2.0.0-to-2.1.0-win-x64.delta.zip";
        var latest = CreateReleasePayload(
            "2.1.0",
            CreateFullAssetPayload( "2.1.0", 900, 100 ),
            CreateAssetPayload( "2.1.0", deltaName, 100, 50, "invalid" ) );
        var current = CreateReleasePayload( "2.0.0" );
        var httpClient = CreateClient( (request, _) => Task.FromResult( CreateJsonResponse(
            JsonSerializer.Serialize(
                request.RequestUri!.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )
                    ? latest
                    : new[] { latest, current } ) ) ) );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 0, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Empty( result.DeltaAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.PreferredAssetKind );
    }

    /// <summary>
    /// majorが異なる場合にRelease履歴を取得せず完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsyncはmajorが異なる場合に履歴を取得せず完全版を返す() {
        var requestCount = 0;
        var latest = CreateReleasePayload(
            "3.0.0",
            CreateFullAssetPayload( "3.0.0", 900, 100 ),
            CreateDeltaAssetPayload( "2.9.0", "3.0.0", 100, 50 ) );
        var httpClient = CreateClient( (request, _) => {
            requestCount++;
            Assert.EndsWith( "/releases/latest", request.RequestUri!.AbsolutePath, StringComparison.Ordinal );
            return Task.FromResult( CreateJsonResponse( JsonSerializer.Serialize( latest ) ) );
        } );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 9, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Empty( result.DeltaAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.PreferredAssetKind );
        Assert.Equal( 1, requestCount );
    }

    /// <summary>
    /// 差分チェーンが6本必要な場合に完全版を選択することを確認する。
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsyncは差分チェーンが6本の場合に完全版を返す() {
        var releases = Enumerable.Range( 1, 6 )
            .Select( minor => CreateReleasePayload(
                $"2.{minor}.0",
                CreateFullAssetPayload( $"2.{minor}.0", 900, 100 ),
                CreateDeltaAssetPayload( $"2.{minor - 1}.0", $"2.{minor}.0", 50, 10 ) ) )
            .Reverse()
            .ToArray();
        var httpClient = CreateClient( (request, _) => Task.FromResult( CreateJsonResponse(
            JsonSerializer.Serialize(
                request.RequestUri!.AbsolutePath.EndsWith( "/latest", StringComparison.Ordinal )
                    ? releases[0]
                    : releases ) ) ) );
        var sut = new UpdateCheckService(
            CreateAppInfoServiceMock( new Version( 2, 0, 0 ) ).Object,
            Mock.Of<ILoggingService>(),
            httpClient,
            "win-x64" );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.Empty( result.DeltaAssets );
        Assert.Equal( ApplicationUpdateAssetKind.Full, result.PreferredAssetKind );
    }

    [Fact]
    public async Task CheckForUpdateAsyncは同一バージョンの場合に更新なしを返す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoServiceMock = CreateAppInfoServiceMock();
        var httpClient = CreateClient( (_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
        {
            Content = new StringContent(
                """
                {
                  "tag_name": "v1.3.1",
                  "html_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.3.1",
                  "draft": false,
                  "prerelease": false
                }
                """,
                Encoding.UTF8,
                "application/json" )
        } ) );

        var sut = new UpdateCheckService( appInfoServiceMock.Object, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
    }

    [Fact]
    public async Task CheckForUpdateAsyncはタグ形式差異を同一系列として判定する() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoServiceMock = CreateAppInfoServiceMock();
        var httpClient = CreateClient( (_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
        {
            Content = new StringContent(
                """
                {
                  "tag_name": "1.3.1",
                  "html_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.3.1",
                  "draft": false,
                  "prerelease": false
                }
                """,
                Encoding.UTF8,
                "application/json" )
        } ) );

        var sut = new UpdateCheckService( appInfoServiceMock.Object, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
    }

    [Fact]
    public async Task CheckForUpdateAsyncはHttp失敗時に更新なしを返して警告ログを出す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoServiceMock = CreateAppInfoServiceMock();
        var httpClient = CreateClient( (_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError )
        {
            ReasonPhrase = "Server Error"
        } ) );

        var sut = new UpdateCheckService( appInfoServiceMock.Object, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
        loggerMock.Verify( logger => logger.Warn(
            It.IsAny<string>(),
            It.IsAny<Exception?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<int>() ), Times.AtLeastOnce );
    }

    [Fact]
    public async Task CheckForUpdateAsyncはタイムアウト時に更新なしを返して警告ログを出す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoServiceMock = CreateAppInfoServiceMock();
        var httpClient = CreateClient( (_, _) => throw new TaskCanceledException( "timeout" ) );

        var sut = new UpdateCheckService( appInfoServiceMock.Object, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
        loggerMock.Verify( logger => logger.Warn(
            It.IsAny<string>(),
            It.IsAny<Exception?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<int>() ), Times.AtLeastOnce );
    }

    [Fact]
    public async Task CheckForUpdateAsyncはタグ解析失敗時に更新なしを返して警告ログを出す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoServiceMock = CreateAppInfoServiceMock();
        var httpClient = CreateClient( (_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
        {
            Content = new StringContent(
                """
                {
                  "tag_name": "not-a-version",
                  "html_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/not-a-version",
                  "draft": false,
                  "prerelease": false
                }
                """,
                Encoding.UTF8,
                "application/json" )
        } ) );

        var sut = new UpdateCheckService( appInfoServiceMock.Object, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
        loggerMock.Verify( logger => logger.Warn(
            It.IsAny<string>(),
            It.IsAny<Exception?>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<int>() ), Times.AtLeastOnce );
    }

    private static HttpClient CreateClient( Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder ) {
        return TestHttpClientFactory.Create( responder, "https://api.github.com/" );
    }

    private static HttpResponseMessage CreateJsonResponse( string json ) => new( HttpStatusCode.OK )
    {
        Content = new StringContent( json, Encoding.UTF8, "application/json" )
    };

    private static object CreateReleasePayload( string version, params object[][] assetGroups ) => new {
        tag_name = $"v{version}",
        html_url = $"https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v{version}",
        draft = false,
        prerelease = false,
        immutable = true,
        assets = assetGroups.SelectMany( assets => assets ).ToArray()
    };

    private static object CreateReleasePayload(
        string version,
        bool draft,
        bool prerelease,
        bool immutable,
        params object[][] assetGroups
    ) => new {
        tag_name = $"v{version}",
        html_url = $"https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v{version}",
        draft,
        prerelease,
        immutable,
        assets = assetGroups.SelectMany( assets => assets ).ToArray()
    };

    private static object[] CreateFullAssetPayload( string version, long size, long checksumSize ) {
        var name = $"DCS-Translation-Tool-{version}-win-x64.zip";
        return CreateAssetPayload( version, name, size, checksumSize );
    }

    private static object[] CreateDeltaAssetPayload( string baseVersion, string targetVersion, long size, long checksumSize ) {
        var name = $"DCS-Translation-Tool-{baseVersion}-to-{targetVersion}-win-x64.delta.zip";
        return CreateAssetPayload( targetVersion, name, size, checksumSize );
    }

    private static object[] CreateAssetPayload(
        string version,
        string name,
        long size,
        long checksumSize,
        string? digest = null
    ) =>
    [
        new
        {
            name,
            browser_download_url = $"https://github.com/5kdn/DCS-Translation-Tool/releases/download/v{version}/{name}",
            size,
            digest = digest ?? $"sha256:{new string( 'a', 64 )}"
        },
        new
        {
            name = $"{name}.sha256",
            browser_download_url = $"https://github.com/5kdn/DCS-Translation-Tool/releases/download/v{version}/{name}.sha256",
            size = checksumSize,
            digest = $"sha256:{new string( 'b', 64 )}"
        }
    ];

    private static Mock<IApplicationInfoService> CreateAppInfoServiceMock( Version? version = null ) {
        var appInfoServiceMock = new Mock<IApplicationInfoService>();
        appInfoServiceMock
            .Setup( service => service.GetVersion() )
            .Returns( version ?? new Version( 1, 3, 1, 0 ) );
        return appInfoServiceMock;
    }
}