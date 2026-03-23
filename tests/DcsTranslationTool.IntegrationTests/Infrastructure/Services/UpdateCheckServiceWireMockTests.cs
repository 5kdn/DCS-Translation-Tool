using DcsTranslationTool.Application.Interfaces;
using DcsTranslationTool.Infrastructure.Services;
using DcsTranslationTool.IntegrationTests.Infrastructure.Services.WireMock;

using Moq;

using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace DcsTranslationTool.IntegrationTests.Infrastructure.Services;

/// <summary>
/// <see cref="UpdateCheckService"/> の HTTP 契約を WireMock で検証する。
/// </summary>
public sealed class UpdateCheckServiceWireMockTests {
    /// <summary>
    /// GitHub Releases API への要求とレスポンス解釈を検証する。
    /// </summary>
    [Fact]
    public async Task CheckForUpdateAsyncはWireMockでHTTP契約を満たす() {
        using var server = new WireMockTestServer();
        var appInfoServiceMock = new Mock<IApplicationInfoService>();
        appInfoServiceMock
            .Setup( service => service.GetVersion() )
            .Returns( new Version( 1, 3, 1, 0 ) );

        server.Server
            .Given( Request.Create().WithPath( "/repos/5kdn/DCS-Translation-Tool/releases/latest" ).UsingGet() )
            .RespondWith(
                Response.Create()
                    .WithStatusCode( 200 )
                    .WithHeader( "Content-Type", "application/json; charset=utf-8" )
                    .WithBody(
                        """
                        {
                          "tag_name": "v1.4.0",
                          "html_url": "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0",
                          "draft": false,
                          "prerelease": false
                        }
                        """ ) );

        var client = server.CreateHttpClient();
        var sut = new UpdateCheckService(
            appInfoServiceMock.Object,
            Mock.Of<ILoggingService>(),
            client );

        var result = await sut.CheckForUpdateAsync( TestContext.Current.CancellationToken );

        Assert.True( result.IsUpdateAvailable );
        Assert.Equal( "v1.4.0", result.LatestVersionLabel );
        Assert.Equal( "https://github.com/5kdn/DCS-Translation-Tool/releases/tag/v1.4.0", result.ReleaseUrl );

        Assert.Contains(
            server.Server.LogEntries,
            entry => string.Equals( entry.RequestMessage?.Path, "/repos/5kdn/DCS-Translation-Tool/releases/latest", StringComparison.Ordinal ) );
        var request = server.Server.LogEntries
            .Last( entry => string.Equals( entry.RequestMessage?.Path, "/repos/5kdn/DCS-Translation-Tool/releases/latest", StringComparison.Ordinal ) )
            .RequestMessage!;
        Assert.Equal( "GET", request.Method );
        Assert.Equal( "/repos/5kdn/DCS-Translation-Tool/releases/latest", request.Path );
        Assert.NotNull( request.Headers );
        Assert.Contains(
            request.Headers!["Accept"].Select( value => value?.ToString() ?? string.Empty ),
            value => value.Contains( "application/vnd.github+json", StringComparison.Ordinal ) );
        Assert.Contains(
            request.Headers["User-Agent"].Select( value => value?.ToString() ?? string.Empty ),
            value => value.Contains( "DCS-Translation-Tool", StringComparison.Ordinal ) );
    }
}