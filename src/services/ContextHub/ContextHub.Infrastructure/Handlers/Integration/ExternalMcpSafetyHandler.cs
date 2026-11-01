namespace ContextHub.Infrastructure.Handlers.Integration;

/// <summary>
/// Bounds native MCP response buffering and preserves redirect and endpoint restrictions.
/// </summary>
/// <param name="logger">The safe cleanup diagnostic logger.</param>
public sealed class ExternalMcpSafetyHandler(ILogger<ExternalMcpSafetyHandler> logger) : DelegatingHandler
{
    /// <summary>
    /// Checks the actual URI and bounds content before the native MCP parser receives it.
    /// </summary>
    /// <param name="request">The native protocol request.</param>
    /// <param name="cancellationToken">The complete HttpClient operation deadline and caller cancellation.</param>
    /// <returns>The bounded native response.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ExternalMcpImportHelper.ValidateEndpoint(request.RequestUri!.AbsoluteUri);
        var response = await base.SendAsync(request, cancellationToken);
        try
        {
            // Native buffering includes streaming bodies in the HttpClient timeout and does not read upstream twice.
            await response.Content.LoadIntoBufferAsync(IntegrationInfrastructureConstants.MAX_RESPONSE_BYTES, cancellationToken);
            return response;
        }
        catch
        {
            try
            {
                response.Dispose();
            }
            catch (Exception cleanupException)
            {
                // Cleanup is secondary; preserve the body or cancellation failure for the native caller.
                logger.LogWarning(IntegrationLogConstants.RESPONSE_CLEANUP_FAILED, cleanupException.GetType().Name);
            }
            throw;
        }
    }
}
