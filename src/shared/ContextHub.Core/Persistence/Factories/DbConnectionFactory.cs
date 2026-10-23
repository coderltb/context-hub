namespace ContextHub.Core.Persistence.Factories;

/// <summary>
/// Uses the shared Npgsql pool without storing or logging connection strings.
/// </summary>
public sealed class DbConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// Retains the host-owned pool; the factory must never dispose it.
    /// </summary>
    private readonly NpgsqlDataSource _dataSource;

    /// <summary>
    /// Binds the factory to the one data source selected by the host.
    /// </summary>
    /// <param name="dataSource">The DI-owned PostgreSQL data source.</param>
    public DbConnectionFactory(NpgsqlDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);

        _dataSource = dataSource;
    }

    /// <summary>
    /// Acquires a pooled connection whose lifetime belongs to the caller.
    /// </summary>
    /// <param name="cancellationToken">The acquisition cancellation token.</param>
    /// <returns>The open connection, not the data source.</returns>
    public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default) =>
        await _dataSource.OpenConnectionAsync(cancellationToken);
}
