namespace ContextHub.Infrastructure.Interfaces.Mcp;

/// <summary>
/// Defines the feature-specific persistence boundary owned by Infrastructure.
/// </summary>
public interface IToolRepository
{
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
    /// <param name="toolName">The approved toolName input.</param>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task<Tool> SaveStateAsync(string toolName, UpdateMcpToolRequestDto request, CancellationToken cancellationToken);
}
