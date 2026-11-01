namespace ContextHub.Application.Interfaces.Integration;

/// <summary>
/// Owns validated hosted connections without exposing upstream configuration to MCP clients.
/// </summary>
public interface IExternalMcpService
{
    /// <summary>
    /// Normalizes imports and discovers metadata without persisting any changes.
    /// </summary>
    /// <param name="import">The bounded supported import document.</param>
    /// <param name="cancellationToken">Cancels network discovery.</param>
    /// <returns>Canonical configurations and safe discovery metadata.</returns>
    Task<ExternalMcpPreviewResponseDto> PreviewAsync(JsonElement import, CancellationToken cancellationToken);

    /// <summary>
    /// Gets saved connections and their rebuildable discovery summaries.
    /// </summary>
    /// <param name="cancellationToken">Cancels database and cache reads.</param>
    /// <returns>The Owner-only connection list.</returns>
    Task<ExternalMcpListResponseDto> GetListAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves a canonical configuration using optimistic concurrency.
    /// </summary>
    /// <param name="name">The Owner-only connection label.</param>
    /// <param name="request">The canonical configuration and expected version.</param>
    /// <param name="cancellationToken">Cancels validation and persistence.</param>
    /// <returns>The committed canonical metadata.</returns>
    Task<ExternalMcpResponseDto> SaveAsync(string name, SaveExternalMcpRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a connection and invalidates derived discovery references.
    /// </summary>
    /// <param name="name">The exact saved connection label.</param>
    /// <param name="concurrencyToken">The opaque current version.</param>
    /// <param name="cancellationToken">Cancels persistence and cache invalidation.</param>
    /// <returns>The completed deletion result.</returns>
    Task<DeletedIntegrationResponseDto> DeleteAsync(string name, string concurrencyToken, CancellationToken cancellationToken);
}
