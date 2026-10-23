namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.UnitOfWork;

/// <summary>
/// Verifies that DI-resolved EF repositories and Dapper share the transaction owned by Unit of Work.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable PostgreSQL database.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class UnitOfWorkDapperTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets only the isolated container schema before each ownership scenario.
    /// </summary>
    /// <returns>The asynchronous test-schema reset.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves the collection-owned container available for subsequent tests.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Commits both writers together or rolls both back without opening an unrelated Dapper connection.
    /// </summary>
    /// <param name="failureKind">Zero for success, one for callback failure, two for SQL failure, or three for commit failure.</param>
    /// <returns>The atomicity, save count, exception identity and connection-ownership assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task EfAndDapperShareTheOwnedTransaction(int failureKind)
    {
        var factory = new UnexpectedConnectionFactory();
        var commit = new CommitFailureObserver();
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<CoreTestDbContext>(options =>
        {
            options.UseNpgsql(postgreSql.ConnectionString);
            if (failureKind == 3)
            {
                options.AddInterceptors(commit);
            }
        });
        services.AddSingleton<IDbConnectionFactory>(factory);
        services.AddPersistence<CoreTestDbContext>().AddDapper();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CoreTestDbContext>();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestEntity>>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var dapper = scope.ServiceProvider.GetRequiredService<IDapperService>();
        var efId = Guid.NewGuid();
        var dapperId = Guid.NewGuid();
        var callbackFailure = new InvalidOperationException("Fixture callback failure");

        var exception = await Record.ExceptionAsync(() => work.ExecuteInTransactionAsync(async token =>
        {
            // The repository stages EF work; the outer Unit of Work alone owns the final save and commit.
            await repository.AddAsync(new TestEntity { Id = efId, Name = "EF" }, token);
            var options = DapperCommandOptionsHelper.CreateTransactionalText(context, token);

            Assert.True(work.HasActiveTransaction);
            Assert.Same(context.Database.GetDbConnection(), options.Transaction!.Connection);
            await dapper.ExecuteAsync("insert into \"CoreEntity\" (\"Id\", \"Name\") values (@id, @name)",
                new { id = dapperId, name = "Dapper" }, options);

            // Dapper has already written inside the borrowed transaction, but an independent observer must not see it.
            await using var observerScope = provider.CreateAsyncScope();
            var observer = observerScope.ServiceProvider.GetRequiredService<CoreTestDbContext>();
            Assert.False(await observer.Entities.AnyAsync(token));

            if (failureKind == 1)
            {
                throw callbackFailure;
            }

            if (failureKind == 2)
            {
                await dapper.ExecuteAsync("select 1 / 0", options: options);
            }
        }));

        if (failureKind == 0)
        {
            Assert.Null(exception);
        }
        else if (failureKind == 1)
        {
            Assert.Same(callbackFailure, exception);
        }
        else if (failureKind == 2)
        {
            Assert.Equal(PostgresErrorCodes.DivisionByZero, Assert.IsType<PostgresException>(exception).SqlState);
        }
        else
        {
            // Commit fails after both EF and Dapper have sent writes, proving rollback covers both database writers.
            Assert.Same(commit.Failure, exception);
        }

        Assert.False(work.HasActiveTransaction);
        Assert.Equal(failureKind is 0 or 3 ? 1 : 0, context.SaveCallCount);
        Assert.Equal(0, factory.Calls);

        await using var finalScope = provider.CreateAsyncScope();
        var finalContext = finalScope.ServiceProvider.GetRequiredService<CoreTestDbContext>();
        var names = await finalContext.Entities.OrderBy(row => row.Name).Select(row => row.Name).ToListAsync();

        Assert.Equal(failureKind == 0 ? new[] { "Dapper", "EF" } : Array.Empty<string>(), names);
    }

    /// <summary>
    /// Rejects only an unexpected owned acquisition; EF and all borrowed Dapper commands still use real PostgreSQL.
    /// </summary>
    private sealed class UnexpectedConnectionFactory : IDbConnectionFactory
    {
        /// <summary>
        /// Counts acquisitions that would break participation in the owning EF transaction.
        /// </summary>
        public int Calls { get; private set; }

        /// <summary>
        /// Fails immediately if Dapper attempts to leave the caller's connection and transaction.
        /// </summary>
        /// <param name="cancellationToken">The command's acquisition cancellation token.</param>
        /// <returns>A failed acquisition task; the factory must not be reached in these scenarios.</returns>
        public Task<DbConnection> GetOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            Calls++;

            return Task.FromException<DbConnection>(new InvalidOperationException("Unexpected owned Dapper connection."));
        }
    }

    /// <summary>
    /// Injects a failure immediately before commit without replacing database commands or the rollback mechanism.
    /// </summary>
    private sealed class CommitFailureObserver : DbTransactionInterceptor
    {
        /// <summary>
        /// Retains the failure instance to detect wrapping or replacement during rollback.
        /// </summary>
        public IOException Failure { get; } = new("Fixture commit failure");

        /// <summary>
        /// Rejects commit after the EF save, leaving the real transaction available for rollback.
        /// </summary>
        /// <param name="transaction">The real PostgreSQL transaction containing both writers.</param>
        /// <param name="eventData">The framework transaction metadata.</param>
        /// <param name="result">The interception state before provider commit.</param>
        /// <param name="cancellationToken">The owning operation's cancellation token.</param>
        /// <returns>No result because the deterministic failure is thrown.</returns>
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            throw Failure;
        }
    }
}
