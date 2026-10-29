namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Repositories;

/// <summary>
/// Exercises native entity tracking and no-tracking unique-result projections through PostgreSQL queries.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable PostgreSQL database.</param>
[Collection(PostgreSqlCollection.NAME)]
public sealed class RepositorySingleAndTrackingTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Recreates only the disposable persistence fixture schema.
    /// </summary>
    /// <returns>The fixture initialization task.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves database container disposal with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Lets callers edit a tracked result and save through the same scoped Unit of Work without Update.
    /// </summary>
    /// <param name="operation">Selects first, list, single or page entity materialization.</param>
    /// <returns>The tracking, staging and independent persisted-value assertions.</returns>
    [Theory]
    [InlineData("first")]
    [InlineData("list")]
    [InlineData("single")]
    [InlineData("page")]
    public async Task TrackedEntityReadsLeaveSavingWithScopedUnitOfWork(string operation)
    {
        await SeedAsync("Original");
        var services = new ServiceCollection().AddLogging();
        services.AddDbContext<CoreTestDbContext>(options => options.UseNpgsql(postgreSql.ConnectionString));
        services.AddPersistence<CoreTestDbContext>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateScopes = true, ValidateOnBuild = true });
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IGenericRepository<TestEntity>>();
        var context = scope.ServiceProvider.GetRequiredService<CoreTestDbContext>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var entity = operation switch
        {
            "first" => await repository.FirstOrDefaultAsync(),
            "list" => Assert.Single(await repository.GetListAsync()),
            "single" => await repository.SingleOrDefaultAsync(),
            _ => Assert.Single((await repository.GetPageAsync(new PageRequest())).Items)
        };
        Assert.NotNull(entity);
        Assert.Same(entity, Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity);
        entity.Name = "Edited";

        // Tracking stages the edit; only the caller's Unit of Work introduces a save boundary.
        await using var observer = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        Assert.Equal("Original", await observer.Entities.Select(row => row.Name).SingleAsync());
        Assert.Equal(0, context.SaveCallCount);
        Assert.Equal(1, await work.SaveChangesAsync());
        Assert.Equal("Edited", await observer.Entities.Select(row => row.Name).SingleAsync());
    }

    /// <summary>
    /// Preserves derived scope and optional search while retaining native entity tracking.
    /// </summary>
    /// <returns>The scoped results, pipeline dispatch and tracker assertions.</returns>
    [Fact]
    public async Task TrackingDoesNotBypassScopeOrSearch()
    {
        await SeedAsync("Visible", "Other", "Hidden");
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new ScopedRepository(context);
        var search = new SearchOptions<TestEntity>
        {
            SearchTerm = "Visible",
            TextFields = [row => row.Name]
        };

        var first = await repository.FirstOrDefaultAsync(search: search);
        var list = await repository.GetListAsync(search: search);
        var single = await repository.SingleOrDefaultAsync(search: search);
        var hidden = await repository.SingleOrDefaultAsync(row => row.Name == "Hidden");

        Assert.NotNull(first);
        Assert.Same(first, Assert.Single(list));
        Assert.Same(first, single);
        Assert.Null(hidden);
        Assert.Equal(4, repository.QueryCalls);
        Assert.Equal("Visible", Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity.Name);
    }

    /// <summary>
    /// Preserves zero, one and multiple-row cardinality without interpreting a predicate lambda as a boolean selector.
    /// </summary>
    /// <param name="matches">The number of rows satisfying the predicate.</param>
    /// <returns>The native cardinality and entity tracking assertions.</returns>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SingleEntityReadUsesNativeCardinality(int matches)
    {
        await SeedAsync([.. Enumerable.Repeat("Match", matches), "Other"]);
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        IGenericRepository<TestEntity> repository = new GenericRepository<TestEntity>(context,
            NullLogger<GenericRepository<TestEntity>>.Instance);

        // The declared Task type also pins overload resolution for a positional boolean lambda.
        Func<Task<TestEntity?>> getSingle = () => repository.SingleOrDefaultAsync(row => row.Name == "Match");

        if (matches > 1)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(getSingle);
        }
        else
        {
            var result = await getSingle();
            Assert.Equal(matches == 0, result is null);
            Assert.True(result is null || result.Name == "Match");
        }

        Assert.Equal(matches, context.ChangeTracker.Entries<TestEntity>().Count());
        Assert.Equal(0, context.SaveCallCount);
    }

    /// <summary>
    /// Uses an omitted or explicitly null selector for global mapping and retains native typed selector defaults.
    /// </summary>
    /// <returns>The mapped alias, SQL projection, override and boolean selector assertions.</returns>
    [Fact]
    public async Task SingleProjectionUsesGlobalMappingOrExplicitSelector()
    {
        await SeedAsync("Mapped", "Hidden");
        new ServiceCollection().AddMapping();
        var commands = new List<string>();
        var options = new DbContextOptionsBuilder<CoreTestDbContext>().UseNpgsql(postgreSql.ConnectionString)
            .LogTo(commands.Add, [RelationalEventId.CommandExecuted]).Options;
        await using var context = new CoreTestDbContext(options);
        var repository = new ScopedRepository(context);

        var mapped = await repository.SingleOrDefaultAsync<GlobalMappingTarget>();
        var explicitNull = await repository.SingleOrDefaultAsync<GlobalMappingTarget>(selector: null);
        var name = await repository.SingleOrDefaultAsync(row => row.Name);
        var isMapped = await repository.SingleOrDefaultAsync<bool>(row => row.Name == "Mapped");
        var missing = await repository.SingleOrDefaultAsync<GlobalMappingTarget>(predicate: row => false);
        var missingNumber = await repository.SingleOrDefaultAsync(row => row.Name.Length, predicate: row => false);

        Assert.Equal("Mapped", mapped!.DisplayName);
        Assert.Equal("Mapped", explicitNull!.DisplayName);
        Assert.Equal("Mapped", name);
        Assert.True(isMapped);
        Assert.Null(missing);
        Assert.Equal(0, missingNumber);
        Assert.Equal(3, repository.ProjectionCalls);
        Assert.Equal(6, repository.QueryCalls);
        Assert.Contains(commands, command => command.Contains("SELECT c.\"Name\"", StringComparison.Ordinal)
            && !command.Contains("c.\"Id\"", StringComparison.Ordinal));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Rejects multiple matching rows for handwritten and globally mapped single projections.
    /// </summary>
    /// <returns>The projected native cardinality assertions.</returns>
    [Fact]
    public async Task SingleProjectionsRejectMultipleRows()
    {
        await SeedAsync("One", "Two");
        new ServiceCollection().AddMapping();
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new ScopedRepository(context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync(row => row.Name));
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.SingleOrDefaultAsync<GlobalMappingTarget>());
        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Treats missing optional inputs as unfiltered reads and preserves cancellation on every new path.
    /// </summary>
    /// <returns>The null-input and cancellation assertions.</returns>
    [Fact]
    public async Task SingleReadsAllowNullFiltersAndForwardCancellation()
    {
        Expression<Func<TestEntity, bool>>? predicate = null;

        await SeedAsync("Only");
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new ScopedRepository(context);

        Assert.NotNull(await repository.SingleOrDefaultAsync());
        Assert.NotNull(await repository.SingleOrDefaultAsync(predicate));
        Assert.Single(context.ChangeTracker.Entries<TestEntity>());

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SingleOrDefaultAsync(
            cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SingleOrDefaultAsync(
            row => row.Name, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetListAsync(
            cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.FirstOrDefaultAsync(
            cancellationToken: cancellation.Token));
        Assert.Equal("Only", Assert.Single(context.ChangeTracker.Entries<TestEntity>()).Entity.Name);
    }

    /// <summary>
    /// Keeps selector projections untracked even when their result shape contains complete EF entities.
    /// </summary>
    /// <returns>The identity selector, nested entity and empty tracker assertions.</returns>
    [Fact]
    public async Task SelectorResultsNeverTrackContainedEntities()
    {
        await SeedAsync("Only");
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        var repository = new ScopedRepository(context);

        Assert.NotNull(await repository.FirstOrDefaultAsync<TestEntity>(selector: row => row));
        Assert.Single(await repository.GetListAsync<TestEntity>(selector: row => row));
        Assert.NotNull(await repository.SingleOrDefaultAsync<TestEntity>(selector: row => row));
        Assert.Single((await repository.GetPageAsync(row => row, new PageRequest())).Items);
        Assert.Equal("Only", (await repository.SingleOrDefaultAsync(row => new { Entity = row }))!.Entity.Name);

        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Respects a host's native no-tracking context policy instead of secretly forcing entity tracking on.
    /// </summary>
    /// <param name="operation">The entity operation using the service-owned context policy.</param>
    /// <returns>The materialized entity and unchanged tracker assertions.</returns>
    [Theory]
    [InlineData("first")]
    [InlineData("list")]
    [InlineData("single")]
    [InlineData("page")]
    public async Task EntityReadsRespectTheDbContextTrackingPolicy(string operation)
    {
        await SeedAsync("Only");
        await using var context = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var repository = new ScopedRepository(context);

        var result = operation switch
        {
            "first" => await repository.FirstOrDefaultAsync(),
            "list" => Assert.Single(await repository.GetListAsync()),
            "single" => await repository.SingleOrDefaultAsync(),
            _ => Assert.Single((await repository.GetPageAsync(new PageRequest())).Items)
        };

        Assert.Equal("Only", result!.Name);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    /// <summary>
    /// Seeds committed fixture rows independently from the context under test.
    /// </summary>
    /// <param name="names">The literal persisted names used by the scenario.</param>
    /// <returns>The fixture seed task.</returns>
    private async Task SeedAsync(params string[] names)
    {
        await using var seed = PostgreSqlTestContext.Create(postgreSql.ConnectionString);
        seed.AddRange(names.Select(name => new TestEntity { Id = Guid.NewGuid(), Name = name }));
        await seed.SaveChangesAsync();
    }

    /// <summary>
    /// Enforces a fixture scope once for all reads while observing the existing extension points.
    /// </summary>
    /// <param name="context">The caller-owned persistence context.</param>
    private sealed class ScopedRepository(DbContext context)
        : GenericRepository<TestEntity>(context, NullLogger<GenericRepository<TestEntity>>.Instance)
    {
        /// <summary>
        /// Counts query preparation through the shared scope seam.
        /// </summary>
        public int QueryCalls { get; private set; }

        /// <summary>
        /// Counts projection creation through the global mapping seam.
        /// </summary>
        public int ProjectionCalls { get; private set; }

        /// <summary>
        /// Adds mandatory scope without bypassing caller filters or search.
        /// </summary>
        /// <param name="predicate">Optional additional caller conditions.</param>
        /// <param name="search">Optional typed search.</param>
        /// <param name="order">Optional typed ordering.</param>
        /// <returns>The unexecuted scoped query.</returns>
        protected override IQueryable<TestEntity> CreateQuery(Expression<Func<TestEntity, bool>>? predicate = null,
            SearchOptions<TestEntity>? search = null, QueryOrder<TestEntity>? order = null)
        {
            QueryCalls++;

            return base.CreateQuery(predicate.AndAlso(row => row.Name != "Hidden"), search, order);
        }

        /// <summary>
        /// Keeps projection creation virtual and delegates mapping to the configured global profile.
        /// </summary>
        /// <typeparam name="TResult">The selected DTO type.</typeparam>
        /// <returns>The provider-side projection expression.</returns>
        protected override Expression<Func<TestEntity, TResult>> CreateProjection<TResult>()
        {
            ProjectionCalls++;

            return base.CreateProjection<TResult>();
        }
    }
}
