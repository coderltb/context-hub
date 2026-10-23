namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.UnitOfWork;

/// <summary>
/// Checks that an owned transaction preserves EF changes until PostgreSQL commit succeeds, including native retries.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable PostgreSQL database.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class UnitOfWorkRetryTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets only the disposable database before each retry scenario.
    /// </summary>
    /// <returns>The isolated schema reset.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves container cleanup to its collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Persists pending inserts and updates when the first commit fails before reaching PostgreSQL.
    /// </summary>
    /// <param name="inserting">Whether the pending entity is new rather than loaded from the database.</param>
    /// <param name="returnsValue">Whether the value-returning transaction overload is used.</param>
    /// <returns>The independent database, replay count and tracker-state assertions.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RetryAfterCommitFailure_PersistsPendingChanges(bool inserting, bool returnsValue)
    {
        var id = Guid.NewGuid();
        if (!inserting)
        {
            await using var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
            seed.Add(new TestEntity { Id = id, Name = "Original" });
            await seed.SaveChangesAsync();
        }

        var fault = new CommitFault(1);
        var options = new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(postgreSql.ConnectionString, provider => provider.EnableRetryOnFailure(1, TimeSpan.Zero, null))
            .AddInterceptors(fault).Options;
        await using var context = new CoreTestDbContext(options);
        var entity = inserting ? new TestEntity { Id = id, Name = "Original" } : await context.Entities.SingleAsync();
        if (inserting)
        {
            context.Add(entity);
        }

        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);
        var calls = 0;
        Func<CancellationToken, Task<int>> action = _ =>
        {
            calls++;
            entity.Name = "Target";

            return Task.FromResult(7);
        };

        // The callback deliberately reuses the tracked entity: accepting the first save would lose the rolled-back change.
        if (returnsValue)
        {
            Assert.Equal(7, await work.ExecuteInTransactionAsync(action));
        }
        else
        {
            await work.ExecuteInTransactionAsync(async token => { await action(token); });
        }

        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var persisted = await observer.Entities.AsNoTracking().SingleAsync();

        Assert.Equal(id, persisted.Id);
        Assert.Equal("Target", persisted.Name);
        Assert.Equal(2, calls);
        Assert.Equal(2, fault.Calls);
        Assert.Equal(EntityState.Unchanged, context.Entry(entity).State);
        Assert.False(work.HasActiveTransaction);
    }

    /// <summary>
    /// Leaves failed owned writes pending instead of accepting state that PostgreSQL rolled back.
    /// </summary>
    /// <returns>The original exception, stored value and unaccepted tracker assertions.</returns>
    [Fact]
    public async Task CommitFailureWithoutRetry_DoesNotAcceptPendingChanges()
    {
        var fault = new CommitFault(int.MaxValue, transient: false);
        var options = new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(postgreSql.ConnectionString).AddInterceptors(fault).Options;
        await using var context = new CoreTestDbContext(options);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Pending" };
        context.Add(entity);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

        var failure = await Assert.ThrowsAsync<IOException>(() => work.ExecuteInTransactionAsync(_ => Task.CompletedTask));

        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());
        Assert.Same(fault.Failure, failure);
        Assert.Equal(EntityState.Added, context.Entry(entity).State);
        Assert.False(work.HasActiveTransaction);
    }

    /// <summary>
    /// Keeps the public save method's ordinary EF acceptance behavior outside the owned transaction helper.
    /// </summary>
    /// <returns>The successful save and accepted tracker-state assertions.</returns>
    [Fact]
    public async Task PublicSave_StillAcceptsChanges()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var entity = new TestEntity { Id = Guid.NewGuid(), Name = "Saved" };
        context.Add(entity);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

        Assert.Equal(1, await work.SaveChangesAsync());
        Assert.Equal(EntityState.Unchanged, context.Entry(entity).State);
        Assert.Equal(0, await work.SaveChangesAsync());
    }

    /// <summary>
    /// Injects a native retriable PostgreSQL error before commit so rollback is known, not ambiguous.
    /// </summary>
    /// <param name="failures">The number of initial commit attempts that must fail.</param>
    /// <param name="transient">Whether native PostgreSQL retry should classify the injected failure as transient.</param>
    private sealed class CommitFault(int failures, bool transient = true) : DbTransactionInterceptor
    {
        /// <summary>
        /// Counts actual commit attempts made by the transaction helper.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Retains the original transient or ordinary commit failure to detect accidental wrapping.
        /// </summary>
        public Exception Failure { get; } = transient
            ? new PostgresException("Fixture transient commit failure", "ERROR", "ERROR", "40001")
            : new IOException("Fixture commit failure");

        /// <summary>
        /// Rejects selected commit attempts before invoking the real PostgreSQL commit.
        /// </summary>
        /// <param name="transaction">The fixture-owned transaction containing real database writes.</param>
        /// <param name="eventData">The native transaction metadata.</param>
        /// <param name="result">The existing interception result.</param>
        /// <param name="cancellationToken">The forwarded operation cancellation.</param>
        /// <returns>The unchanged interception result after the configured failures have occurred.</returns>
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (++Calls <= failures)
            {
                throw Failure;
            }

            return ValueTask.FromResult(result);
        }
    }
}
