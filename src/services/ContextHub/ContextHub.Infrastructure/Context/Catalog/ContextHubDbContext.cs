namespace ContextHub.Infrastructure.Context.Catalog;

/// <summary>
/// Maps only the eight Context Hub-owned tables; Authentication remains a separate service authority.
/// </summary>
/// <param name="options">The service-owned PostgreSQL context options.</param>
public sealed class ContextHubDbContext(DbContextOptions<ContextHubDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the Packages table query and staging set.
    /// </summary>
    public DbSet<Package> Packages => Set<Package>();

    /// <summary>
    /// Gets the Versions table query and staging set.
    /// </summary>
    public DbSet<PackageVersion> Versions => Set<PackageVersion>();

    /// <summary>
    /// Gets the Repositories table query and staging set.
    /// </summary>
    public DbSet<Repository> Repositories => Set<Repository>();

    /// <summary>
    /// Gets the ExternalMcps table query and staging set.
    /// </summary>
    public DbSet<ExternalMcp> ExternalMcps => Set<ExternalMcp>();

    /// <summary>
    /// Gets the Tools table query and staging set.
    /// </summary>
    public DbSet<Tool> Tools => Set<Tool>();

    /// <summary>
    /// Gets the server-owned guidance configurations.
    /// </summary>
    public DbSet<McpPrompt> Prompts => Set<McpPrompt>();

    /// <summary>
    /// Gets the DailyUsage table query and staging set.
    /// </summary>
    public DbSet<DailyUsage> DailyUsage => Set<DailyUsage>();

    /// <summary>
    /// Gets the Recommendations table query and staging set.
    /// </summary>
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    /// <summary>
    /// Applies service-owned configurations without creating or migrating database objects.
    /// </summary>
    /// <param name="modelBuilder">The native EF model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Keep model ownership explicit; referenced Authentication configurations are never scanned.
        modelBuilder.ApplyConfiguration(new PackageConfiguration());
        modelBuilder.ApplyConfiguration(new PackageVersionConfiguration());
        modelBuilder.ApplyConfiguration(new RepositoryConfiguration());
        modelBuilder.ApplyConfiguration(new ExternalMcpConfiguration());
        modelBuilder.ApplyConfiguration(new ToolConfiguration());
        modelBuilder.ApplyConfiguration(new McpPromptConfiguration());
        modelBuilder.ApplyConfiguration(new DailyUsageConfiguration());
        modelBuilder.ApplyConfiguration(new RecommendationConfiguration());
    }
}
