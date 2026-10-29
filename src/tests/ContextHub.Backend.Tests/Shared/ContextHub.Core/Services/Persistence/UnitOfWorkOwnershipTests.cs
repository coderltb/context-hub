namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Services.Persistence;

/// <summary>
/// Checks nested, borrowed and failing transaction boundaries against real PostgreSQL.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class UnitOfWorkOwnershipTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Recreates only the isolated fixture schema.
    /// </summary>
    /// <returns>The schema reset task.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves the collection-owned container alive for the next isolated test.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Leaves all save and transaction decisions to the caller in both callback overloads.
    /// </summary>
    /// <param name="returnsValue">Whether the callback returns a value.</param>
    /// <param name="fails">Whether the caller's callback fails after staging.</param>
    /// <returns>The borrowed-transaction ownership assertions.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task BorrowedTransactionNeverSavesOrCompletesForCaller(bool returnsValue, bool fails)
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        IUnitOfWork work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);
        Assert.False(work.HasActiveTransaction);
        await using var transaction = await context.Database.BeginTransactionAsync();
        var failure = new InvalidOperationException("Fixture callback failure");
        Func<CancellationToken, Task<int>> action = _ =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Caller staged" });
            return fails ? Task.FromException<int>(failure) : Task.FromResult(7);
        };

        Func<Task> execute = returnsValue
            ? async () => Assert.Equal(7, await work.ExecuteInTransactionAsync(action))
            : () => work.ExecuteInTransactionAsync(async token => { await action(token); });
        if (fails)
        {
            Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(execute));
        }
        else
        {
            await execute();
        }

        Assert.True(work.HasActiveTransaction);
        Assert.Same(transaction, context.Database.CurrentTransaction);
        Assert.Equal(0, context.SaveCallCount);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());

        // The caller can still choose to save and commit even after a callback failure.
        await work.SaveChangesAsync();
        await transaction.CommitAsync();

        Assert.False(work.HasActiveTransaction);
        Assert.Equal(1, context.SaveCallCount);
        Assert.Equal("Caller staged", (await observer.Entities.SingleAsync()).Name);
    }

    /// <summary>
    /// Lets the outer transaction save once while an inner callback reuses exactly the same transaction.
    /// </summary>
    /// <returns>The nested successful commit assertions.</returns>
    [Fact]
    public async Task NestedSuccessSavesOnlyAtTheOuterBoundary()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

        await work.ExecuteInTransactionAsync(async token =>
        {
            var outer = context.Database.CurrentTransaction;
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Outer" });
            CancellationToken receivedToken = default;
            var result = await work.ExecuteInTransactionAsync(innerToken =>
            {
                receivedToken = innerToken;
                Assert.Same(outer, context.Database.CurrentTransaction);
                context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Inner" });
                return Task.FromResult(9);
            }, token);

            Assert.Equal(token, receivedToken);
            Assert.Equal(9, result);
            Assert.True(work.HasActiveTransaction);
            Assert.Equal(0, context.SaveCallCount);
        });

        Assert.Equal(1, context.SaveCallCount);
        Assert.False(work.HasActiveTransaction);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(2, await observer.Entities.CountAsync());
    }

    /// <summary>
    /// Rolls back writes already sent inside the inner callback without wrapping its original exception.
    /// </summary>
    /// <returns>The nested failure and database rollback assertions.</returns>
    [Fact]
    public async Task NestedFailureRollsBackTheOuterOwnedTransaction()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);
        var failure = new InvalidOperationException("Fixture nested failure");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => work.ExecuteInTransactionAsync(async token =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Outer write" });
            await context.SaveChangesAsync(token);
            await work.ExecuteInTransactionAsync(async innerToken =>
            {
                context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Inner write" });
                await context.SaveChangesAsync(innerToken);
                throw failure;
            }, token);
        }));

        Assert.Same(failure, actual);
        Assert.False(work.HasActiveTransaction);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());
    }

    /// <summary>
    /// Uses an uncancelled rollback even when cancellation arrives after a write in the owned transaction.
    /// </summary>
    /// <returns>The cancellation identity and rollback assertions.</returns>
    [Fact]
    public async Task CancellationAfterBeginStillRollsBackPersistedWork()
    {
        var rollback = new RollbackObserver();
        await using var context = CreateContext(rollback);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);
        using var cancellation = new CancellationTokenSource();

        var actual = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => work.ExecuteInTransactionAsync(async token =>
        {
            Assert.True(work.HasActiveTransaction);
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Cancelled write" });
            await context.SaveChangesAsync(token);
            await cancellation.CancelAsync();
            token.ThrowIfCancellationRequested();
        }, cancellation.Token));

        Assert.Equal(cancellation.Token, actual.CancellationToken);
        Assert.True(rollback.Attempted);
        Assert.False(rollback.Token.CanBeCanceled);
        Assert.False(work.HasActiveTransaction);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());
    }

    /// <summary>
    /// Rolls back an earlier successful write when the final shared save fails in PostgreSQL.
    /// </summary>
    /// <returns>The provider error classification and atomicity assertions.</returns>
    [Fact]
    public async Task SaveFailureRollsBackEarlierWrites()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => work.ExecuteInTransactionAsync(async token =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Already sent" });
            await context.SaveChangesAsync(token);
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = new string('x', 201) });
        }));

        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation,
            Assert.IsType<PostgresException>(failure.InnerException).SqlState);
        Assert.False(work.HasActiveTransaction);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());
    }

    /// <summary>
    /// Records the original operation before rollback so a secondary rollback failure cannot hide its diagnostic.
    /// </summary>
    /// <returns>The log ordering, privacy and exception identity assertions.</returns>
    [Fact]
    public async Task OriginalFailureIsLoggedBeforeRollback()
    {
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var loggedBeforeRollback = false;
        var rollback = new RollbackObserver(onRollback: () => loggedBeforeRollback = logger.Entries.Any(entry =>
            entry.Message.Contains(nameof(IUnitOfWork.ExecuteInTransactionAsync), StringComparison.Ordinal) &&
            entry.Message.Contains(nameof(InvalidOperationException), StringComparison.Ordinal)));
        await using var context = CreateContext(rollback);
        var work = new UnitOfWork<CoreTestDbContext>(context, logger);
        var failure = new InvalidOperationException("private-original-message");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            work.ExecuteInTransactionAsync(_ => Task.FromException<int>(failure)));

        Assert.Same(failure, actual);
        Assert.True(loggedBeforeRollback);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("private-original-message", StringComparison.Ordinal));
    }

    /// <summary>
    /// Preserves the callback exception when explicit rollback itself throws, recording only safe error metadata.
    /// </summary>
    /// <returns>The original exception identity, error log and eventual disposal rollback assertions.</returns>
    [Fact]
    public async Task RollbackFailureDoesNotReplaceOriginalException()
    {
        var rollback = new RollbackObserver(fail: true);
        await using var context = CreateContext(rollback);
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var work = new UnitOfWork<CoreTestDbContext>(context, logger);
        var failure = new InvalidOperationException("Private callback content");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => work.ExecuteInTransactionAsync(async token =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Uncommitted" });
            await context.SaveChangesAsync(token);
            throw failure;
        }));

        Assert.Same(failure, actual);
        Assert.True(rollback.Attempted);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("Private callback content", StringComparison.Ordinal));
        Assert.False(work.HasActiveTransaction);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());
    }

    /// <summary>
    /// Rolls back staged writes when commit fails before the provider has committed them.
    /// </summary>
    /// <returns>The original commit failure and PostgreSQL atomicity assertions.</returns>
    [Fact]
    public async Task CommitFailurePreservesOriginalExceptionAndRollsBack()
    {
        var commit = new CommitFailureObserver();
        await using var context = CreateContext(commit);
        var work = new UnitOfWork<CoreTestDbContext>(context, NullLogger<UnitOfWork<CoreTestDbContext>>.Instance);

        var actual = await Assert.ThrowsAsync<IOException>(() => work.ExecuteInTransactionAsync(_ =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Commit rejected" });
            return Task.CompletedTask;
        }));

        Assert.Same(commit.Failure, actual);
        Assert.False(work.HasActiveTransaction);
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Empty(await observer.Entities.ToListAsync());
    }

    /// <summary>
    /// Preserves callback and cancellation failures across disposal, but exposes disposal errors after success.
    /// </summary>
    /// <param name="failureKind">Zero for success, one for callback failure, or two for cancellation.</param>
    /// <returns>The failure identity, safe logging and durable database assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task CleanupFailureDoesNotHideAnEarlierFailure(int failureKind)
    {
        var options = new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .ReplaceService<IRelationalTransactionFactory, CleanupFailureTransactionFactory>().Options;
        await using var context = new CoreTestDbContext(options);
        var logger = new TestLogger<UnitOfWork<CoreTestDbContext>>();
        var work = new UnitOfWork<CoreTestDbContext>(context, logger);
        using var cancellation = new CancellationTokenSource();
        Exception? failure = failureKind switch
        {
            1 => new InvalidOperationException("Private callback content"),
            2 => new OperationCanceledException(cancellation.Token),
            _ => null
        };

        var actual = await Record.ExceptionAsync(() => work.ExecuteInTransactionAsync(async token =>
        {
            context.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Cleanup boundary" });
            await context.SaveChangesAsync(token);
            if (failure is not null)
            {
                if (failureKind == 2)
                {
                    await cancellation.CancelAsync();
                }

                throw failure;
            }
        }, cancellation.Token));

        // Disposal follows the real provider cleanup; no active transaction can leak into the next operation.
        Assert.False(work.HasActiveTransaction);
        if (failure is null)
        {
            Assert.IsType<IOException>(actual);
        }
        else
        {
            Assert.Same(failure, actual);
            Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
        }

        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("Private", StringComparison.Ordinal));
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal(failureKind == 0 ? 1 : 0, await observer.Entities.CountAsync());
    }

    /// <summary>
    /// Injects a commit failure at the native interceptor boundary, before any durable commit occurs.
    /// </summary>
    private sealed class CommitFailureObserver : DbTransactionInterceptor
    {
        /// <summary>
        /// Retains the original instance so the test can detect accidental wrapping or replacement.
        /// </summary>
        public IOException Failure { get; } = new("Fixture commit failure");

        /// <summary>
        /// Rejects commit while leaving the transaction available for the owned rollback path.
        /// </summary>
        /// <param name="transaction">The real PostgreSQL transaction.</param>
        /// <param name="eventData">The native transaction metadata.</param>
        /// <param name="result">The interception state before commit.</param>
        /// <param name="cancellationToken">The caller's transaction cancellation.</param>
        /// <returns>No result because the injected failure is thrown.</returns>
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            throw Failure;
        }
    }

    /// <summary>
    /// Replaces only the EF transaction wrapper; commands and transaction operations still reach PostgreSQL.
    /// </summary>
    /// <param name="dependencies">The native factory dependencies supplied by EF DI.</param>
    private sealed class CleanupFailureTransactionFactory(RelationalTransactionFactoryDependencies dependencies)
        : RelationalTransactionFactory(dependencies)
    {
        /// <summary>
        /// Wraps the actual provider transaction with a deterministic post-disposal fault.
        /// </summary>
        /// <param name="connection">The provider-owned relational connection.</param>
        /// <param name="transaction">The real database transaction.</param>
        /// <param name="transactionId">The native transaction identity.</param>
        /// <param name="logger">The framework transaction logger.</param>
        /// <param name="transactionOwned">Whether the wrapper owns provider disposal.</param>
        /// <returns>The native transaction wrapper with fault injection limited to cleanup.</returns>
        public override RelationalTransaction Create(IRelationalConnection connection, DbTransaction transaction,
            Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned)
        {
            return new CleanupFailureTransaction(connection, transaction, transactionId, logger, transactionOwned,
                Dependencies.SqlGenerationHelper);
        }
    }

    /// <summary>
    /// Simulates cleanup failure after real provider disposal, without faking commit or rollback behavior.
    /// </summary>
    /// <param name="connection">The relational connection.</param>
    /// <param name="transaction">The real PostgreSQL transaction.</param>
    /// <param name="transactionId">The transaction identity.</param>
    /// <param name="logger">The native EF logger.</param>
    /// <param name="transactionOwned">Whether disposal owns the provider transaction.</param>
    /// <param name="sqlGenerationHelper">The provider's SQL generation helper.</param>
    private sealed class CleanupFailureTransaction(IRelationalConnection connection, DbTransaction transaction,
        Guid transactionId, IDiagnosticsLogger<DbLoggerCategory.Database.Transaction> logger, bool transactionOwned,
        ISqlGenerationHelper sqlGenerationHelper)
        : RelationalTransaction(connection, transaction, transactionId, logger, transactionOwned, sqlGenerationHelper)
    {
        /// <summary>
        /// Completes real cleanup before reporting the deterministic secondary failure.
        /// </summary>
        /// <returns>A failed cleanup operation after resources have been released.</returns>
        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();

            throw new IOException("Private fixture cleanup content");
        }
    }

    /// <summary>
    /// Attaches only test-owned transaction observation while retaining the real PostgreSQL provider.
    /// </summary>
    /// <param name="interceptor">The native transaction observer.</param>
    /// <returns>The test-owned database context.</returns>
    private CoreTestDbContext CreateContext(DbTransactionInterceptor interceptor)
    {
        return new CoreTestDbContext(new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(postgreSql.ConnectionString).AddInterceptors(interceptor).Options);
    }

    /// <summary>
    /// Observes the real rollback boundary and optionally injects a secondary rollback failure.
    /// </summary>
    /// <param name="fail">Whether to throw before PostgreSQL receives explicit rollback.</param>
    /// <param name="onRollback">Observes diagnostics before native rollback begins.</param>
    private sealed class RollbackObserver(bool fail = false, Action? onRollback = null) : DbTransactionInterceptor
    {
        /// <summary>
        /// Records that rollback was attempted rather than skipped because of cancellation.
        /// </summary>
        public bool Attempted { get; private set; }

        /// <summary>
        /// Records the token passed to rollback for cancellation ownership assertions.
        /// </summary>
        public CancellationToken Token { get; private set; }

        /// <summary>
        /// Observes rollback without replacing the transaction's normal disposal behavior.
        /// </summary>
        /// <param name="transaction">The real PostgreSQL transaction.</param>
        /// <param name="eventData">The native EF transaction metadata.</param>
        /// <param name="result">The unchanged interception result.</param>
        /// <param name="cancellationToken">The token selected by Unit of Work for rollback.</param>
        /// <returns>The native result unless the test requests a secondary failure.</returns>
        public override ValueTask<InterceptionResult> TransactionRollingBackAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            onRollback?.Invoke();
            Attempted = true;
            Token = cancellationToken;
            if (fail)
            {
                throw new IOException("Fixture rollback failure");
            }

            return ValueTask.FromResult(result);
        }
    }
}
