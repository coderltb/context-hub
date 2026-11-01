namespace ContextHub.Infrastructure.Interfaces.Integration;

/// <summary>
/// Keeps hosted-connection entities behind the Infrastructure boundary.
/// </summary>
public interface IExternalConnectionRepository
{
    /// <summary>
    /// Gets stored connections without trusting discovery snapshots.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The current connections.</returns>
    Task<IReadOnlyList<ExternalMcp>> GetConnectionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets a connection by its exact Owner-only name.
    /// </summary>
    /// <param name="name">The exact connection label.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The current entity or null.</returns>
    Task<ExternalMcp?> GetByNameAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a current connection by its protected route identifier.
    /// </summary>
    /// <param name="id">The stored connection identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The current entity or null.</returns>
    Task<ExternalMcp?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts or updates canonical metadata with the expected concurrency version.
    /// </summary>
    /// <param name="entity">The current or new connection entity.</param>
    /// <param name="expectedVersion">The expected version, or null for create.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The committed entity.</returns>
    Task<ExternalMcp> SaveAsync(ExternalMcp entity, uint? expectedVersion, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the exact connection using its expected row version.
    /// </summary>
    /// <param name="entity">The current connection.</param>
    /// <param name="expectedVersion">The caller's current metadata version.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>The completed save operation.</returns>
    Task DeleteAsync(ExternalMcp entity, uint expectedVersion, CancellationToken cancellationToken);
}
