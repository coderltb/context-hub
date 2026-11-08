namespace ContextHub.Infrastructure.Interfaces.Mcp;

/// <summary>
/// Defines the feature-specific persistence boundary owned by Infrastructure.
/// </summary>
public interface IToolRepository
{
    /// <summary>
    /// Reads one stored tool including both schemas, even when it is disabled.
    /// </summary>
    /// <param name="toolId">The immutable Owner-facing identity.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The current configuration.</returns>
    Task<Tool?> GetDetailAsync(Guid toolId, CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<IReadOnlyList<ToolStateModel>> GetStatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Projects both JSON contracts from one authoritative row read.
    /// </summary>
    /// <param name="toolName">An exact tool name, or null for discovery.</param>
    /// <param name="cancellationToken">Cancels the projection.</param>
    /// <returns>The detached contract projections.</returns>
    Task<IReadOnlyList<McpToolContractModel>> GetContractsAsync(
        string? toolName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<IReadOnlyList<string>> GetConnectionNamesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="toolId">The selected registered tool UUID.</param>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<Tool> SaveStateAsync(
        Guid toolId,
        UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Filters and pages Owner metadata before materialization.
    /// </summary>
    /// <param name="request">The query, status and bounded page.</param>
    /// <param name="cancellationToken">Cancels count and item queries.</param>
    /// <returns>The provider-projected metadata page.</returns>
    Task<PageResponse<ToolStateModel>> GetPageAsync(
        GetMcpToolsRequestDto request,
        CancellationToken cancellationToken);
}
