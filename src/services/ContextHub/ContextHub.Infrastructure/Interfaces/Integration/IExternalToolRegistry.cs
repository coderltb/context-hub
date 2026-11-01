namespace ContextHub.Infrastructure.Interfaces.Integration;

/// <summary>
/// Shares reviewed discovery between Admin preview, metadata refresh and public capability calls.
/// </summary>
public interface IExternalToolRegistry
{
    /// <summary>
    /// Discovers preview definitions without cache writes.
    /// </summary>
    /// <param name="name">The Owner-only connection label.</param>
    /// <param name="config">The checked canonical configuration.</param>
    /// <param name="cancellationToken">Cancels native discovery.</param>
    /// <returns>The bounded Owner-only definitions.</returns>
    Task<IReadOnlyList<ExternalToolResponseDto>> PreviewAsync(string name, ExternalMcpConfigDto config, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all currently enabled eligible capabilities.
    /// </summary>
    /// <param name="cancellationToken">Cancels current state and snapshot reads.</param>
    /// <returns>The independently reviewed current candidates.</returns>
    Task<IReadOnlyList<ExternalToolCandidateModel>> GetCandidatesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a routed candidate against current connection state and definition.
    /// </summary>
    /// <param name="route">The private opaque-reference binding.</param>
    /// <param name="refresh">Whether to re-discover the definition before a call.</param>
    /// <param name="cancellationToken">Cancels authoritative checks and native discovery.</param>
    /// <returns>The permitted candidate or null for invalidated state.</returns>
    Task<ExternalToolCandidateModel?> ResolveAsync(ExternalToolRouteModel route, bool refresh, CancellationToken cancellationToken);

    /// <summary>
    /// Gets only existing snapshot metadata without initiating discovery.
    /// </summary>
    /// <param name="connection">The current database authority.</param>
    /// <param name="cancellationToken">Cancels the cache read.</param>
    /// <returns>The derived summary.</returns>
    Task<ExternalMcpDiscoveryDto> GetSummaryAsync(ExternalMcp connection, CancellationToken cancellationToken);

    /// <summary>
    /// Refreshes enabled connections inside the caller's current scope.
    /// </summary>
    /// <param name="cancellationToken">Cancels refresh work.</param>
    /// <returns>The completed refresh iteration.</returns>
    Task RefreshAsync(CancellationToken cancellationToken);
}
