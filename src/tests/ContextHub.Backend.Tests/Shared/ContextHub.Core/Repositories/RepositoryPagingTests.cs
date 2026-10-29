namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Repositories;

/// <summary>
/// Exercises typed paging and expression translation against the disposable PostgreSQL database.
/// </summary>
/// <param name="postgreSql">The collection-owned database fixture.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class RepositoryPagingTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets only the fixture database before each test.
    /// </summary>
    /// <returns>The isolated schema initialization.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves container disposal with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Applies composed filters and provider projection, tracking only entity pages without saving.
    /// </summary>
    /// <returns>The database ordering, projection and ownership assertions.</returns>
    [Fact]
    public async Task PagingComposesFiltersAndProjectsOnlySelectedColumns()
    {
        Expression<Func<TestEntity, bool>>? noPredicate = null;

        await SeedAsync();
        var commands = new PageCommandInterceptor();
        await using var context = CreateContext(commands);
        IGenericRepository<TestEntity> repository = new GenericRepository<TestEntity>(context,
            NullLogger<GenericRepository<TestEntity>>.Instance);
        var prefix = "A";
        Expression<Func<TestEntity, bool>> startsWithA = row => row.Name.StartsWith(prefix);
        Expression<Func<TestEntity, bool>> excluded = row => row.Name == "Alpha";
        var predicate = startsWithA.OrElse(excluded.Not()).AndAlso(row => row.Name != "Beta");
        var ascending = QueryOrder<TestEntity>.By(row => row.Name).ThenBy(row => row.Id);

        var page = await repository.GetPageAsync(row => row.Name, new PageRequest(2, 1), ascending, predicate);
        var reversedTies = QueryOrder<TestEntity>.By(row => row.Name).ThenBy(row => row.Id, descending: true);
        var entities = await repository.GetPageAsync(new PageRequest(1, 2), reversedTies);
        var unchangedOrder = await repository.GetPageAsync(new PageRequest(1, 2), ascending, noPredicate);
        var descending = await repository.GetPageAsync(row => row.Name, new PageRequest(1, 1),
            QueryOrder<TestEntity>.By(row => row.Name, descending: true));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(["Alpha"], page.Items);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000002"), entities.Items[0].Id);
        Assert.Equal(Guid.Parse("00000000-0000-0000-0000-000000000001"), unchangedOrder.Items[0].Id);
        Assert.Equal(["Beta"], descending.Items);
        Assert.Equal(2, context.ChangeTracker.Entries<TestEntity>().Count());
        Assert.Equal(0, context.SaveCallCount);
        Assert.Null(context.Database.CurrentTransaction);

        // The projection is executed in PostgreSQL, not after loading entities into memory.
        Assert.StartsWith("SELECT c.\"Name\"", commands.Commands[1], StringComparison.Ordinal);
        Assert.Contains("ORDER BY", commands.Commands[1], StringComparison.Ordinal);
        Assert.Contains("OFFSET", commands.Commands[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Retains counts on empty pages and rejects required inputs or overflowing offsets before SQL.
    /// </summary>
    /// <returns>The page boundary and cancellation assertions.</returns>
    [Fact]
    public async Task PagingValidatesBeforeIoAndKeepsTotalsBeyondLastPage()
    {
        await SeedAsync();
        var commands = new PageCommandInterceptor();
        await using var context = CreateContext(commands);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        var order = QueryOrder<TestEntity>.By(row => row.Id);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            repository.GetPageAsync(new PageRequest(int.MaxValue, 100), order));
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.GetPageAsync(null!, order));
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.GetPageAsync<string>(null!, new PageRequest(), order));
        Assert.Throws<ArgumentNullException>(() => QueryOrder<TestEntity>.By<string>(null!));
        Assert.Throws<ArgumentNullException>(() => order.ThenBy<string>(null!));
        Assert.Empty(commands.Commands);

        var empty = await repository.GetPageAsync(new PageRequest(4, 1), order);
        var noMatch = await repository.GetPageAsync(row => row.Id, new PageRequest(), order, row => false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.Empty(empty.Items);
        Assert.Equal(3, empty.TotalCount);
        Assert.Empty(noMatch.Items);
        Assert.Equal(0, noMatch.TotalCount);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.GetPageAsync(new PageRequest(), order, cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// Omits sorting and additional search when callers supply neither optional input.
    /// </summary>
    /// <returns>The default and explicit-null read behavior verified against provider SQL.</returns>
    [Fact]
    public async Task PagingWithoutOrderOrSearchDoesNotInventEither()
    {
        await SeedAsync();
        var commands = new PageCommandInterceptor();
        await using var context = CreateContext(commands);
        IGenericRepository<TestEntity> repository = new GenericRepository<TestEntity>(context,
            NullLogger<GenericRepository<TestEntity>>.Instance);

        var page = await repository.GetPageAsync(new PageRequest());
        var projection = await repository.GetPageAsync(row => row.Name, new PageRequest(), order: null, search: null);
        var list = await repository.GetListAsync();
        var first = await repository.FirstOrDefaultAsync();

        // Unordered reads deliberately assert membership rather than a first row or stable page order.
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.Equal(3, projection.TotalCount);
        Assert.Equal(["Alpha", "Alpha", "Beta"], projection.Items.Order());
        Assert.Equal(3, list.Count);
        Assert.Contains(first!.Id, list.Select(row => row.Id));
        Assert.DoesNotContain(commands.Commands, sql => sql.Contains("ORDER BY", StringComparison.Ordinal));
        Assert.DoesNotContain(commands.Commands, sql => sql.Contains("WHERE", StringComparison.Ordinal));
        Assert.Equal(3, context.ChangeTracker.Entries<TestEntity>().Count());
        Assert.Equal(0, context.SaveCallCount);
    }

    /// <summary>
    /// Returns the count as observed even when another connection inserts before the items query.
    /// </summary>
    /// <returns>The real two-read race assertions.</returns>
    [Fact]
    public async Task InsertBetweenCountAndItemsDoesNotInvalidateThePage()
    {
        var commands = new PageCommandInterceptor(async () =>
        {
            await using var writer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
            writer.Add(new TestEntity { Id = Guid.NewGuid(), Name = "Inserted after count" });
            await writer.SaveChangesAsync();
        });
        await using var context = CreateContext(commands);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        var page = await repository.GetPageAsync(new PageRequest(), QueryOrder<TestEntity>.By(row => row.Id));

        Assert.Equal(0, page.TotalCount);
        Assert.Equal("Inserted after count", Assert.Single(page.Items).Name);
        Assert.Equal(2, commands.Commands.Count);
    }

    /// <summary>
    /// Cancels after count has finished and preserves cancellation rather than returning a partial page.
    /// </summary>
    /// <returns>The cancellation propagation and no-save assertions.</returns>
    [Fact]
    public async Task CancellationBetweenReadsDoesNotReturnPartialResults()
    {
        using var cancellation = new CancellationTokenSource();
        var commands = new PageCommandInterceptor(async () =>
        {
            await cancellation.CancelAsync();
        });
        await using var context = CreateContext(commands);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetPageAsync(row => row.Name,
            new PageRequest(), QueryOrder<TestEntity>.By(row => row.Id), cancellationToken: cancellation.Token));

        Assert.Equal(2, commands.Commands.Count);
        Assert.All(commands.Tokens, token => Assert.Equal(cancellation.Token, token));
        Assert.Equal(0, context.SaveCallCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Adds a secondary key immutably so reusing the original order does not retain a previous tie-breaker.
    /// </summary>
    /// <returns>The ordering assertions from both independent query plans.</returns>
    [Fact]
    public async Task ThenByDoesNotMutateAnExistingOrdering()
    {
        await SeedAsync();
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance);
        var primary = QueryOrder<TestEntity>.By(row => row.Name);
        var descending = primary.ThenBy(row => row.Id, descending: true);
        var ascending = primary.ThenBy(row => row.Id);

        var down = await repository.GetPageAsync(new PageRequest(1, 2), descending);
        var up = await repository.GetPageAsync(new PageRequest(1, 2), ascending);

        Assert.Equal([2, 1], down.Items.Select(row => int.Parse(row.Id.ToString("N")[^1..])));
        Assert.Equal([1, 2], up.Items.Select(row => int.Parse(row.Id.ToString("N")[^1..])));
    }

    /// <summary>
    /// Seeds literal keys and repeated names so paging requires a secondary ordering key.
    /// </summary>
    /// <returns>The fixture-only save operation.</returns>
    private async Task SeedAsync()
    {
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        context.AddRange(
            new TestEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000002"), Name = "Alpha" },
            new TestEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Name = "Alpha" },
            new TestEntity { Id = Guid.Parse("00000000-0000-0000-0000-000000000003"), Name = "Beta" });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Observes SQL at the native EF boundary without mocking query execution.
    /// </summary>
    /// <param name="interceptor">The test-owned SQL observer.</param>
    /// <returns>A context connected only to the disposable fixture.</returns>
    private CoreTestDbContext CreateContext(DbCommandInterceptor interceptor)
    {
        return new CoreTestDbContext(new DbContextOptionsBuilder<CoreTestDbContext>()
            .UseNpgsql(postgreSql.ConnectionString).AddInterceptors(interceptor).Options);
    }

    /// <summary>
    /// Captures actual query commands and optionally coordinates a write between count and page reads.
    /// </summary>
    /// <param name="beforePage">An optional fixture write executed immediately before the page query.</param>
    private sealed class PageCommandInterceptor(Func<Task>? beforePage = null) : DbCommandInterceptor
    {
        /// <summary>
        /// Records executed SQL for projection and two-query assertions.
        /// </summary>
        public List<string> Commands { get; } = [];

        /// <summary>
        /// Records tokens passed to each real query without replacing provider cancellation.
        /// </summary>
        public List<CancellationToken> Tokens { get; } = [];

        /// <summary>
        /// Lets PostgreSQL execute the query after the optional coordinated concurrent write.
        /// </summary>
        /// <param name="command">The real database command.</param>
        /// <param name="eventData">The EF operation metadata.</param>
        /// <param name="result">The unchanged native interception result.</param>
        /// <param name="cancellationToken">The query cancellation token.</param>
        /// <returns>The unchanged result after any fixture coordination.</returns>
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            Tokens.Add(cancellationToken);
            if (beforePage is not null && command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                // The writer uses another connection; no artificial sleep or production hook is needed.
                await beforePage();
            }

            return result;
        }
    }
}
