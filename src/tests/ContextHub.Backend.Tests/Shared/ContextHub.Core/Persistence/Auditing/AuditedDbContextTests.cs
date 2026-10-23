namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Auditing;

/// <summary>
/// Verifies DI-attached audit interception on an isolated PostgreSQL database through both native save paths.
/// </summary>
/// <param name="postgreSql">The collection-owned PostgreSQL Testcontainer.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class AuditedDbContextTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Owns only this test's DI registrations; the collection fixture owns the PostgreSQL container.
    /// </summary>
    private ServiceProvider _provider = null!;

    /// <summary>
    /// Provides deterministic UTC time without delaying persistence tests.
    /// </summary>
    private readonly AuditClock _clock = new();

    /// <summary>
    /// Resolves the context through DI and resets only the isolated Testcontainer database.
    /// </summary>
    /// <returns>The asynchronous test-model initialization.</returns>
    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(_clock);
        services.AddScoped<IAuditActorAccessor, ScopedActor>();
        services.AddAuditedDbContext<AuditContext>((_, options) =>
            options.UseNpgsql(postgreSql.ConnectionString));
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        // Native DI supplies the interceptor; these tests never construct it manually or use product migrations.
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Disposes the test-owned container of services without taking ownership of the PostgreSQL fixture.
    /// </summary>
    /// <returns>The asynchronous DI cleanup.</returns>
    public Task DisposeAsync() => _provider.DisposeAsync().AsTask();

    /// <summary>
    /// Automatically stamps saves, protects original creation fields and leaves non-opt-in entities untouched.
    /// </summary>
    /// <param name="useAsync">Whether to exercise SaveChangesAsync rather than synchronous SaveChanges.</param>
    /// <returns>The asynchronous verification of persisted rows.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveThroughDi_StampsAndProtectsOptInFields(bool useAsync)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditContext>();
        var actor = (ScopedActor)scope.ServiceProvider.GetRequiredService<IAuditActorAccessor>();
        var entity = new AuditEntity { Id = Guid.NewGuid() };

        // A different sentinel makes accidental stamping of the non-opt-in entity observable on the first save.
        var plainCreatedAt = _clock.GetUtcNow().AddDays(-1);
        var plain = new PlainAuditEntity { Id = Guid.NewGuid(), CreatedAt = plainCreatedAt };
        context.AddRange(entity, plain);
        await SaveAsync(context, useAsync);

        var createdAt = entity.CreatedAt;
        var createdBy = actor.ActorId;
        Assert.Equal(_clock.GetUtcNow(), createdAt);
        Assert.Equal(createdAt, entity.UpdatedAt);
        Assert.Equal(createdBy, entity.CreatedBy);
        Assert.Equal(createdBy, entity.UpdatedBy);

        // Tampering with tracked creation fields must not change their stored values during an update.
        _clock.Now = _clock.Now.AddMinutes(1);
        actor.ActorId = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.MinValue;
        entity.CreatedBy = Guid.NewGuid();
        entity.Name = "Updated through DI";
        await SaveAsync(context, useAsync);
        context.ChangeTracker.Clear();

        var stored = await context.Set<AuditEntity>().SingleAsync();
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.Equal(createdBy, stored.CreatedBy);
        Assert.Equal(_clock.GetUtcNow(), stored.UpdatedAt);
        Assert.Equal(actor.ActorId, stored.UpdatedBy);
        Assert.Equal(TimeSpan.Zero, stored.UpdatedAt.Offset);
        Assert.Equal(plainCreatedAt, (await context.Set<PlainAuditEntity>().SingleAsync()).CreatedAt);
    }

    /// <summary>
    /// Keeps persisted creation fields when an unrelated detached instance is attached for update.
    /// </summary>
    /// <param name="useAsync">Whether to exercise asynchronous rather than synchronous saving.</param>
    /// <returns>The independent database assertions for creation and update fields.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DetachedUpdate_PreservesStoredCreationFields(bool useAsync)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditContext>();
        var entity = new AuditEntity { Id = Guid.NewGuid(), Name = "Original" };
        context.Add(entity);
        await SaveAsync(context, useAsync);
        var createdAt = entity.CreatedAt;
        var createdBy = entity.CreatedBy;
        context.ChangeTracker.Clear();

        // A detached object's original values are untrusted; creation columns must never be included in its UPDATE.
        _clock.Now = _clock.Now.AddMinutes(1);
        var actor = (ScopedActor)scope.ServiceProvider.GetRequiredService<IAuditActorAccessor>();
        actor.ActorId = Guid.NewGuid();
        context.Update(new AuditEntity
        {
            Id = entity.Id,
            Name = "Detached update",
            CreatedAt = DateTimeOffset.MinValue,
            CreatedBy = Guid.NewGuid()
        });
        await SaveAsync(context, useAsync);

        await using var observerScope = _provider.CreateAsyncScope();
        var observer = observerScope.ServiceProvider.GetRequiredService<AuditContext>();
        var stored = await observer.Set<AuditEntity>().AsNoTracking().SingleAsync();
        Assert.Equal(createdAt, stored.CreatedAt);
        Assert.Equal(createdBy, stored.CreatedBy);
        Assert.Equal("Detached update", stored.Name);
        Assert.Equal(_clock.GetUtcNow(), stored.UpdatedAt);
        Assert.Equal(actor.ActorId, stored.UpdatedBy);
    }

    /// <summary>
    /// Resolves independent contexts and actors in simultaneous scopes without leaking stamping identity.
    /// </summary>
    /// <returns>The asynchronous cross-scope persistence assertions.</returns>
    [Fact]
    public async Task SeparateScopes_KeepActorsIsolated()
    {
        await using var first = _provider.CreateAsyncScope();
        await using var second = _provider.CreateAsyncScope();
        var firstContext = first.ServiceProvider.GetRequiredService<AuditContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<AuditContext>();
        var firstActor = first.ServiceProvider.GetRequiredService<IAuditActorAccessor>();
        var secondActor = second.ServiceProvider.GetRequiredService<IAuditActorAccessor>();
        var firstEntity = new AuditEntity { Id = Guid.NewGuid() };
        var secondEntity = new AuditEntity { Id = Guid.NewGuid() };
        firstContext.Add(firstEntity);
        secondContext.Add(secondEntity);

        // Concurrent saves exercise separate scoped interceptor dependencies against the same test database.
        await Task.WhenAll(firstContext.SaveChangesAsync(), secondContext.SaveChangesAsync());
        firstContext.ChangeTracker.Clear();
        secondContext.ChangeTracker.Clear();

        Assert.NotEqual(firstActor.ActorId, secondActor.ActorId);
        Assert.Equal(firstActor.ActorId,
            (await firstContext.Set<AuditEntity>().SingleAsync(value => value.Id == firstEntity.Id)).CreatedBy);
        Assert.Equal(secondActor.ActorId,
            (await secondContext.Set<AuditEntity>().SingleAsync(value => value.Id == secondEntity.Id)).CreatedBy);
    }

    /// <summary>
    /// Honors service-provided actor nullability on both synchronous and asynchronous save boundaries.
    /// </summary>
    /// <param name="useAsync">Whether the save boundary uses the asynchronous EF API.</param>
    /// <returns>The asynchronous actor-nullability assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingActor_FollowsModelNullability(bool useAsync)
    {
        await using var scope = _provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AuditContext>();
        ((ScopedActor)scope.ServiceProvider.GetRequiredService<IAuditActorAccessor>()).ActorId = null;
        var optional = new AuditEntity { Id = Guid.NewGuid() };
        context.Add(optional);
        await SaveAsync(context, useAsync);
        Assert.Null(optional.CreatedBy);
        Assert.Null(optional.UpdatedBy);

        // A required actor is an owning-model rule; shared registration must not manufacture one.
        context.Add(new RequiredActorEntity { Id = Guid.NewGuid() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => SaveAsync(context, useAsync));
    }

    /// <summary>
    /// Selects the native save API under test without altering exception or transaction behavior.
    /// </summary>
    /// <param name="context">The DI-owned test context.</param>
    /// <param name="useAsync">Whether to use asynchronous saving.</param>
    /// <returns>The affected row count from the selected native save API.</returns>
    private static Task<int> SaveAsync(AuditContext context, bool useAsync) =>
        useAsync ? context.SaveChangesAsync() : Task.FromResult(context.SaveChanges());

    /// <summary>
    /// Maps opt-in and ordinary entities only for this isolated PostgreSQL test model.
    /// </summary>
    /// <param name="options">The scoped native PostgreSQL context options.</param>
    private sealed class AuditContext(DbContextOptions<AuditContext> options) : DbContext(options)
    {
        /// <summary>
        /// Makes actor nullability explicit rather than inheriting application-specific entity policies.
        /// </summary>
        /// <param name="modelBuilder">The native EF test model builder.</param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<AuditEntity>().HasKey(entity => entity.Id);
            modelBuilder.Entity<RequiredActorEntity>().HasKey(entity => entity.Id);
            modelBuilder.Entity<RequiredActorEntity>().Property(entity => entity.CreatedBy).IsRequired();
            modelBuilder.Entity<RequiredActorEntity>().Property(entity => entity.UpdatedBy).IsRequired();
            modelBuilder.Entity<PlainAuditEntity>().HasKey(entity => entity.Id);
        }
    }

    /// <summary>
    /// Supplies a mutable actor owned by one DI scope, including null for explicit anonymous test operations.
    /// </summary>
    private sealed class ScopedActor : IAuditActorAccessor
    {
        /// <summary>
        /// Holds the identity selected by the test scope.
        /// </summary>
        public Guid? ActorId { get; set; } = Guid.NewGuid();
    }

    /// <summary>
    /// Supplies controlled UTC time shared only within one test's provider.
    /// </summary>
    private sealed class AuditClock : TimeProvider
    {
        /// <summary>
        /// Holds the current deterministic test instant.
        /// </summary>
        public DateTimeOffset Now { get; set; } = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Returns the explicitly selected UTC instant.
        /// </summary>
        /// <returns>The current test time without wall-clock dependencies.</returns>
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
