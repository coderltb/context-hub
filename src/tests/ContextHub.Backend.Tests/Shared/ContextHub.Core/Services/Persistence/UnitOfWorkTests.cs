namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Verifies save boundaries, transaction ownership, rollback and cancellation against real PostgreSQL.
/// </summary>
/// <param name="postgreSql">The shared PostgreSQL fixture supplying the real integration-test database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class UnitOfWorkTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets the shared test database before this test instance executes.
    /// </summary>
    /// <returns>A task that completes after the fixture has initialized its test dependencies.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Completes per-test cleanup; the collection fixture owns container disposal.
    /// </summary>
    /// <returns>A task that completes after fixture-owned resources have been released.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Verifies that an explicit save persists the change with exactly one context save call.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task SaveChangesDelegatesToOwnedContextOnce()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var unitOfWork = CreateUnitOfWork(context);
        context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Saved" });

        var changes = await unitOfWork.SaveChangesAsync();

        Assert.Equal(1, changes);
        Assert.Equal(1, context.SaveCallCount);
    }

    /// <summary>
    /// Verifies that an owned transaction saves once, commits and emits lifecycle logs.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task OwnedTransactionSavesOnceAndCommitsOnSuccess()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var unitOfWork = CreateUnitOfWork(context, logger);

        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Committed" });
            return Task.CompletedTask;
        });

        Assert.Equal(1, context.SaveCallCount);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Debug && entry.Message.Contains("Started", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Debug && entry.Message.Contains("Committed", StringComparison.Ordinal));
        await using var verification = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.True(await verification.Entities.AnyAsync(entity => entity.Name == "Committed"));
    }

    /// <summary>
    /// Verifies rollback, safe failure logging and preservation of the original exception instance.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task OwnedTransactionRollsBackAndPreservesOriginalException()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var unitOfWork = CreateUnitOfWork(context, logger);
        var expected = new InvalidOperationException("original");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
            {
                context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Rolled back" });
                await context.SaveChangesAsync(cancellationToken);
                throw expected;
            }));

        Assert.Same(expected, actual);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning
            && entry.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal));
        await using var verification = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.False(await verification.Entities.AnyAsync(entity => entity.Name == "Rolled back"));
    }

    /// <summary>
    /// Verifies that Unit of Work neither commits nor disposes a caller-owned transaction.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ExistingTransactionRemainsOwnedByCaller()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var unitOfWork = CreateUnitOfWork(context, logger);
        await using var transaction = await context.Database.BeginTransactionAsync();

        await unitOfWork.ExecuteInTransactionAsync(async cancellationToken =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Outer" });
            await context.SaveChangesAsync(cancellationToken);
        });

        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Debug && entry.Message.Contains("caller-owned", StringComparison.Ordinal));
        await using (var beforeRollback = PostgreSqlTestContext.Create(postgreSql.ConnectionString))
        {
            Assert.False(await beforeRollback.Entities.AnyAsync(entity => entity.Name == "Outer"));
        }

        await transaction.RollbackAsync();
        await using var afterRollback = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.False(await afterRollback.Entities.AnyAsync(entity => entity.Name == "Outer"));
    }

    /// <summary>
    /// Verifies that a transaction returns its callback result and commits staged changes.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task GenericTransactionReturnsResultAndCommits()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var unitOfWork = CreateUnitOfWork(context);

        var result = await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Result" });
            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(1, context.SaveCallCount);
    }

    /// <summary>
    /// Verifies that cancellation is propagated without being translated into a business error.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task CancellationRemainsCancellation()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var unitOfWork = CreateUnitOfWork(context);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => unitOfWork.ExecuteInTransactionAsync(
                token => Task.Delay(TimeSpan.FromSeconds(1), token),
                cancellation.Token));
    }

    /// <summary>
    /// Creates Unit of Work for the test context with an optional capturing logger.
    /// </summary>
    /// <param name="context">The operation context supplied by the framework.</param>
    /// <param name="logger">The fixture logger used to inspect safe operational metadata.</param>
    /// <returns>The unit of work using the fixture context and optional test logger.</returns>
    private static UnitOfWork<CoreTestDbContext> CreateUnitOfWork(
        CoreTestDbContext context,
        ILogger<UnitOfWork<CoreTestDbContext>>? logger = null) =>
        new(context, logger ?? NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

}
