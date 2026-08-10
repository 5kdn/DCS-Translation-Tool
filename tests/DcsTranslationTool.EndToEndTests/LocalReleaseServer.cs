using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DcsTranslationTool.EndToEndTests;

/// <summary>
/// GitHub Release相当のメタデータと更新アセットをloopbackで配信する。
/// </summary>
/// <param name="runtimeIdentifier">配信対象RID。</param>
/// <param name="assetsDirectory">配信する更新アセットのディレクトリ。</param>
internal sealed class LocalReleaseServer( string runtimeIdentifier, string assetsDirectory ) : IAsyncDisposable {
    private readonly string _runtimeIdentifier = runtimeIdentifier;
    private readonly string _assetsDirectory = Path.GetFullPath( assetsDirectory );
    private readonly ConcurrentQueue<string> _requests = new();
    private WebApplication? _application;

    /// <summary>
    /// 受信した要求パスを取得する。
    /// </summary>
    public IReadOnlyCollection<string> Requests => [.. this._requests];

    /// <summary>
    /// サーバーのベースURIを取得する。
    /// </summary>
    public Uri BaseUri { get; private set; } = null!;

    /// <summary>
    /// loopbackの動的ポートでサーバーを開始する。
    /// </summary>
    /// <param name="cancellationToken">開始処理を取り消すトークン。</param>
    public async Task StartAsync( CancellationToken cancellationToken ) {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel( options => options.Listen( IPAddress.Loopback, 0 ) );
        var application = builder.Build();
        application.Use( async ( context, next ) => {
            this._requests.Enqueue( context.Request.Path.Value ?? string.Empty );
            await next( context );
        } );
        application.MapGet(
            "/repos/5kdn/DCS-Translation-Tool/releases/latest",
            () => Results.Json( this.CreateReleasePayload() ) );
        application.MapGet( "/releases/v1.1.0", () => Results.Text( "v1.1.0" ) );
        application.MapGet( "/assets/{fileName}", this.DownloadAssetAsync );
        await application.StartAsync( cancellationToken );

        var addresses = application.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.SingleOrDefault()
            ?? throw new InvalidOperationException( "The E2E release server did not expose an address." );
        this.BaseUri = new Uri( address.EndsWith( '/' ) ? address : $"{address}/" );
        this._application = application;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() {
        if(this._application is not null) {
            await this._application.StopAsync();
            await this._application.DisposeAsync();
        }
    }

    /// <summary>
    /// 指定された更新アセットを遅延付きで返す。
    /// </summary>
    /// <param name="fileName">要求されたファイル名。</param>
    /// <returns>アセット応答を返す。</returns>
    private async Task<IResult> DownloadAssetAsync( string fileName ) {
        var safeFileName = Path.GetFileName( fileName );
        var path = Path.Combine( this._assetsDirectory, safeFileName );
        if(!File.Exists( path )) {
            return Results.NotFound();
        }

        await Task.Delay( TimeSpan.FromMilliseconds( 750 ) );
        return Results.File( path, "application/octet-stream", safeFileName, enableRangeProcessing: false );
    }

    /// <summary>
    /// Immutable Release相当のJSONペイロードを生成する。
    /// </summary>
    /// <returns>Release APIへ返すオブジェクトを返す。</returns>
    private object CreateReleasePayload() {
        var fullName = $"DCS-Translation-Tool-1.1.0-{this._runtimeIdentifier}.zip";
        var deltaName = $"DCS-Translation-Tool-1.0.0-to-1.1.0-{this._runtimeIdentifier}.delta.zip";
        return new {
            tag_name = "v1.1.0",
            html_url = new Uri( this.BaseUri, "releases/v1.1.0" ).AbsoluteUri,
            draft = false,
            prerelease = false,
            immutable = true,
            assets = new[]
            {
                this.CreateAssetPayload( fullName ),
                this.CreateAssetPayload( $"{fullName}.sha256" ),
                this.CreateAssetPayload( deltaName ),
                this.CreateAssetPayload( $"{deltaName}.sha256" )
            }
        };
    }

    /// <summary>
    /// Release APIのasset要素を生成する。
    /// </summary>
    /// <param name="fileName">アセットファイル名。</param>
    /// <returns>サイズとdigestを含むasset要素を返す。</returns>
    private object CreateAssetPayload( string fileName ) {
        var path = Path.Combine( this._assetsDirectory, fileName );
        var info = new FileInfo( path );
        var digest = Convert.ToHexString( SHA256.HashData( File.ReadAllBytes( path ) ) ).ToLowerInvariant();
        return new {
            name = fileName,
            browser_download_url = new Uri( this.BaseUri, $"assets/{Uri.EscapeDataString( fileName )}" ).AbsoluteUri,
            size = info.Length,
            digest = $"sha256:{digest}"
        };
    }
}