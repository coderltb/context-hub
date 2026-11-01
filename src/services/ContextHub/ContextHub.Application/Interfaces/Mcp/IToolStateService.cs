namespace ContextHub.Application.Interfaces.Mcp;

/// <summary>
/// Defines an Application-owned port for privacy-safe feature operations.
/// </summary>
public interface IToolStateService
{
    /// <summary>
    /// Reads current input and output contracts together without caching authority.
    /// </summary>
    /// <param name="toolName">The selected name, or null for the fixed catalog.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The current paired contracts.</returns>
    Task<IReadOnlyList<McpToolContractModel>> GetContractsAsync(
        string? toolName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads enabled fixed names in one authoritative snapshot without routing metadata or a positive-state cache.
    /// </summary>
    /// <param name="cancellationToken">Cancels the current database read.</param>
    /// <returns>The enabled fixed names in discovery order.</returns>
    Task<IReadOnlyList<string>> GetEnabledNamesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the complete fixed tool-state list.
    /// </summary>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<McpToolsResponseDto> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates global enablement with optimistic concurrency.
    /// </summary>
    /// <param name="toolName">The approved toolName input.</param>
    /// <param name="request">The approved request input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<McpToolResponseDto> UpdateAsync(
        string toolName,
        UpdateMcpToolRequestDto request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Checks live authority without a positive-state cache.
    /// </summary>
    /// <param name="toolName">The approved toolName input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task<bool> IsEnabledAsync(string toolName, CancellationToken cancellationToken);
}
