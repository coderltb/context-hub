namespace ContextHub.Core.Interfaces.Persistence;

/// <summary>
/// Opens pooled PostgreSQL connections from the host's DI-owned data source.
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// Opens one connection; its caller must dispose it after use.
    /// </summary>
    /// <param name="cancellationToken">Cancels acquisition without changing the pool ownership.</param>
    /// <returns>An open caller-owned connection.</returns>
    Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default);
}
