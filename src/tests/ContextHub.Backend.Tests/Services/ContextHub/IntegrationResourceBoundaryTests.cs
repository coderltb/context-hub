namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies failed native response buffering retains its original failure when owned cleanup also fails.
/// </summary>
public sealed class IntegrationResourceBoundaryTests
{
    /// <summary>
    /// Preserves the native body-read failure while reporting only secondary cleanup metadata.
    /// </summary>
    /// <returns>The primary-failure, disposal and safe-log assertions.</returns>
    [Fact]
    public async Task BufferingFailureIsNotMaskedByResponseDisposal()
    {
        var primary = new IOException("private-provider-read-message");
        var content = new FailedContent(primary);
        var logger = new TestLogger<ExternalMcpSafetyHandler>();
        using var handler = new ExternalMcpSafetyHandler(logger) { InnerHandler = new ResponseHandler(content) };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/mcp");

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => invoker.SendAsync(request, CancellationToken.None));

        Assert.True(ReferenceEquals(primary, failure) || ReferenceEquals(primary, failure.InnerException));
        Assert.Equal(1, content.DisposalAttempts);
        var warning = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Null(warning.Exception);
        Assert.DoesNotContain("private-provider", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("private-cleanup", warning.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Keeps cancellation distinguishable even when a provider response cannot dispose cleanly.
    /// </summary>
    /// <returns>The cancellation and attempted-cleanup assertions.</returns>
    [Fact]
    public async Task BufferingCancellationIsNotMaskedByResponseDisposal()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var content = new FailedContent(new OperationCanceledException(cancellation.Token));
        var logger = new TestLogger<ExternalMcpSafetyHandler>();
        using var handler = new ExternalMcpSafetyHandler(logger) { InnerHandler = new ResponseHandler(content) };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://example.com/mcp");

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invoker.SendAsync(request, cancellation.Token));

        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(1, content.DisposalAttempts);
        Assert.Single(logger.Entries);
    }

    private sealed class ResponseHandler(HttpContent content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
    }

    private sealed class FailedContent(Exception primary) : HttpContent
    {
        public int DisposalAttempts { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.FromException(primary);

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken) =>
            Task.FromException(primary);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            DisposalAttempts++;
            base.Dispose(disposing);
            throw new InvalidOperationException("private-cleanup-message");
        }
    }
}
