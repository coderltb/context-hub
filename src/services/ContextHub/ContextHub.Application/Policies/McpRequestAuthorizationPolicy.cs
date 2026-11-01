namespace ContextHub.Application.Policies;

/// <summary>
/// Requires the application identity and optional exact tool grant for neutral MCP use cases.
/// </summary>
/// <typeparam name="TRequest">The protected MCP request.</typeparam>
/// <param name="authService">The validated request identity.</param>
public sealed class McpRequestAuthorizationPolicy<TRequest>(IAuthService authService)
    : IRequestAuthorizationPolicy<TRequest> where TRequest : IMcpRequest
{
    /// <summary>
    /// Rejects Owner credentials and ungranted tools before executing business work.
    /// </summary>
    /// <param name="request">The operation and its fixed tool name.</param>
    /// <param name="cancellationToken">Cancels evaluation.</param>
    /// <returns>Whether the current application has the required grant.</returns>
    public ValueTask<bool> IsAllowedAsync(
        TRequest request,
        CancellationToken cancellationToken)
    {
        var allowed = authService.IsAuthenticated &&
            authService.Get<string>(McpApplicationConstants.ACTOR_TYPE) == McpApplicationConstants.CLIENT_ACTOR &&
            authService.HasScope(McpApplicationConstants.MCP_SCOPE) &&
            authService.TryGet<Guid>(McpApplicationConstants.CLIENT_ID, out var clientId) && clientId != Guid.Empty;

        // Discovery has no selected tool; its result is filtered to the client's grants separately.
        return ValueTask.FromResult(allowed && (request.ToolName.Length == 0 ||
            authService.GetAll(McpApplicationConstants.TOOL_CLAIM).Any(claim => claim.Value == request.ToolName)));
    }
}
