namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Http;

/// <summary>
/// Verifies bounded body consumption after the transport has already returned successful response headers.
/// </summary>
[Collection(nameof(Mappings.GlobalMappingCollection))]
public sealed class ThirdPartyBodyTests
{
    /// <summary>
    /// Cancels a stalled body within the facade deadline without changing it into a provider business error.
    /// </summary>
    /// <returns>The asynchronous deadline and disposal assertions.</returns>
    [Fact]
    public async Task StalledBody_HasIndependentDeadlineAndDisposesContent()
    {
        using var content = new StalledContent();
        using var provider = CreateProvider(content);
        using var scope = provider.CreateScope();
        using var watchdog = new CancellationTokenSource(TimeSpan.FromSeconds(35));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
                .HandleApiData(Request(), watchdog.Token));

        // The outer watchdog bounds a broken implementation; only the facade's earlier deadline may end this read.
        Assert.False(watchdog.IsCancellationRequested);
        Assert.True(content.Disposed);
    }

    /// <summary>
    /// Preserves caller cancellation after headers and releases the response content.
    /// </summary>
    /// <returns>The asynchronous caller-cancellation assertions.</returns>
    [Fact]
    public async Task CallerCancellation_DuringBodyReadIsPreserved()
    {
        var logger = new TestLogger<ThirdPartyApiService>();
        using var content = new StalledContent();
        using var provider = CreateProvider(content, logger);
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var pending = scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>()
            .HandleApiData(Request(), cancellation.Token);

        await content.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.True(content.Disposed);
        var entry = Assert.Single(logger.Entries, entry => entry.Message.Contains("body read cancelled", StringComparison.Ordinal));
        Assert.Equal(LogLevel.Debug, entry.Level);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// Rejects response bodies over the existing mebibyte bound rather than exposing unbounded provider data.
    /// </summary>
    /// <returns>The asynchronous response-size assertions.</returns>
    [Fact]
    public async Task OversizedBody_IsRejectedBeforeSuccessfulOutput()
    {
        var logger = new TestLogger<ThirdPartyApiService>();
        using var content = new StringContent(new string('x', 1_048_577));
        using var provider = CreateProvider(content, logger);
        using var scope = provider.CreateScope();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleApiData(Request()));

        var entry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("body read failed", entry.Message);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// Leaves programming errors to the caller while still disposing the unread response.
    /// </summary>
    /// <returns>The asynchronous exception identity, privacy and disposal assertions.</returns>
    [Fact]
    public async Task ProgrammingFailure_PreservesExceptionWithoutProviderWarning()
    {
        var logger = new TestLogger<ThirdPartyApiService>();
        var failure = new InvalidOperationException("private-token-in-provider-error");
        using var content = new FailedContent(failure);
        using var provider = CreateProvider(content, logger);
        using var scope = provider.CreateScope();

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleApiData(Request()));

        Assert.Same(failure, actual);
        Assert.True(content.Disposed);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Records a known body transport failure without replacing its exception or logging provider-controlled text.
    /// </summary>
    /// <returns>The safe body diagnostic and native exception-identity assertions.</returns>
    [Fact]
    public async Task TransportReadFailure_EmitsOneSafeBodyDiagnostic()
    {
        var failure = new HttpRequestException("private-provider-message");
        var logger = new TestLogger<ThirdPartyApiService>();
        using var content = new FailedContent(failure);
        using var provider = CreateProvider(content, logger);
        using var scope = provider.CreateScope();

        var actual = await Assert.ThrowsAsync<HttpRequestException>(() =>
            scope.ServiceProvider.GetRequiredService<IThirdPartyApiService>().HandleApiData(Request()));

        Assert.Same(failure, actual);
        Assert.True(content.Disposed);
        var entry = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains(nameof(HttpRequestException), entry.Message);
        Assert.DoesNotContain("private-provider-message", entry.Message);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// Composes the real facade, startup mappings, serializer and transport while replacing only the external network.
    /// </summary>
    /// <param name="content">The unread content returned by the synthetic upstream.</param>
    /// <param name="logger">The optional logger used to inspect facade diagnostics only.</param>
    /// <returns>The provider owned and disposed by the test.</returns>
    private static ServiceProvider CreateProvider(HttpContent content, TestLogger<ThirdPartyApiService>? logger = null)
    {
        var services = new ServiceCollection().AddLogging().AddCoreInfrastructure();
        if (logger is not null)
        {
            services.AddSingleton<ILogger<ThirdPartyApiService>>(logger);
        }

        services.AddHttpClientInfrastructure("body-probe", _ => { })
            .ConfigurePrimaryHttpMessageHandler(() => new BodyHandler(content));

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Creates the fixture transport request without relying on a live external endpoint.
    /// </summary>
    /// <returns>The request consumed by the real shared facade.</returns>
    private static BaseThirdPartyApiRequest Request() =>
        new() { HttpClientName = "body-probe", Endpoint = "https://selected.invalid", Method = "GET" };

    /// <summary>
    /// Returns headers immediately so tests isolate body consumption from the HTTP send timeout.
    /// </summary>
    /// <param name="content">The body whose ownership transfers to the response.</param>
    private sealed class BodyHandler(HttpContent content) : HttpMessageHandler
    {
        /// <summary>
        /// Returns an unread successful response with no additional network delay.
        /// </summary>
        /// <param name="request">The real outgoing request.</param>
        /// <param name="cancellationToken">Cancellation owned by the transport send stage.</param>
        /// <returns>The upstream response owned by the facade.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    /// <summary>
    /// Fails while producing body bytes, after the transport has successfully returned headers.
    /// </summary>
    /// <param name="failure">The exact exception instance that must reach the caller.</param>
    private sealed class FailedContent(Exception failure) : HttpContent
    {
        /// <summary>
        /// Gets whether the facade released the failed response.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Produces the controlled body failure without a live provider.
        /// </summary>
        /// <param name="stream">The native body buffer.</param>
        /// <param name="context">The optional transport context.</param>
        /// <returns>The original failed operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(failure);

        /// <summary>
        /// Leaves body size unknown so the native buffer invokes the failing producer.
        /// </summary>
        /// <param name="length">The unused length for this unknown-size body.</param>
        /// <returns>False because no body length is declared.</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }

        /// <summary>
        /// Records response cleanup before native disposal.
        /// </summary>
        /// <param name="disposing">Whether managed resources are being released.</param>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Simulates an upstream that returns headers but never supplies its response body.
    /// </summary>
    private sealed class StalledContent : HttpContent
    {
        /// <summary>
        /// Signals when buffering begins, avoiding timing races in the caller-cancellation test.
        /// </summary>
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets whether the owning facade disposed the response content.
        /// </summary>
        public bool Disposed { get; private set; }

        /// <summary>
        /// Forwards the legacy overload to the cancellable content producer.
        /// </summary>
        /// <param name="stream">The buffer receiving provider bytes.</param>
        /// <param name="context">The optional native transport context.</param>
        /// <returns>The stalled serialization operation.</returns>
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            SerializeToStreamAsync(stream, context, CancellationToken.None);

        /// <summary>
        /// Waits for the actual body-read token rather than the earlier HTTP send token.
        /// </summary>
        /// <param name="stream">The buffer receiving provider bytes.</param>
        /// <param name="context">The optional native transport context.</param>
        /// <param name="cancellationToken">The body-consumption cancellation token.</param>
        /// <returns>The stalled operation, ending only when cancellation is requested.</returns>
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            Started.TrySetResult();

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        /// <summary>
        /// Declares an unknown size so buffering must consult the cancellable producer.
        /// </summary>
        /// <param name="length">The unused length for this unknown-size body.</param>
        /// <returns>False because the upstream has not declared its body size.</returns>
        protected override bool TryComputeLength(out long length)
        {
            length = 0;

            return false;
        }

        /// <summary>
        /// Records response ownership release before delegating native disposal.
        /// </summary>
        /// <param name="disposing">Whether managed resources are being released.</param>
        protected override void Dispose(bool disposing)
        {
            Disposed = true;

            base.Dispose(disposing);
        }
    }
}
