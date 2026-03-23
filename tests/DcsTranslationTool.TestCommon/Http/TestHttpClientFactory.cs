namespace DcsTranslationTool.TestCommon.Http;

/// <summary>
/// テスト用 HTTP クライアント生成を補助する。
/// </summary>
public static class TestHttpClientFactory {
    /// <summary>
    /// レスポンダーから HTTP クライアントを生成する。
    /// </summary>
    /// <param name="responder">レスポンダー。</param>
    /// <param name="baseAddress">ベース アドレス。</param>
    /// <returns>生成した HTTP クライアントを返す。</returns>
    public static HttpClient Create(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder,
        string baseAddress ) =>
        new( new TestHttpMessageHandler( responder ) )
        {
            BaseAddress = new Uri( baseAddress, UriKind.Absolute ),
        };
}