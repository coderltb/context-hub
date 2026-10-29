namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.DependencyInjection;

/// <summary>
/// Requires explicit resilience registration rather than changing every outbound HTTP client.
/// </summary>
public sealed class HttpResilienceTests
{
    /// <summary>
    /// Requires the approved opt-in extension before integration tests exercise the native handler.
    /// </summary>
    [Fact]
    public void ResilienceRegistration_IsAvailable()
    {
        var operations = typeof(CoreServiceRegistration).GetMethods().Select(method => method.Name);
        Assert.Contains("AddHttpResilience", operations);
    }

    /// <summary>
    /// Exercises the native handler: only safe methods can replay a transient server failure.
    /// </summary>
    /// <param name="method">The method sent to the synthetic upstream.</param>
    /// <param name="expectedCalls">The permitted number of attempts.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("GET", 4)]
    [InlineData("POST", 1)]
    [InlineData("PUT", 1)]
    [InlineData("PATCH", 1)]
    [InlineData("DELETE", 1)]
    public async Task TransientFailure_RetriesSafeMethodsOnly(string method, int expectedCalls)
    {
        var handler = new CountingHandler();
        var services = new ServiceCollection().AddLogging();
        services.AddHttpClient("resilient").ConfigurePrimaryHttpMessageHandler(() => handler)
            .AddHttpResilience(options => options.Retry.Delay = TimeSpan.Zero);
        services.AddHttpClient("plain").ConfigurePrimaryHttpMessageHandler(() => new CountingHandler());
        await using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("resilient");
        using var request = new HttpRequestMessage(new HttpMethod(method), "https://example.invalid/test");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(expectedCalls, handler.Calls);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(100), factory.CreateClient("plain").Timeout);
    }

    /// <summary>
    /// Preserves cancellation and the native timeout exception rather than returning a fabricated response.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task TimeoutAndCancellation_RemainExceptions()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddHttpClient("timeout").ConfigurePrimaryHttpMessageHandler(() => new WaitingHandler())
            .AddHttpResilience(options =>
            {
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(2);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1);
            });
        await using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("timeout");

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://example.invalid/test", cancellation.Token));
        var exception = await Record.ExceptionAsync(() => client.PostAsync("https://example.invalid/mcp", null));

        Assert.NotNull(exception);
        Assert.Equal("TimeoutRejectedException", exception.GetType().Name);
    }

    /// <summary>
    /// Counts upstream attempts without a socket or a credential-bearing request.
    /// </summary>
    private sealed class CountingHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets the number of requests reaching the upstream boundary.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Returns a transient failure for every attempt so retry limits remain observable.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }
    }

    /// <summary>
    /// Waits on the real pipeline token rather than simulating timeout statuses.
    /// </summary>
    private sealed class WaitingHandler : HttpMessageHandler
    {
        /// <summary>
        /// Stops only when the native timeout or caller cancellation signals its token.
        /// </summary>
        /// <param name="request">The native request sent through the configured HTTP handler pipeline.</param>
        /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
        /// <returns>The actual unread upstream response; the receiving caller must consume and dispose it.</returns>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
