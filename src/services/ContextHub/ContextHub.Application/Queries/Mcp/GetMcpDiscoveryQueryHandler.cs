namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Filters discovery without treating client-side cached definitions as an authorization decision.
/// </summary>
/// <param name="toolStateService">The authoritative global tool state.</param>
/// <param name="authService">The current introspected application grants.</param>
public sealed class GetMcpDiscoveryQueryHandler(
    IToolStateService toolStateService,
    IAuthService authService)
    : IRequestHandler<GetMcpDiscoveryQuery, IReadOnlyList<string>>
{
    /// <summary>
    /// Reads current state for every advertised tool before returning its name.
    /// </summary>
    /// <param name="request">The protected discovery request.</param>
    /// <param name="cancellationToken">Cancels authoritative reads.</param>
    /// <returns>The enabled and granted names in fixed discovery order.</returns>
    public async ValueTask<IReadOnlyList<string>> Handle(
        GetMcpDiscoveryQuery request,
        CancellationToken cancellationToken)
    {
        var grants = authService.GetAll(McpApplicationConstants.TOOL_CLAIM)
            .Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        var enabled = await toolStateService.GetEnabledNamesAsync(cancellationToken);
        return enabled.Where(grants.Contains).ToArray();
    }
}
