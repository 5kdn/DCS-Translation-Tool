using System.Net;
using System.Text;

using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Interfaces;
using DcsTranslationTool.Infrastructure.Services;

using Moq;

namespace DcsTranslationTool.Infrastructure.Tests.Services;

/// <summary>
/// <see cref="UpdateCheckService"/> の更新判定挙動を検証する。
/// </summary>
public sealed class UpdateCheckServiceTests {
    [Fact]
    public async Task CheckForUpdateAsyncは最新版が存在する場合に更新ありを返す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoService = new StubApplicationInfoService( new Version( 1, 3, 1, 0 ) );
        var httpClient = CreateClient( (request, _) => {
            Assert.Equal( "/repos/5kdn/DCS-Translation-Tool/releases/latest", request.RequestUri?.AbsolutePath );
            return Task.FromResult( new HttpResponseMessage( HttpStatusCode.OK )
            {
                Content = new StringContent(
                """
                {
                  "tag_name": "v1.4.0",
                  "html_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
                  "draft": false,
                  "prerelease": false
                }
                """,
                Encoding.UTF8,
                "application/json" )
            } );
        } );

        var sut = new UpdateCheckService( appInfoService, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsUpdateAvailable );
        Assert.Equal( "v1.4.0", result.LatestVersionLabel );
        Assert.Equal( "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0", result.ReleaseUrl );
    }

    [Fact]
    public async Task CheckForUpdateAsyncは同一バージョンの場合に更新なしを返す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoService = new StubApplicationInfoService( new Version( 1, 3, 1, 0 ) );
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

        var sut = new UpdateCheckService( appInfoService, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
    }

    [Fact]
    public async Task CheckForUpdateAsyncはタグ形式差異を同一系列として判定する() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoService = new StubApplicationInfoService( new Version( 1, 3, 1, 0 ) );
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

        var sut = new UpdateCheckService( appInfoService, loggerMock.Object, httpClient );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.False( result.IsUpdateAvailable );
    }

    [Fact]
    public async Task CheckForUpdateAsyncはHttp失敗時に更新なしを返して警告ログを出す() {
        var loggerMock = new Mock<ILoggingService>();
        var appInfoService = new StubApplicationInfoService( new Version( 1, 3, 1, 0 ) );
        var httpClient = CreateClient( (_, _) => Task.FromResult( new HttpResponseMessage( HttpStatusCode.InternalServerError )
        {
            ReasonPhrase = "Server Error"
        } ) );

        var sut = new UpdateCheckService( appInfoService, loggerMock.Object, httpClient );

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
        var appInfoService = new StubApplicationInfoService( new Version( 1, 3, 1, 0 ) );
        var httpClient = CreateClient( (_, _) => throw new TaskCanceledException( "timeout" ) );

        var sut = new UpdateCheckService( appInfoService, loggerMock.Object, httpClient );

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
        var appInfoService = new StubApplicationInfoService( new Version( 1, 3, 1, 0 ) );
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

        var sut = new UpdateCheckService( appInfoService, loggerMock.Object, httpClient );

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
        var handler = new StubHttpMessageHandler( responder );
        return new HttpClient( handler )
        {
            BaseAddress = new Uri( "https://api.github.com/" )
        };
    }

    private sealed class StubHttpMessageHandler( Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder ) : HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken cancellationToken ) => responder( request, cancellationToken );
    }

    private sealed class StubApplicationInfoService( Version version ) : IApplicationInfoService {
        public Version GetVersion() => version;
    }
}