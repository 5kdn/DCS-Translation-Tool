using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;
using DcsTranslationTool.Presentation.Wpf.UI.Interfaces;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// ダウンロードと適用処理の実行を提供する。
/// </summary>
public sealed class DownloadWorkflowService(
    ILoggingService logger,
    IApplyWorkflowService applyWorkflowService
) : IDownloadWorkflowService {
    private static readonly ConcurrentDictionary<string, IPAddress> LastSuccessfulAddressCache = new();

    /// <inheritdoc/>
    public async Task DownloadFilesAsync(
        IReadOnlyList<ApiDownloadFilePathsItem> items,
        string saveRootPath,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        if(items.Count == 0) {
            return;
        }

        var successCount = 0;
        var failureCount = 0;
        var progressed = 0;
        var maxConcurrency = Math.Clamp( Environment.ProcessorCount, 2, 6 );
        using var semaphore = new SemaphoreSlim( maxConcurrency );
        using var httpClient = CreateHttpClient();
        List<Task> tasks = [];

        foreach(var item in items) {
            await semaphore.WaitAsync( cancellationToken );

            tasks.Add( Task.Run( async () => {
                try {
                    var filePath = Path.Combine( saveRootPath, item.Path );
                    await this.DownloadFileAsync( httpClient, item.Url, filePath, cancellationToken );
                    Interlocked.Increment( ref successCount );
                }
                catch {
                    Interlocked.Increment( ref failureCount );
                    throw;
                }
                finally {
                    var current = Interlocked.Increment( ref progressed );
                    await progressCallback( Math.Min( 100, (double)current / items.Count * 100 ) );
                    semaphore.Release();
                }
            }, cancellationToken ) );
        }

        try {
            await Task.WhenAll( tasks );
        }
        finally {
            logger.Info( $"ファイルのダウンロードが完了した。成功={successCount}/{items.Count} 件, 失敗={failureCount}/{items.Count} 件" );
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(
        IReadOnlyList<IFileEntryViewModel> targetEntries,
        string rootFullPath,
        string rootWithSeparator,
        string translateFullPath,
        string translateRootWithSeparator,
        Func<string, Task> showSnackbarAsync,
        Func<double, Task> progressCallback,
        CancellationToken cancellationToken = default
    ) {
        return await applyWorkflowService.ApplyAsync(
            targetEntries,
            rootFullPath,
            rootWithSeparator,
            translateFullPath,
            translateRootWithSeparator,
            items => this.DownloadFilesAsync( items, translateFullPath, _ => Task.CompletedTask, cancellationToken ),
            showSnackbarAsync,
            progressCallback,
            cancellationToken
        );
    }

    /// <summary>
    /// 単一ファイルを保存する。
    /// </summary>
    /// <param name="httpClient">HTTP クライアント。</param>
    /// <param name="url">ダウンロードURL。</param>
    /// <param name="filePath">保存先パス。</param>
    /// <param name="cancellationToken">キャンセルトークン。</param>
    /// <returns>非同期タスク。</returns>
    private async Task DownloadFileAsync( HttpClient httpClient, string url, string filePath, CancellationToken cancellationToken ) {
        logger.Info( $"ファイルをダウンロードする。Url={url}, FilePath={filePath}" );
        try {
            var bytes = await httpClient.GetByteArrayAsync( url, cancellationToken );
            var directoryName = Path.GetDirectoryName( filePath );
            if(!string.IsNullOrEmpty( directoryName ) && !Directory.Exists( directoryName )) {
                Directory.CreateDirectory( directoryName );
            }

            await File.WriteAllBytesAsync( filePath, bytes, cancellationToken );
        }
        catch(Exception ex) {
            logger.Error( $"ファイルのダウンロードに失敗した。Url={url}, FilePath={filePath}", ex );
            throw;
        }
        logger.Info( $"ファイルのダウンロードが完了した。Url={url}, FilePath={filePath}" );
    }

    /// <summary>
    /// 環境差異を吸収する HttpClient を生成する。
    /// </summary>
    /// <returns>初期化済みの HttpClient。</returns>
    private static HttpClient CreateHttpClient() {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds( 3 ),
            ConnectCallback = async ( context, token ) => {
                var host = context.DnsEndPoint!.Host;
                var port = context.DnsEndPoint.Port;
                var hostKey = host.ToLowerInvariant();

                IPAddress[] v4 = [];
                IPAddress[] v6 = [];
                try {
                    v4 = await Dns.GetHostAddressesAsync( host, AddressFamily.InterNetwork, token );
                }
                catch {
                    // IPv4 解決不可は許容する。
                }
                try {
                    v6 = await Dns.GetHostAddressesAsync( host, AddressFamily.InterNetworkV6, token );
                }
                catch {
                    // IPv6 解決不可は許容する。
                }

                var candidates = new List<IPAddress>( v4.Length + v6.Length + 1 );
                if(LastSuccessfulAddressCache.TryGetValue( hostKey, out var cached )) {
                    candidates.Add( cached );
                }
                candidates.AddRange( v4 );
                candidates.AddRange( v6 );

                foreach(var addr in candidates.Distinct()) {
                    var socket = new Socket( addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp ) { NoDelay = true };
                    try {
                        await socket.ConnectAsync( addr, port, token );
                        LastSuccessfulAddressCache[hostKey] = addr;
                        return new NetworkStream( socket, ownsSocket: true );
                    }
                    catch {
                        socket.Dispose();
                    }
                }

                LastSuccessfulAddressCache.TryRemove( hostKey, out _ );
                throw new SocketException( (int)SocketError.HostUnreachable );
            }
        };

        return new HttpClient( handler, disposeHandler: true );
    }
}