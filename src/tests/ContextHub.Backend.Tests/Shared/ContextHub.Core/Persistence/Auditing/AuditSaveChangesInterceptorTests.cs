namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Persistence.Auditing;

/// <summary>
/// Checks opt-in stamping against PostgreSQL rather than EF InMemory or SQLite.
/// </summary>
/// <param name="postgreSql">The isolated PostgreSQL fixture.</param>
[Collection(PostgreSqlCollection.Name)]
public sealed class AuditSaveChangesInterceptorTests(PostgreSqlFixture postgreSql) : IAsyncLifetime
{
    /// <summary>
    /// Persists the audit update even when a caller deliberately manages tracking without automatic change detection.
    /// </summary>
    /// <returns>The asynchronous explicit-tracking assertions.</returns>
    [Fact]
    public async Task ExplicitTracking_PersistsUpdatedAuditFields()
    {
        await using var context = CreateContext();
        var entity = new AuditEntity { Id = Guid.NewGuid() };
        context.Add(entity);
        await context.SaveChangesAsync();

        _clock.Advance(TimeSpan.FromMinutes(1));
        _actor.ActorId = Guid.NewGuid();
        context.ChangeTracker.AutoDetectChangesEnabled = false;
        entity.Name = "Explicit update";
        context.Entry(entity).Property(value => value.Name).IsModified = true;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.Set<AuditEntity>().SingleAsync();
        Assert.Equal(_clock.GetUtcNow(), stored.UpdatedAt);
        Assert.Equal(_actor.ActorId, stored.UpdatedBy);
    }

    /// <summary>
    /// Provides deterministic timestamps without sleeping.
    /// </summary>
    private readonly AuditClock _clock = new();

    /// <summary>
    /// Supplies an explicit background-test actor.
    /// </summary>
    private readonly TestActor _actor = new();

    /// <summary>
    /// Creates only isolated test tables; no product migration or design script is executed.
    /// </summary>
    /// <returns>The asynchronous fixture reset.</returns>
    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Leaves container lifetime with the collection fixture.
    /// </summary>
    /// <returns>A completed cleanup task.</returns>
    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>
    /// Sets equal creation/update timestamps and protects creation fields on later saves.
    /// </summary>
    /// <returns>The asynchronous persistence assertions.</returns>
    [Fact]
    public async Task AddedAndModified_StampOnlyTheOwningFields()
    {
        await using var context = CreateContext();
        var entity = new AuditEntity { Id = Guid.NewGuid() };
        context.Add(entity);
        await context.SaveChangesAsync();
        Assert.Equal(_clock.GetUtcNow(), entity.CreatedAt);
        Assert.Equal(entity.CreatedAt, entity.UpdatedAt);
        Assert.Equal(_actor.ActorId, entity.CreatedBy);

        var originalTime = entity.CreatedAt;
        var originalActor = entity.CreatedBy;
        _clock.Advance(TimeSpan.FromMinutes(1));
        _actor.ActorId = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.MinValue;
        entity.CreatedBy = Guid.NewGuid();
        entity.Name = "Changed";
        context.SaveChanges();

        context.ChangeTracker.Clear();
        var stored = await context.Set<AuditEntity>().SingleAsync();
        Assert.Equal(originalTime, stored.CreatedAt);
        Assert.Equal(originalActor, stored.CreatedBy);
        Assert.Equal(_clock.GetUtcNow(), stored.UpdatedAt);
        Assert.Equal(_actor.ActorId, stored.UpdatedBy);
        Assert.Equal(TimeSpan.Zero, stored.UpdatedAt.Offset);
    }

