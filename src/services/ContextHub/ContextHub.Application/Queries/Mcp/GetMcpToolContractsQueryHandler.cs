namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Filters current database contracts to the authenticated application's grants.
/// </summary>
/// <param name="toolStateService">The authoritative contract port.</param>
/// <param name="authService">The current introspected capability grants.</param>
public sealed class GetMcpToolContractsQueryHandler(
    IToolStateService toolStateService,
    IAuthService authService)
    : IRequestHandler<GetMcpToolContractsQuery, IReadOnlyList<McpToolContractModel>>
{
    /// <summary>
    /// Reads one row pair for invocation or one batch for discovery.
    /// </summary>
    /// <param name="request">The authorized capability selector.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>Only contracts granted to the current application.</returns>
    public async ValueTask<IReadOnlyList<McpToolContractModel>> Handle(
        GetMcpToolContractsQuery request,
        CancellationToken cancellationToken)
    {
        var grants = authService.GetAll(McpApplicationConstants.TOOL_CLAIM)
            .Select(claim => claim.Value).ToHashSet(StringComparer.Ordinal);
        var contracts = await toolStateService.GetContractsAsync(request.ToolName.Length == 0 ? null : request.ToolName, cancellationToken);

        return contracts.Where(contract => grants.Contains(contract.Name)).ToArray();
    }
}
