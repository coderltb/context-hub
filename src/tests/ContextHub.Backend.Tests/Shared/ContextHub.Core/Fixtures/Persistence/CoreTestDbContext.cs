namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Fixtures.Persistence;

/// <summary>
/// Maps a minimal test entity and counts saves for PostgreSQL persistence tests.
/// </summary>
/// <param name="options">The fixture's configured EF context options.</param>
public sealed class CoreTestDbContext(DbContextOptions<CoreTestDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the entity set used to exercise repository tracking and transaction behavior.
    /// </summary>
    public DbSet<TestEntity> Entities => Set<TestEntity>();

    /// <summary>
    /// Gets the number of asynchronous save calls made through this context instance.
    /// </summary>
    public int SaveCallCount { get; private set; }

    /// <summary>
    /// Counts the save attempt before delegating persistence to EF Core.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Whether the caller accepts tracker changes after this save.</param>
    /// <param name="cancellationToken">Cancellation propagated to the underlying operation.</param>
    /// <returns>The number of persisted state entries reported by the fixture context.</returns>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        SaveCallCount++;
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Configures the isolated test table, primary key and required bounded name.
    /// </summary>
    /// <param name="modelBuilder">The EF model builder configuring the fixture's persistence model.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestEntity>(builder =>
        {
            builder.ToTable("CoreEntity");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Name).HasMaxLength(200).IsRequired();
        });
    }
}

/// <summary>
/// Provides a minimal entity for repository and transaction checks.
/// </summary>
public sealed class TestEntity
{
    /// <summary>
    /// Gets or sets the primary key used by persistence tests.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the required name used to verify stored values.
    /// </summary>
    public required string Name { get; set; }
}