    /// <summary>
    /// Accepts absent actors only where the EF model explicitly allows null.
    /// </summary>
    /// <returns>The asynchronous nullability assertions.</returns>
    [Fact]
    public async Task ActorNullability_FollowsEfMapping()
    {
        _actor.ActorId = null;
        await using var context = CreateContext();
        var optional = new AuditEntity { Id = Guid.NewGuid() };
        context.Add(optional);
        await context.SaveChangesAsync();
        Assert.Null(optional.CreatedBy);
        Assert.Null(optional.UpdatedBy);

        context.Add(new RequiredActorEntity { Id = Guid.NewGuid() });
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());
    }

    /// <summary>
    /// Does not stamp non-opt-in entities and preserves cancellation before stamping.
    /// </summary>
    /// <returns>The asynchronous opt-in and cancellation assertions.</returns>
    [Fact]
    public async Task NonOptInAndCancellation_KeepTheirOriginalState()
    {
        await using var context = CreateContext();
        var plain = new PlainAuditEntity { Id = Guid.NewGuid() };
        context.Add(plain);
        await context.SaveChangesAsync();
        Assert.Equal(default, plain.CreatedAt);

        var cancelled = new AuditEntity { Id = Guid.NewGuid() };
        context.Add(cancelled);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.SaveChangesAsync(new CancellationToken(true)));
        Assert.Equal(default, cancelled.CreatedAt);
    }

    /// <summary>
    /// Creates the test context with the scoped interceptor attached explicitly.
    /// </summary>
    /// <returns>The caller-owned context.</returns>
    private AuditContext CreateContext() => new(new DbContextOptionsBuilder<AuditContext>()
        .UseNpgsql(postgreSql.ConnectionString)
        .AddInterceptors(new AuditSaveChangesInterceptor(_clock, _actor, NullLogger<AuditSaveChangesInterceptor>.Instance)).Options);

    /// <summary>
    /// Maps separate optional and required actor entities without inheriting business base classes.
    /// </summary>
    private sealed class AuditContext : DbContext
    {
        /// <summary>
        /// Initializes the isolated test model.
        /// </summary>
        /// <param name="options">The test-only PostgreSQL options.</param>
        public AuditContext(DbContextOptions<AuditContext> options) : base(options) { }

        /// <summary>
        /// Configures the test model's actor nullability explicitly.
        /// </summary>
        /// <param name="modelBuilder">The EF test model builder.</param>
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
    /// Supplies the explicit test actor, including null for anonymous background work.
    /// </summary>
    private sealed class TestActor : IAuditActorAccessor
    {
        /// <summary>
        /// Holds the actor selected by the test operation.
        /// </summary>
        public Guid? ActorId { get; set; } = Guid.NewGuid();
    }

    /// <summary>
    /// Supplies controlled UTC time for both synchronous and asynchronous saves.
    /// </summary>
    private sealed class AuditClock : TimeProvider
    {
        /// <summary>
        /// Stores the controlled instant.
        /// </summary>
        private DateTimeOffset _now = new(2026, 10, 6, 0, 0, 0, TimeSpan.Zero);

        /// <summary>
        /// Returns the controlled UTC instant.
        /// </summary>
        /// <returns>The current test time.</returns>
        public override DateTimeOffset GetUtcNow() => _now;

        /// <summary>
        /// Advances the current save boundary time.
        /// </summary>
        /// <param name="amount">The elapsed test duration.</param>
        public void Advance(TimeSpan amount) => _now += amount;
    }
}

/// <summary>
/// Defines an opt-in audit test entity without business policy.
/// </summary>
public sealed class AuditEntity : IAuditableEntity
{
    /// <summary>
    /// Identifies the isolated test row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Provides a mutable field for update verification.
    /// </summary>
    public string Name { get; set; } = "Original";

    /// <summary>
    /// Records the original UTC creation instant.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Records the optional actor selected by the owning service's EF mapping.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Records the latest UTC update instant.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Records the latest actor without fabricating an anonymous identifier.
    /// </summary>
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Defines an opt-in audit test entity without business policy.
/// </summary>
public sealed class RequiredActorEntity : IAuditableEntity
{
    /// <summary>
    /// Identifies the isolated test row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Provides a mutable field for update verification.
    /// </summary>
    public string Name { get; set; } = "Original";

    /// <summary>
    /// Records the original UTC creation instant.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Records the optional actor selected by the owning service's EF mapping.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Records the latest UTC update instant.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Records the latest actor without fabricating an anonymous identifier.
    /// </summary>
    public Guid? UpdatedBy { get; set; }
}

/// <summary>
/// Defines an ordinary entity whose similarly named properties do not opt it into auditing.
/// </summary>
public sealed class PlainAuditEntity
{
    /// <summary>
    /// Identifies the isolated row.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Remains untouched because the entity does not implement the opt-in contract.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}
