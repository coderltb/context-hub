namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Exercises Dapper cardinality, resource lifetime and transaction borrowing against isolated PostgreSQL.
/// </summary>
/// <param name="postgreSql">The shared test-only PostgreSQL fixture.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class DapperPostgreSqlTests(PostgreSqlFixture postgreSql)
{
    /// <summary>
    /// Binds parameters and materializes query results before releasing the owned connection.
    /// </summary>
    /// <returns>The asynchronous cardinality assertions.</returns>
    [Fact]
    public async Task Queries_PreserveCardinalityAndReleaseOwnedConnections()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new TrackingFactory(source);
        var logger = new TestLogger<DapperService>();
        var service = new DapperService(factory, logger);
        var rows = await service.QueryAsync<int>("select @number union all select @number + 1", new { number = 41 });
        Assert.Equal([41, 42], rows);
        Assert.Equal(ConnectionState.Closed, factory.LastConnection!.State);
        Assert.Equal(41, await service.QueryFirstAsync<int>("select 41 union all select 42"));
        Assert.Equal(0, await service.QueryFirstOrDefaultAsync<int>("select 1 where false"));
        Assert.Null(await service.QuerySingleOrDefaultAsync<string>("select 'one' where false"));
        Assert.Equal("value", await service.QuerySingleAsync<string>("select @value", new { value = "value" }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QuerySingleAsync<int>("select 1 where false"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QuerySingleAsync<int>("select 1 union all select 2"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QuerySingleOrDefaultAsync<int>("select 1 union all select 2"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueryFirstAsync<int>("select 1 where false"));
        Assert.Equal(7, await service.ExecuteScalarAsync<int>("select @value", new { value = 7 }));

        var messages = string.Join('\n', logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain("select", messages, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", messages, StringComparison.OrdinalIgnoreCase);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    /// <summary>
    /// Disposes multiple-result readers even when the callback throws its original failure.
    /// </summary>
    /// <returns>The asynchronous reader-lifetime assertions.</returns>
    [Fact]
    public async Task MultipleResults_AreBoundedByCallbackLifetime()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        var results = await service.QueryMultipleAsync("select 1; select 'two'", async grid =>
            (await grid.ReadSingleAsync<int>(), await grid.ReadSingleAsync<string>()));
        Assert.Equal((1, "two"), results);
        Assert.Equal(ConnectionState.Closed, factory.LastConnection!.State);

        SqlMapper.GridReader? escaped = null;
        var failure = new InvalidOperationException("test callback failure");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => service.QueryMultipleAsync<int>("select 1", grid =>
        {
            escaped = grid;
            throw failure;
        }));
        Assert.Same(failure, actual);
        Assert.Throws<ObjectDisposedException>(() => escaped!.Read<int>());
        Assert.Equal(ConnectionState.Closed, factory.LastConnection.State);
    }

    /// <summary>
    /// Keeps EF in charge of the borrowed transaction, including after command failure.
    /// </summary>
    /// <returns>The asynchronous ownership assertions.</returns>
    [Fact]
    public async Task BorrowedEfTransaction_IsNotCompletedOrDisposed()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        await using var context = new CoreTestDbContext(new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(postgreSql.ConnectionString).Options);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(DapperCommandOptionsHelper.CreateTransactionalText(context)));
        // Explicitly open the connection as the caller. EF is allowed to close connections it opened itself after rollback.
        await context.Database.OpenConnectionAsync();

        await using var transaction = await context.Database.BeginTransactionAsync();
        var options = DapperCommandOptionsHelper.CreateTransactionalText(context);

        await service.ExecuteAsync("create temporary table dapper_ownership(value int) on commit drop", options: options);
        Assert.Equal(1, await service.ExecuteAsync("insert into dapper_ownership values (@value)", new { value = 9 }, options));
        Assert.Equal(9, await service.QuerySingleAsync<int>("select value from dapper_ownership", options: options));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.QuerySingleAsync<int>("select 1 union all select 2", options: options));
        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Open, context.Database.GetDbConnection().State);
        Assert.Equal(0, factory.Calls);

        // A real PostgreSQL error aborts the borrowed transaction; the facade must not silently roll it back.
        var sqlFailure = await Assert.ThrowsAsync<PostgresException>(() =>
            service.ExecuteAsync("select 1 / 0", options: options));
        Assert.Equal(PostgresErrorCodes.DivisionByZero, sqlFailure.SqlState);
        Assert.Same(transaction, context.Database.CurrentTransaction);
        var aborted = await Assert.ThrowsAsync<PostgresException>(() =>
            service.QuerySingleAsync<int>("select 1", options: options));
        Assert.Equal(PostgresErrorCodes.InFailedSqlTransaction, aborted.SqlState);
        Assert.Equal(0, factory.Calls);

        await transaction.RollbackAsync();
        Assert.Equal(ConnectionState.Open, context.Database.GetDbConnection().State);
        Assert.Equal(1, await service.QuerySingleAsync<int>("select 1"));
    }

    /// <summary>
    /// Preserves cancellation and native timeout failures while releasing owned resources.
    /// </summary>
    /// <returns>The asynchronous cancellation and timeout assertions.</returns>
    [Fact]
    public async Task CancellationAndTimeout_PreserveProviderBehavior()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync("select pg_sleep(5)",
            options: new DapperCommandOptions { CancellationToken = cancellation.Token }));
        Assert.Equal(ConnectionState.Closed, factory.LastConnection!.State);

        await Assert.ThrowsAnyAsync<NpgsqlException>(() => service.ExecuteAsync("select pg_sleep(5)",
            options: new DapperCommandOptions { CommandTimeout = 1 }));
        Assert.Equal(ConnectionState.Closed, factory.LastConnection.State);
    }

    /// <summary>
    /// Observes native connection lifetime without substituting a database provider.
    /// </summary>
    internal sealed class TrackingFactory : IDbConnectionFactory
    {
        /// <summary>
        /// Retains the test-owned PostgreSQL pool.
        /// </summary>
        private readonly NpgsqlDataSource _source;

        /// <summary>
        /// Counts owned acquisitions, which must remain zero for borrowed transactions.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Exposes the last connection solely for disposal assertions.
        /// </summary>
        public DbConnection? LastConnection { get; private set; }

        /// <summary>
        /// Binds the tracking factory to the real test pool.
        /// </summary>
        /// <param name="source">The test-owned data source.</param>
        public TrackingFactory(NpgsqlDataSource source) => _source = source;

        /// <summary>
        /// Opens one native pooled connection and records it.
        /// </summary>
        /// <param name="cancellationToken">The forwarded acquisition cancellation token.</param>
        /// <returns>The caller-owned open connection.</returns>
        public async Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            LastConnection = await _source.OpenConnectionAsync(cancellationToken);

            return LastConnection;
        }
    }
}
