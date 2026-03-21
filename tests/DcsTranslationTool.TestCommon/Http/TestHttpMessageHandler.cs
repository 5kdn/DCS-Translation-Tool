namespace DcsTranslationTool.TestCommon.Http;

/// <summary>
/// 任意レスポンダーを使用するテスト用 HTTP メッセージ ハンドラーを表す。
/// </summary>
/// <param name="responder">レスポンダー。</param>
public sealed class TestHttpMessageHandler( Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder ) : HttpMessageHandler {
    /// <summary>
    /// 要求を処理してレスポンダー結果を返す。
    /// </summary>
    /// <param name="request">HTTP 要求。</param>
    /// <param name="cancellationToken">キャンセル トークン。</param>
    /// <returns>HTTP 応答を返す。</returns>
    protected override Task<HttpResponseMessage> SendAsync( HttpRequestMessage request, CancellationToken cancellationToken ) =>
        responder( request, cancellationToken );
}