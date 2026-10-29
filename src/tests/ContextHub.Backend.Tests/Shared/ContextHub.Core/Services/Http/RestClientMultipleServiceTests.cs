namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Http;

/// <summary>
/// Verifies neutral HTTP transport without network access or response fabrication.
/// </summary>
public sealed class RestClientMultipleServiceTests
{
    /// <summary>
    /// Selects each named configuration while preserving every reference-style verb and JSON convention.
    /// </summary>
    /// <param name="operation">The multiple-client operation to exercise.</param>
    /// <param name="method">The expected native HTTP verb.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("get", "GET")]
    [InlineData("post", "POST")]
    [InlineData("put", "PUT")]
    [InlineData("patch", "PATCH")]
    [InlineData("delete", "DELETE")]
    public async Task NamedVerbs_KeepClientSelectionAndMethod(string operation, string method)
    {
        string? actualMethod = null;
        string? actualHost = null;
        string? body = null;
        var handler = new CallbackHandler(async (request, token) =>
        {
            actualMethod = request.Method.Method;
            actualHost = request.RequestUri!.Host;
            body = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"name\":\"result\"}") };
        });
        var services = new ServiceCollection().AddLogging().AddRestClients();
        services.AddHttpClient("first", client => client.BaseAddress = new Uri("https://first.invalid"));
        services.AddHttpClient("selected", client => client.BaseAddress = new Uri("https://selected.invalid"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IRestClientMultipleService>();
        var dto = new BaseHttpRequest { HttpClientName = "selected", RequestUri = "/work", RequestData = "{\"name\":\"sample\"}" };

        using var response = await (operation switch
        {
            "get" => transport.GetAsync(dto),
            "post" => transport.PostAsync(dto),
            "put" => transport.PutAsync(dto),
            "patch" => transport.PatchAsync(dto),
            _ => transport.DeleteAsync(dto)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(method, actualMethod);
        Assert.Equal("selected.invalid", actualHost);
        if (body is not null)
        {
            using var json = JsonDocument.Parse(body);
            Assert.Equal("sample", json.RootElement.GetProperty("name").GetString());
        }
    }

    /// <summary>
    /// Forwards native verbs and content types without replacing them with legacy DTO serialization.
    /// </summary>
    /// <param name="method">The native HTTP verb.</param>
    /// <param name="kind">The content kind constructed by the caller.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("GET", "none")]
    [InlineData("DELETE", "none")]
    [InlineData("POST", "json")]
    [InlineData("PUT", "form")]
    [InlineData("PATCH", "multipart")]
    [InlineData("POST", "stream")]
    public async Task SendAsync_PreservesNativeMethodAndContent(string method, string kind)
    {
        string? actualMethod = null;
        string? mediaType = null;
        string? actualBody = null;
        var handler = new CallbackHandler(async (request, token) =>
        {
            actualMethod = request.Method.Method;
            mediaType = request.Content?.Headers.ContentType?.MediaType;
            actualBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(token);

            return new HttpResponseMessage(HttpStatusCode.Accepted);
        });
        var services = new ServiceCollection().AddLogging().AddRestClients();
        services.AddHttpClient("native").ConfigurePrimaryHttpMessageHandler(() => handler);
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IRestClientMultipleService>();
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://example.invalid/resource");
        request.Content = CreateContent(kind);

        using var response = await transport.SendAsync("native", request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(method, actualMethod);
        Assert.Equal(kind switch
        {
            "json" => "application/json",
            "form" => "application/x-www-form-urlencoded",
            "multipart" => "multipart/form-data",
            _ => null
        }, mediaType);
        if (kind != "none")
        {
            Assert.Contains("sample", actualBody);
        }
    }

    /// <summary>
    /// Constructs content with native APIs so transfer tests do not validate a replacement serializer.
    /// </summary>
    /// <param name="kind">The request content variant selected by this scenario.</param>
    /// <returns>The selected native content fixture, or null for the no-body case.</returns>
    private static HttpContent? CreateContent(string kind)
    {
        if (kind == "multipart")
        {
            var multipart = new MultipartFormDataContent();
            multipart.Add(new StringContent("sample"), "field");

            return multipart;
        }

        return kind switch
        {
            "json" => JsonContent.Create(new { value = "sample" }),
            "form" => new FormUrlEncodedContent(new Dictionary<string, string> { ["value"] = "sample" }),
            "stream" => new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes("sample"))),
            _ => null
        };
    }

    /// <summary>
    /// Preserves upstream status, content and caller-owned resources without buffering the body.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SendAsync_ReturnsRealFailureWithoutReadingOrDisposingContent()
    {
        var body = new TrackingContent();
        var handler = new CallbackHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Conflict) { Content = body }));
        var services = new ServiceCollection().AddLogging().AddRestClients();
        services.AddHttpClient("upstream").ConfigurePrimaryHttpMessageHandler(() => handler);
        var logger = new TestLogger<RestClientMultipleService>();
        services.AddSingleton<ILogger<RestClientMultipleService>>(logger);
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IRestClientMultipleService>();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.invalid/private?token=secret");
        request.Content = new StringContent("private-body");

        using var response = await transport.SendAsync("upstream", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(body.Read);
        Assert.False(body.Disposed);
        Assert.Equal("upstream-body", await response.Content.ReadAsStringAsync());
        Assert.Equal("private-body", await request.Content.ReadAsStringAsync());
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain("private", entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("secret", entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(entry.Exception);
        }
    }

    /// <summary>
    /// Keeps the same transport exception and never includes its credential-bearing text in logs.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SendAsync_PreservesExceptionInstance()
    {
        var failure = new HttpRequestException("private-token-in-exception");
        var services = new ServiceCollection().AddLogging().AddRestClients();
        services.AddHttpClient("upstream").ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler((_, _) => Task.FromException<HttpResponseMessage>(failure)));
        var logger = new TestLogger<RestClientMultipleService>();
        services.AddSingleton<ILogger<RestClientMultipleService>>(logger);
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IRestClientMultipleService>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/private");

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() => transport.SendAsync("upstream", request));

        Assert.Same(failure, actual);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        foreach (var entry in logger.Entries)
        {
            Assert.DoesNotContain("private-token", entry.Message, StringComparison.Ordinal);
            Assert.Null(entry.Exception);
        }
    }

    /// <summary>
    /// Leaves a handler's programming error to the owning caller instead of labeling it a transport outage.
    /// </summary>
    /// <returns>The original exception and absence of a misleading transport warning.</returns>
    [Fact]
    public async Task SendAsync_ProgrammingFailureDoesNotEmitTransportWarning()
    {
        var failure = new InvalidOperationException("private-programming-error");
        var logger = new TestLogger<RestClientMultipleService>();
        var services = new ServiceCollection().AddLogging().AddRestClients();
        services.AddSingleton<ILogger<RestClientMultipleService>>(logger);
        services.AddHttpClient("upstream").ConfigurePrimaryHttpMessageHandler(() =>
            new CallbackHandler((_, _) => Task.FromException<HttpResponseMessage>(failure)));
        await using var provider = services.BuildServiceProvider();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://selected.invalid");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.GetRequiredService<IRestClientMultipleService>().SendAsync("upstream", request));

        Assert.Same(failure, actual);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Passes cancellation to the outbound operation rather than reporting a synthetic gateway timeout.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SendAsync_PropagatesCancellation()
    {
        var services = new ServiceCollection().AddLogging().AddRestClients();
        var logger = new TestLogger<RestClientMultipleService>();
        services.AddSingleton<ILogger<RestClientMultipleService>>(logger);
        services.AddHttpClient("upstream").ConfigurePrimaryHttpMessageHandler(() => new CallbackHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, token);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        await using var provider = services.BuildServiceProvider();
        var transport = provider.GetRequiredService<IRestClientMultipleService>();
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid");
        using var cancellation = new CancellationTokenSource();
        var pending = transport.SendAsync("upstream", request, cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Observes only the outbound handler boundary while retaining the real client factory.
    /// </summary>
    /// <param name="callback">The synthetic upstream invoked with the real outgoing request and cancellation token.</param>
    private sealed class CallbackHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        /// <summary>
        /// Executes the synthetic upstream without replacing factory configuration or transport logic.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request, cancellationToken);
    }

    /// <summary>
    /// Detects eager buffering and premature disposal at the transport boundary.
    /// </summary>
    private sealed class TrackingContent : HttpContent
    {
        /// <summary>
        /// Gets whether transport or the caller has consumed the content.
        /// </summary>
        public bool Read { get; private set; }

        /// <summary>
        /// Gets whether the response owner has disposed the content.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Writes a fixed synthetic body only when explicitly consumed.
        /// </summary>
        /// <param name="stream">The destination stream supplied by native HTTP content serialization.</param>
        /// <param name="context">The operation context supplied by the framework.</param>
        /// <returns>A task representing the fixture content's asynchronous write.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            Read = true;

            return stream.WriteAsync(Encoding.UTF8.GetBytes("upstream-body")).AsTask();
        }

        /// <summary>
        /// Declines a precomputed length so body consumption remains observable.
        /// </summary>
        /// <param name="length">The computed content length, assigned by this content fixture.</param>
        /// <returns>Whether this content fixture can provide its byte length without reading.</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }

        /// <summary>
        /// Records lifecycle ownership without interfering with native content cleanup.
        /// </summary>
        /// <param name="disposing">Whether managed resources should be released by this disposal call.</param>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
