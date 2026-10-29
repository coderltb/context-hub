namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Repositories;

/// <summary>
/// Verifies typed search, ordering and paging through real PostgreSQL queries rather than in-memory filtering.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class RepositorySearchTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Recreates only the isolated fixture model and seeds deliberately conflicting scope and field values.
    /// </summary>
    /// <returns>The fixture schema and literal data initialization.</returns>
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        context.AddRange(
            new SearchEntity { Id = 1, Name = "Alpha", CreatedAt = UtcDay(1) },
            new SearchEntity { Id = 2, Name = "Alpha", Description = "Details", CreatedAt = UtcDay(3) },
            new SearchEntity { Id = 3, Name = "Beta", Description = "Alpha notes", CreatedAt = UtcDay(2) },
            new SearchEntity { Id = 4, Name = "Alpha", Scope = 2, CreatedAt = UtcDay(4) },
            new SearchEntity { Id = 5, Name = "Alpha", Active = true, Description = "Enabled", CreatedAt = UtcDay(4) },
            new SearchEntity { Id = 6, Name = "Alpha", Stock = 1, Description = "In stock", CreatedAt = UtcDay(4) },
            new SearchEntity { Id = 7, Name = "Alpha", Description = "Extra", CreatedAt = UtcDay(3) });
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Leaves container ownership with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Uses identical AND/OR grouping and independent sort directions, tracking only entity results.
    /// </summary>
    /// <returns>The real SQL search, projection, ordering and read ownership assertions.</returns>
    [Fact]
    public async Task AllReadShapesApplyTheSameSearchWithoutBroadeningScope()
    {
        var commands = new SearchCommandObserver();
        await using var context = CreateContext(commands);
        IGenericRepository<SearchEntity> repository = new GenericRepository<SearchEntity>(context,
            NullLogger<GenericRepository<SearchEntity>>.Instance);
        Expression<Func<SearchEntity, bool>> scope = row => row.Scope == 1;
        var search = new SearchOptions<SearchEntity>
        {
            SearchTerm = "Alpha",
            TextFields = [row => row.Name, row => row.Description],
            FieldFilters = [null, row => !row.Active, row => row.Stock == 0]
        };
        var order = QueryOrder<SearchEntity>.By(row => row.Name)
            .ThenBy(row => row.CreatedAt, descending: true).ThenBy(row => row.Id);

        var page = await repository.GetPageAsync(row => row.Id, new PageRequest(1, 2), order, scope, search);
        var entityPage = await repository.GetPageAsync(new PageRequest(2, 2), order, scope, search);
        var entities = await repository.GetListAsync(scope, order, search);
        var ids = await repository.GetListAsync(row => row.Id, scope, order, search);
        var first = await repository.FirstOrDefaultAsync(scope, order, search);
        var firstId = await repository.FirstOrDefaultAsync(row => row.Id, scope, order, search);

        Assert.Equal(4, page.TotalCount);
        Assert.Equal([2, 7], page.Items);
        Assert.Equal([1, 3], entityPage.Items.Select(row => row.Id));
        Assert.Equal([2, 7, 1, 3], entities.Select(row => row.Id));
        Assert.Equal([2, 7, 1, 3], ids);
        Assert.Equal(2, first!.Id);
        Assert.Equal(2, firstId);
        Assert.Equal(4, await repository.CountAsync(scope, search));
        Assert.True(await repository.AnyAsync(scope, search));
        Assert.False(await repository.AnyAsync(row => row.Scope == 3, search));
        Assert.Equal([1, 2, 3, 7], context.ChangeTracker.Entries<SearchEntity>().Select(entry => entry.Entity.Id).Order());
        Assert.Null(context.Database.CurrentTransaction);

        // PostgreSQL performs the projection and mixed sorting; the shared facade never filters materialized rows.
        Assert.StartsWith("SELECT s.\"Id\"", commands.Sql[1], StringComparison.Ordinal);
        Assert.Contains("ORDER BY s.\"Name\", s.\"CreatedAt\" DESC, s.\"Id\"", commands.Sql[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// Retains field predicates when text is inactive and preserves legacy no-filter and boolean projection calls.
    /// </summary>
    /// <returns>The optional input, explicit null comparison and overload compatibility assertions.</returns>
    [Fact]
    public async Task OptionalSearchDoesNotDiscardExplicitFieldConditions()
    {
        Expression<Func<SearchEntity, bool>>? noPredicate = null;

        await using var context = CreateContext();
        var repository = new GenericRepository<SearchEntity>(context, NullLogger<GenericRepository<SearchEntity>>.Instance);
        var search = new SearchOptions<SearchEntity>
        {
            SearchTerm = " \t ",
            FieldFilters = [null, row => row.Description == null, row => row.Scope == 1]
        };

        Assert.Equal([1], await repository.GetListAsync(row => row.Id, search: search));
        Assert.Equal(7, await repository.CountAsync(search: null));
        Assert.Equal(7, (await repository.GetListAsync(noPredicate)).Count);
        Assert.Equal(7, (await repository.GetListAsync()).Count);
        Assert.NotNull(await repository.FirstOrDefaultAsync(noPredicate));
        Assert.Equal(7, (await repository.GetListAsync<bool>(row => row.Active)).Count);
        Assert.False(await repository.FirstOrDefaultAsync<bool>(row => row.Active,
            order: QueryOrder<SearchEntity>.By(row => row.Id)));
        Assert.Equal(0, await repository.CountAsync(search: new SearchOptions<SearchEntity>
            { FieldFilters = [row => false] }));
    }

    /// <summary>
    /// Treats wildcard-looking text and Unicode literally, with the keyword carried in provider parameters.
    /// </summary>
    /// <param name="term">A literal keyword that must not become SQL or a wildcard expression.</param>
    /// <returns>The PostgreSQL literal-match and parameterization assertions.</returns>
    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("'")]
    [InlineData("\\")]
    [InlineData("Đắk")]
    [InlineData("alpha")]
    public async Task TextSearchIsLiteralAndParameterized(string term)
    {
        await using (var writer = CreateContext())
        {
            writer.AddRange(new SearchEntity { Id = 8, Name = "before" + term + "after", CreatedAt = UtcDay(1) },
                new SearchEntity { Id = 9, Name = "before-other-after", CreatedAt = UtcDay(1) });
            await writer.SaveChangesAsync();
        }

        var commands = new SearchCommandObserver();
        await using var context = CreateContext(commands);
        var repository = new GenericRepository<SearchEntity>(context, NullLogger<GenericRepository<SearchEntity>>.Instance);
        var search = new SearchOptions<SearchEntity> { SearchTerm = term, TextFields = [row => row.Name] };

        var ids = await repository.GetListAsync(row => row.Id, search: search);

        Assert.Equal([8], ids);
        Assert.NotEmpty(Assert.Single(commands.Parameters));
        Assert.Contains(commands.Parameters[0], value => value.Contains(term, StringComparison.Ordinal));
        Assert.Contains("@", Assert.Single(commands.Sql), StringComparison.Ordinal);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Rejects invalid active search before SQL and keeps cancellation as cancellation on all read shapes.
    /// </summary>
    /// <returns>The pre-I/O failure and cancellation assertions.</returns>
    [Fact]
    public async Task InvalidSearchFailsBeforeIoAndCancellationIsPreserved()
    {
        var commands = new SearchCommandObserver();
        await using var context = CreateContext(commands);
        var repository = new GenericRepository<SearchEntity>(context, NullLogger<GenericRepository<SearchEntity>>.Instance);
        var invalid = new SearchOptions<SearchEntity> { SearchTerm = "Alpha" };
        var order = QueryOrder<SearchEntity>.By(row => row.Id);

        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetPageAsync(new PageRequest(), order, search: invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetListAsync(search: invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.FirstOrDefaultAsync(search: invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.CountAsync(search: invalid));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.AnyAsync(search: invalid));
        Assert.Empty(commands.Sql);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetListAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FirstOrDefaultAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.CountAsync(cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AnyAsync(cancellationToken: cancellation.Token));
    }

    /// <summary>
    /// Reuses the prepared predicate even when caller-owned collections change between count and items reads.
    /// </summary>
    /// <returns>The predicate preparation and source collection ownership assertions.</returns>
    [Fact]
    public async Task PagingPreparesSearchOnceBeforeBothReads()
    {
        var fields = new List<Expression<Func<SearchEntity, string?>>> { row => row.Name };
        var filters = new List<Expression<Func<SearchEntity, bool>>?> { row => row.Id == 1 };
        var commands = new SearchCommandObserver(() => { fields.Clear(); filters.Clear(); });
        await using var context = CreateContext(commands);
        var repository = new GenericRepository<SearchEntity>(context, NullLogger<GenericRepository<SearchEntity>>.Instance);
        var search = new SearchOptions<SearchEntity> { SearchTerm = "Alpha", TextFields = fields, FieldFilters = filters };

        var page = await repository.GetPageAsync(new PageRequest(), QueryOrder<SearchEntity>.By(row => row.Id), search: search);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, Assert.Single(page.Items).Id);
        Assert.Equal(2, commands.Sql.Count);
        Assert.Empty(fields);
        Assert.Empty(filters);
    }

    /// <summary>
    /// Separates tracked entity paging from no-tracking selector dispatch while sharing the scoped query seam.
    /// </summary>
    /// <returns>The runtime virtual-dispatch assertions through the shared interface.</returns>
    [Fact]
    public async Task EntityAndProjectionPagingShareQueryScopeWithoutSharingTrackingPolicy()
    {
        await using var context = CreateContext();
        var specific = new SpecificSearchRepository(context);
        IGenericRepository<SearchEntity> repository = specific;
        var search = new SearchOptions<SearchEntity> { FieldFilters = [row => row.Id == 2] };

        var page = await repository.GetPageAsync(new PageRequest(), QueryOrder<SearchEntity>.By(row => row.Id), search: search);

        Assert.False(specific.ProjectionCalled);
        Assert.Same(search, specific.Search);
        Assert.Equal(2, Assert.Single(page.Items).Id);
        Assert.Single(context.ChangeTracker.Entries<SearchEntity>());

        var projected = await repository.GetPageAsync(row => row.Id, new PageRequest(), search: search);

        Assert.True(specific.ProjectionCalled);
        Assert.Equal([2], projected.Items);
        Assert.Single(context.ChangeTracker.Entries<SearchEntity>());
        Assert.All(typeof(GenericRepository<SearchEntity>).GetMethods(BindingFlags.Public | BindingFlags.Instance |
            BindingFlags.DeclaredOnly), method => Assert.True(method.IsVirtual && !method.IsFinal));
    }

    /// <summary>
    /// Creates a test-only context with optional native SQL observation.
    /// </summary>
    /// <param name="observer">An optional test-owned command interceptor.</param>
    /// <returns>A context connected exclusively to the disposable database.</returns>
    private SearchContext CreateContext(DbCommandInterceptor? observer = null)
    {
        var options = new DbContextOptionsBuilder<SearchContext>().UseNpgsql(postgreSql.ConnectionString);
        if (observer is not null)
        {
            options.AddInterceptors(observer);
        }

        return new SearchContext(options.Options);
    }

    /// <summary>
    /// Produces explicit UTC sort keys accepted by PostgreSQL timestamp-with-time-zone mapping.
    /// </summary>
    /// <param name="day">The literal day within the fixture month.</param>
    /// <returns>The UTC fixture timestamp.</returns>
    private static DateTime UtcDay(int day) => new(2026, 1, day, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Captures real SQL and can coordinate a collection mutation before the second paging read.
    /// </summary>
    /// <param name="beforeItems">An optional deterministic action before the page command executes.</param>
    private sealed class SearchCommandObserver(Action? beforeItems = null) : DbCommandInterceptor
    {
        /// <summary>
        /// Stores provider SQL for projection and ordering assertions only.
        /// </summary>
        public List<string> Sql { get; } = [];

        /// <summary>
        /// Stores test-only provider parameters to verify keyword parameterization.
        /// </summary>
        public List<string[]> Parameters { get; } = [];

        /// <summary>
        /// Records commands without suppressing or replacing PostgreSQL execution.
        /// </summary>
        /// <param name="command">The real provider command.</param>
        /// <param name="eventData">The native EF operation metadata.</param>
        /// <param name="result">The unchanged interception result.</param>
        /// <param name="cancellationToken">The caller's query cancellation token.</param>
        /// <returns>The original result after optional deterministic fixture coordination.</returns>
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Sql.Add(command.CommandText);
            Parameters.Add([.. command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value?.ToString() ?? "")]);
            if (command.CommandText.Contains("LIMIT", StringComparison.Ordinal))
            {
                beforeItems?.Invoke();
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Demonstrates independent query scope and projection paging overrides.
    /// </summary>
    /// <param name="context">The same context used by inherited reads.</param>
    private sealed class SpecificSearchRepository(DbContext context)
        : GenericRepository<SearchEntity>(context, NullLogger<GenericRepository<SearchEntity>>.Instance)
    {
        /// <summary>
        /// Records that interface dispatch reached the projection override.
        /// </summary>
        public bool ProjectionCalled { get; private set; }

        /// <summary>
        /// Retains the passed search object to verify forwarding ownership.
        /// </summary>
        public SearchOptions<SearchEntity>? Search { get; private set; }

        /// <summary>
        /// Observes the common query seam used by entity and projection paging alike.
        /// </summary>
        /// <param name="predicate">Optional caller scope.</param>
        /// <param name="search">Optional search conditions.</param>
        /// <param name="order">Optional typed ordering.</param>
        /// <returns>The shared unexecuted query.</returns>
        protected override IQueryable<SearchEntity> CreateQuery(Expression<Func<SearchEntity, bool>>? predicate = null,
            SearchOptions<SearchEntity>? search = null, QueryOrder<SearchEntity>? order = null)
        {
            Search = search;

            return base.CreateQuery(predicate, search, order);
        }

        /// <summary>
        /// Observes forwarding while delegating actual query behavior to the shared implementation.
        /// </summary>
        /// <typeparam name="TResult">The provider-projected result type.</typeparam>
        /// <param name="selector">The selected columns.</param>
        /// <param name="page">The bounded page request.</param>
        /// <param name="order">The caller's optional typed ordering.</param>
        /// <param name="predicate">The optional mandatory scope predicate.</param>
        /// <param name="search">The optional typed search input.</param>
        /// <param name="cancellationToken">Cancellation forwarded unchanged.</param>
        /// <returns>The shared provider-projected page.</returns>
        public override Task<PageResponse<TResult>> GetPageAsync<TResult>(Expression<Func<SearchEntity, TResult>> selector,
            PageRequest page, QueryOrder<SearchEntity>? order = null, Expression<Func<SearchEntity, bool>>? predicate = null,
            SearchOptions<SearchEntity>? search = null, CancellationToken cancellationToken = default)
        {
            ProjectionCalled = true;
            Search = search;

            return base.GetPageAsync(selector, page, order, predicate, search, cancellationToken);
        }
    }

    /// <summary>
    /// Maps only test-owned searchable fields without modifying product entities or migrations.
    /// </summary>
    /// <param name="options">The isolated PostgreSQL options.</param>
    private sealed class SearchContext(DbContextOptions<SearchContext> options) : DbContext(options)
    {
        /// <summary>
        /// Defines the fixture table and explicitly assigned keys.
        /// </summary>
        /// <param name="modelBuilder">The fixture-only model configuration.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SearchEntity>().Property(row => row.Id).ValueGeneratedNever();
        }
    }

    /// <summary>
    /// Supplies real provider columns for independent search, scope and mixed sorting checks.
    /// </summary>
    private sealed class SearchEntity
    {
        /// <summary>
        /// Holds the explicit unique tie-breaker.
        /// </summary>
        public int Id { get; init; }

        /// <summary>
        /// Holds the first searchable field and primary sort key.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Holds the nullable alternative searchable field.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Holds a business scope that search must not broaden.
        /// </summary>
        public int Scope { get; init; } = 1;

        /// <summary>
        /// Makes false a meaningful field filter.
        /// </summary>
        public bool Active { get; init; }

        /// <summary>
        /// Makes zero a meaningful numeric field filter.
        /// </summary>
        public int Stock { get; init; }

        /// <summary>
        /// Holds the independently descending secondary sort key.
        /// </summary>
        public DateTime CreatedAt { get; init; }
    }
}
