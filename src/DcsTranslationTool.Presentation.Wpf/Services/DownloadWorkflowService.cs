using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

using DcsTranslationTool.Application.Contracts;
using DcsTranslationTool.Presentation.Wpf.Services.Abstractions;

namespace DcsTranslationTool.Presentation.Wpf.Services;

/// <summary>
/// ダウンロード処理を提供する。
/// </summary>
public sealed class DownloadWorkflowService(
    ILoggingService logger
) : IDownloadWorkflowService {
    private static readonly ConcurrentDictionary<string, IPAddress> LastSuccessfulAddressCache = new();

    /// <inheritdoc/>
    public async Task<DownloadWorkflowResult> DownloadFilesAsync(
        IReadOnlyList<ApiDownloadFilePathsItem> items,
        string saveRootPath,
        CancellationToken cancellationToken = default,
        IProgress<WorkflowEvent>? progress = null
    ) {
        if(items.Count == 0) {
            return new DownloadWorkflowResult( true, [] );
        }

        var successCount = 0;
        var failureCount = 0;
        var progressed = 0;
        var progressEvents = new ConcurrentQueue<(int Sequence, WorkflowEvent Event)>();

        var maxConcurrency = Math.Clamp( Environment.ProcessorCount, 2, 6 );
        using var semaphore = new SemaphoreSlim( maxConcurrency );
        using var httpClient = CreateHttpClient();
        List<Task> tasks = [];
        var isCancellationRequested = false;

        foreach(var item in items) {
            try {
                await semaphore.WaitAsync( cancellationToken );
            }
            catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
                isCancellationRequested = true;
                break;
            }

            tasks.Add( Task.Run( async () => {
                var canceled = false;
                try {
                    var filePath = Path.Combine( saveRootPath, item.Path );
                    await DownloadFileAsync( httpClient, item.Url, filePath, cancellationToken );
                    Interlocked.Increment( ref successCount );
                }
                catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
                    canceled = true;
                    throw;
                }
                catch(Exception ex) {
                    Interlocked.Increment( ref failureCount );
                    logger.Error( $"ファイルのダウンロードに失敗した。Path={item.Path}", ex );
                }
                finally {
                    if(!canceled) {
                        var current = Interlocked.Increment( ref progressed );
                        var progressEvent = new WorkflowEvent(
                            WorkflowEventKind.DownloadProgress,
                            Progress: Math.Min( 100, (double)current / items.Count * 100 ) );
                        progressEvents.Enqueue( (current, progressEvent) );
                        progress?.Report( progressEvent );
                    }
                    semaphore.Release();
                }
            } ) );
        }

        try {
            await Task.WhenAll( tasks );
        }
        catch(OperationCanceledException) when(cancellationToken.IsCancellationRequested) {
            isCancellationRequested = true;
        }

        if(isCancellationRequested) {
            cancellationToken.ThrowIfCancellationRequested();
        }

        logger.Info( $"ファイルのダウンロードが完了した。成功={successCount}/{items.Count} 件, 失敗={failureCount}/{items.Count} 件" );

        var events = progressEvents
            .OrderBy( e => e.Sequence )
            .Select( e => e.Event )
            .ToList();

        if(failureCount > 0) {
            events.Add( new WorkflowEvent( WorkflowEventKind.Notification, $"一部のファイルの保存に失敗しました ({failureCount}/{items.Count})" ) );
        }

        return new DownloadWorkflowResult( failureCount == 0, events );
    }

    /// <summary>
    /// 単一ファイルを保存する。
    /// </summary>
    private async Task DownloadFileAsync( HttpClient httpClient, string url, string filePath, CancellationToken cancellationToken ) {
        logger.Info( $"ファイルをダウンロードする。Url={url}, FilePath={filePath}" );
        var bytes = await httpClient.GetByteArrayAsync( url, cancellationToken );
        var directoryName = Path.GetDirectoryName( filePath );
        if(!string.IsNullOrEmpty( directoryName ) && !Directory.Exists( directoryName )) {
            Directory.CreateDirectory( directoryName );
        }

        await File.WriteAllBytesAsync( filePath, bytes, cancellationToken );
    }

    /// <summary>
    /// 環境差異を吸収する HttpClient を生成する。
    /// </summary>
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