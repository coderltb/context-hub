namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Repositories;

/// <summary>
/// Verifies specific repository reuse and key handling without changing context or commit ownership.
/// </summary>
/// <param name="postgreSql">The collection-owned disposable PostgreSQL database.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class RepositoryInheritanceTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Resets only the disposable fixture schema.
    /// </summary>
    /// <returns>The schema initialization task.</returns>
    public Task InitializeAsync() => PostgreSqlTestContext.ResetAsync(postgreSql.ConnectionString);

    /// <summary>
    /// Leaves container ownership with the collection fixture.
    /// </summary>
    /// <returns>An already completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Resolves a derived repository and Unit of Work in the same scope, then commits their shared tracked changes once.
    /// </summary>
    /// <returns>The inherited CRUD, context isolation and paging assertions.</returns>
    [Fact]
    public async Task SpecificRepositoryReusesScopedContextCrudAndPaging()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<CoreTestDbContext>(options => options.UseNpgsql(postgreSql.ConnectionString));
        services.AddPersistence<CoreTestDbContext>();
        services.AddScoped<SpecificRepository>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
            { ValidateOnBuild = true, ValidateScopes = true });
        await using var scope = provider.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<SpecificRepository>();
        var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var context = scope.ServiceProvider.GetRequiredService<CoreTestDbContext>();
        var id = Guid.NewGuid();

        await repository.AddAsync(new TestEntity { Id = id, Name = "Original" });
        repository.RenameStaged(id, "Changed through protected context");
        var before = await repository.GetPageAsync(new PageRequest(), QueryOrder<TestEntity>.By(row => row.Id));

        Assert.Empty(before.Items);
        Assert.Equal(0, context.SaveCallCount);
        Assert.True(repository.AddOverrideCalled);
        Assert.Equal(1, await work.SaveChangesAsync());
        Assert.Equal(1, context.SaveCallCount);
        work.ClearTrackedChanges();

        var page = await repository.GetPageAsync(row => row.Name, new PageRequest(), QueryOrder<TestEntity>.By(row => row.Id));
        Assert.Equal(new[] { "Changed through protected context" }, page.Items);
        Assert.Empty(context.ChangeTracker.Entries());
        await using var otherScope = provider.CreateAsyncScope();
        Assert.NotSame(context, otherScope.ServiceProvider.GetRequiredService<CoreTestDbContext>());
    }

    /// <summary>
    /// Uses typed predicates for shadow and composite keys without assuming every entity has an Id member.
    /// </summary>
    /// <returns>The PostgreSQL key predicate and native tracking assertions.</returns>
    [Fact]
    public async Task PredicateLookupSupportsShadowAndCompositeKeys()
    {
        var options = new DbContextOptionsBuilder<KeyContext>().UseNpgsql(postgreSql.ConnectionString).Options;
        await using var context = new KeyContext(options);

        // This collection supplies only its disposable container database, never a product connection.
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
        var shadow = new ShadowEntity { Name = "Shadow value" };
        context.Add(shadow).Property<int>("Key").CurrentValue = 17;
        context.AddRange(new CompositeEntity { First = 1, Second = 2 }, new CompositeEntity { First = 1, Second = 3 });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var shadowRepository = new GenericRepository<ShadowEntity>(context, NullLogger<GenericRepository<ShadowEntity>>.Instance);
        var compositeRepository = new GenericRepository<CompositeEntity>(context, NullLogger<GenericRepository<CompositeEntity>>.Instance);

        // EF.Property addresses a mapped shadow key; composite keys remain an explicit conjunction chosen by the caller.
        var result = await shadowRepository.FirstOrDefaultAsync(predicate: row => EF.Property<int>(row, "Key") == 17);
        var composite = await compositeRepository.FirstOrDefaultAsync(predicate: row => row.First == 1 && row.Second == 2);

        Assert.NotNull(result);
        Assert.Equal("Shadow value", result.Name);
        Assert.Null(await shadowRepository.FirstOrDefaultAsync(predicate: row => EF.Property<int>(row, "Key") == 18));
        Assert.NotNull(composite);
        Assert.Equal(2, composite.Second);
        Assert.Null(await compositeRepository.FirstOrDefaultAsync(predicate: row => row.First == 1 && row.Second == 4));
        Assert.Same(result, Assert.Single(context.ChangeTracker.Entries<ShadowEntity>()).Entity);
        Assert.Same(composite, Assert.Single(context.ChangeTracker.Entries<CompositeEntity>()).Entity);
    }

    /// <summary>
    /// Demonstrates a specific repository adding behavior while retaining inherited staging and paging.
    /// </summary>
    /// <param name="context">The same scoped context used by the shared Unit of Work.</param>
    /// <param name="logger">The shared repository's metadata logger.</param>
    private sealed class SpecificRepository(DbContext context, ILogger<GenericRepository<TestEntity>> logger)
        : GenericRepository<TestEntity>(context, logger)
    {
        /// <summary>
        /// Records whether virtual dispatch reached the specific repository.
        /// </summary>
        public bool AddOverrideCalled { get; private set; }

        /// <summary>
        /// Adds feature-specific staging behavior without introducing another save boundary.
        /// </summary>
        /// <param name="entity">The entity staged in the inherited repository.</param>
        /// <param name="cancellationToken">Cancellation passed unchanged to inherited staging.</param>
        /// <returns>The entity tracked by the shared scoped context.</returns>
        public override Task<TestEntity> AddAsync(TestEntity entity, CancellationToken cancellationToken = default)
        {
            AddOverrideCalled = true;

            return base.AddAsync(entity, cancellationToken);
        }

        /// <summary>
        /// Uses the protected context to modify an entity staged by inherited CRUD.
        /// </summary>
        /// <param name="id">The fixture entity identifier.</param>
        /// <param name="name">The new fixture value.</param>
        public void RenameStaged(Guid id, string name)
        {
            _dbContext.Set<TestEntity>().Local.Single(row => row.Id == id).Name = name;
        }
    }

    /// <summary>
    /// Maps shadow and composite keys solely for integration-test coverage.
    /// </summary>
    /// <param name="options">The disposable PostgreSQL connection configuration.</param>
    private sealed class KeyContext(DbContextOptions<KeyContext> options) : DbContext(options)
    {
        /// <summary>
        /// Makes shadow key type and composite key ownership explicit in the test model.
        /// </summary>
        /// <param name="modelBuilder">The fixture model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ShadowEntity>().Property<int>("Key").ValueGeneratedNever();
            modelBuilder.Entity<ShadowEntity>().HasKey("Key");
            modelBuilder.Entity<CompositeEntity>().HasKey(row => new { row.First, row.Second });
        }
    }

    /// <summary>
    /// Carries a value while its identifier exists only in the EF model.
    /// </summary>
    private sealed class ShadowEntity
    {
        /// <summary>
        /// Holds the value returned by the shadow-key lookup.
        /// </summary>
        public required string Name { get; set; }
    }

    /// <summary>
    /// Requires a specific query rather than a guessed single-column lookup.
    /// </summary>
    private sealed class CompositeEntity
    {
        /// <summary>
        /// Holds the first primary-key component.
        /// </summary>
        public int First { get; set; }

        /// <summary>
        /// Holds the second primary-key component.
        /// </summary>
        public int Second { get; set; }
    }
}
