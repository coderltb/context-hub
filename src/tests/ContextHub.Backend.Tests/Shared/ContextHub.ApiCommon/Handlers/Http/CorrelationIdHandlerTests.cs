namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Handlers.Http;

/// <summary>
/// Verifies outbound correlation propagation at the actual HTTP message boundary.
/// </summary>
public sealed class CorrelationIdHandlerTests
{
    /// <summary>
    /// Propagates the accepted request identifier without altering a caller's valid explicit identifier.
    /// </summary>
    /// <param name="explicitHeader">Whether the caller supplied its own canonical identifier.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SendAsync_ValidIdentifiers_ArePreserved(bool explicitHeader)
    {
        var contextual = Guid.NewGuid().ToString("D");
        var supplied = Guid.NewGuid().ToString("D");
        var context = new DefaultHttpContext();
        context.Items[ApiCommonConstants.CORRELATION_ID] = contextual;
        using var terminal = new CaptureHandler();
        using var handler = new CorrelationIdHandler(new HttpContextAccessor { HttpContext = context }, NullLogger<CorrelationIdHandler>.Instance)
        {
            InnerHandler = terminal
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/resource");
        if (explicitHeader)
        {
            request.Headers.Add(ApiCommonConstants.CORRELATION_ID, supplied);
        }

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(explicitHeader ? supplied : contextual, terminal.CorrelationId);
    }

    /// <summary>
    /// Removes malformed values and never forwards arbitrary request-header content.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SendAsync_MalformedHeader_IsRemoved()
    {
        var logger = new TestLogger<CorrelationIdHandler>();
        using var terminal = new CaptureHandler();
        using var handler = new CorrelationIdHandler(new HttpContextAccessor(), logger)
        {
            InnerHandler = terminal
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
        request.Headers.Add(ApiCommonConstants.CORRELATION_ID, "private-invalid-value");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Null(terminal.CorrelationId);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.DoesNotContain("private-invalid-value", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Uses validated activity baggage when work executes outside an HTTP request.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SendAsync_BackgroundActivity_PropagatesCanonicalIdentifier()
    {
        var correlationId = Guid.NewGuid().ToString("D");
        using var activity = new Activity("Background operation").Start();
        activity.SetBaggage(ApiCommonConstants.CORRELATION_ID, correlationId);
        using var terminal = new CaptureHandler();
        using var handler = new CorrelationIdHandler(new HttpContextAccessor(), NullLogger<CorrelationIdHandler>.Instance)
        {
            InnerHandler = terminal
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");

        using var response = await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal(correlationId, terminal.CorrelationId);
    }

    /// <summary>
    /// Preserves caller cancellation rather than converting it to a synthetic business or HTTP error.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SendAsync_CancelledCaller_PropagatesCancellation()
    {
        using var terminal = new CaptureHandler();
        using var handler = new CorrelationIdHandler(new HttpContextAccessor(), NullLogger<CorrelationIdHandler>.Instance)
        {
            InnerHandler = terminal
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://example.invalid/");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.SendAsync(request, cancellation.Token));
    }

    /// <summary>
    /// Records only the transport-facing identifier so tests do not inspect implementation internals.
    /// </summary>
    private sealed class CaptureHandler : HttpMessageHandler
    {
        /// <summary>
        /// Gets the identifier received by the terminal transport.
        /// </summary>
        public string? CorrelationId { get; private set; }

        /// <summary>
        /// Captures the forwarded header and returns a synthetic response without network access.
        /// </summary>
        /// <param name="request">The outbound request.</param>
        /// <param name="cancellationToken">The caller's cancellation token.</param>
        /// <returns>A successful synthetic response.</returns>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CorrelationId = request.Headers.TryGetValues(ApiCommonConstants.CORRELATION_ID, out var values)
                ? values.Single()
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
