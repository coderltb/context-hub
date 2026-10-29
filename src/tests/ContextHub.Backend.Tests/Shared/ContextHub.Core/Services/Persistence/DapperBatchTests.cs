namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Exercises native enumerable parameters without introducing a batch wrapper or implicit transaction.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class DapperBatchTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets only the disposable test schema before each batch scenario.
    /// </summary>
    /// <returns>The asynchronous schema reset.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves container disposal with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Binds every batch item, returns the affected total and treats an empty batch as a native no-op.
    /// </summary>
    /// <returns>The stored values, affected counts and resource-release assertions.</returns>
    [Fact]
    public async Task EnumerableParametersBindEveryItemAndEmptyBatchDoesNothing()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        IDapperService service = new DapperService(factory, NullLogger<DapperService>.Instance);
        var rows = new[]
        {
            new TestEntity { Id = Guid.NewGuid(), Name = "First" },
            new TestEntity { Id = Guid.NewGuid(), Name = "Second" }
        };
        var sql = "insert into \"CoreEntity\" (\"Id\", \"Name\") values (@Id, @Name)";

        Assert.Equal(2, await service.ExecuteAsync(sql, rows));
        Assert.Equal(ConnectionState.Closed, factory.LastConnection!.State);
        Assert.Equal(0, await service.ExecuteAsync(sql, Array.Empty<TestEntity>()));
        Assert.Equal(ConnectionState.Closed, factory.LastConnection.State);
        Assert.Equal(["First", "Second"], await service.QueryAsync<string>(
            "select \"Name\" from \"CoreEntity\" order by \"Name\""));
    }

    /// <summary>
    /// Keeps a successful batch invisible until the borrowed EF transaction owner commits it.
    /// </summary>
    /// <returns>The independent visibility, acquisition and ownership assertions.</returns>
    [Fact]
    public async Task BorrowedBatchWaitsForCallerCommit()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var options = DapperCommandOptionsHelper.CreateTransactionalText(context);
        var rows = new[] { new { Id = Guid.NewGuid(), Name = "First" }, new { Id = Guid.NewGuid(), Name = "Second" } };

        Assert.Equal(2, await service.ExecuteAsync(
            "insert into \"CoreEntity\" (\"Id\", \"Name\") values (@Id, @Name)", rows, options));
        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Equal(0, factory.Calls);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(0, await observer.Entities.CountAsync());

        // The facade must neither commit nor roll back a caller-owned batch transaction.
        await transaction.CommitAsync();

        Assert.Equal(2, await observer.Entities.CountAsync());
    }

    /// <summary>
    /// Preserves a failed batch's PostgreSQL error and leaves rollback of earlier items to the transaction owner.
    /// </summary>
    /// <returns>The SQLSTATE, aborted-transaction and final rollback assertions.</returns>
    [Fact]
    public async Task FailedBorrowedBatchRequiresCallerRollback()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        var options = DapperCommandOptionsHelper.CreateTransactionalText(context);
        var id = Guid.NewGuid();
        var rows = new[] { new { Id = id, Name = "First" }, new { Id = id, Name = "Duplicate" } };

        var failure = await Assert.ThrowsAsync<PostgresException>(() => service.ExecuteAsync(
            "insert into \"CoreEntity\" (\"Id\", \"Name\") values (@Id, @Name)", rows, options));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Same(transaction, context.Database.CurrentTransaction);
        var aborted = await Assert.ThrowsAsync<PostgresException>(() => service.ExecuteScalarAsync<int>("select 1", options: options));
        Assert.Equal(PostgresErrorCodes.InFailedSqlTransaction, aborted.SqlState);
        Assert.Equal(0, factory.Calls);
        await transaction.RollbackAsync();

        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(0, await observer.Entities.CountAsync());
        Assert.Equal(ConnectionState.Open, context.Database.GetDbConnection().State);
    }

    /// <summary>
    /// Cancels an executing native batch without retrying it, retaining the cancellation and releasing resources.
    /// </summary>
    /// <returns>The cancellation, persisted-row and disposal assertions.</returns>
    [Fact]
    public async Task BatchCancellationDoesNotBecomeAFakeResponseOrRetry()
    {
        await using var source = NpgsqlDataSource.Create(postgreSql.ConnectionString);
        var factory = new DapperPostgreSqlTests.TrackingFactory(source);
        var service = new DapperService(factory, NullLogger<DapperService>.Instance);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var options = new DapperCommandOptions { CancellationToken = cancellation.Token };
        var rows = new[] { new { Id = Guid.NewGuid(), Name = "Cancelled" } };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExecuteAsync(
            "insert into \"CoreEntity\" (\"Id\", \"Name\") select @Id, @Name from pg_sleep(5)", rows, options));

        Assert.Equal(1, factory.Calls);
        Assert.Equal(ConnectionState.Closed, factory.LastConnection!.State);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(0, await observer.Entities.CountAsync());
    }
}
