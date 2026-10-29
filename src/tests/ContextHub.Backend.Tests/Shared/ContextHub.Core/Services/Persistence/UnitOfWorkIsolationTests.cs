namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Verifies explicit native isolation while preserving ownership of caller-supplied PostgreSQL transactions.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable PostgreSQL database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class UnitOfWorkIsolationTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Recreates only the fixture-owned database before each scenario.
    /// </summary>
    /// <returns>The isolated schema reset.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves container disposal with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Runs each callback form at Serializable isolation and persists its work at the owned boundary.
    /// </summary>
    /// <param name="returnsValue">Whether the callback returns a result.</param>
    /// <returns>The observed native isolation and durable commit assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedTransactionUsesRequestedIsolation(bool returnsValue)
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);
        using var cancellation = new CancellationTokenSource();
        var called = false;

        Task<int> StageAsync(CancellationToken token)
        {
            called = true;
            Assert.Equal(cancellation.Token, token);
            Assert.Equal(IsolationLevel.Serializable, context.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Explicit isolation" });

            return Task.FromResult(7);
        }

        if (returnsValue)
        {
            Assert.Equal(7, await work.ExecuteInTransactionAsync(StageAsync, IsolationLevel.Serializable, cancellation.Token));
        }
        else
        {
            await work.ExecuteInTransactionAsync(async token => { await StageAsync(token); },
                IsolationLevel.Serializable, cancellation.Token);
        }

        Assert.True(called);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(1, context.SaveCallCount);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal("Explicit isolation", (await observer.Entities.SingleAsync()).Name);
    }

    /// <summary>
    /// Reuses a matching caller transaction without saving or completing it in either overload.
    /// </summary>
    /// <param name="returnsValue">Whether the callback returns a result.</param>
    /// <returns>The borrowed ownership and caller-controlled commit assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MatchingBorrowedIsolationLeavesCompletionWithCaller(bool returnsValue)
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

        Task<int> StageAsync(CancellationToken token)
        {
            Assert.Same(transaction, context.Database.CurrentTransaction);
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Caller isolation" });

            return Task.FromResult(9);
        }

        if (returnsValue)
        {
            Assert.Equal(9, await work.ExecuteInTransactionAsync(StageAsync, IsolationLevel.ReadCommitted));
        }
        else
        {
            await work.ExecuteInTransactionAsync(async token => { await StageAsync(token); }, IsolationLevel.ReadCommitted);
        }

        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Equal(0, context.SaveCallCount);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());

        // A borrowed callback only stages work; the caller remains responsible for making it durable.
        await work.SaveChangesAsync();
        await transaction.CommitAsync();

        Assert.Equal("Caller isolation", (await observer.Entities.SingleAsync()).Name);
    }

    /// <summary>
    /// Rejects an isolation mismatch before callback work while leaving the existing transaction usable by its owner.
    /// </summary>
    /// <param name="returnsValue">Whether the callback returns a result.</param>
    /// <returns>The no-callback, no-save and retained transaction assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BorrowedIsolationMismatchRejectsBeforeCallback(bool returnsValue)
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var work = new UnitOfWork<CoreTestDbContext>(context, logger);
        var called = false;

        Task<int> StageAsync(CancellationToken token)
        {
            called = true;

            return Task.FromResult(1);
        }

        var failure = returnsValue
            ? await Assert.ThrowsAsync<InvalidOperationException>(() =>
                work.ExecuteInTransactionAsync(StageAsync, IsolationLevel.Serializable))
            : await Assert.ThrowsAsync<InvalidOperationException>(() =>
                work.ExecuteInTransactionAsync(async token => { await StageAsync(token); }, IsolationLevel.Serializable));

        Assert.Equal(CoreErrorConstants.DatabaseErrors.TRANSACTION_ISOLATION_CONFLICT, failure.Message);
        Assert.False(called);
        Assert.Equal(0, context.SaveCallCount);
        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Warning);

        // Rejection must not roll back or dispose the owner's transaction.
        context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Owner continues" });
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal("Owner continues", (await observer.Entities.SingleAsync()).Name);
    }
}
