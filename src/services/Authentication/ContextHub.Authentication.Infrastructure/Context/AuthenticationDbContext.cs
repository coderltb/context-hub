namespace ContextHub.Authentication.Infrastructure.Context;

/// <summary>
/// Owns framework identity stores and application credentials in one scoped persistence boundary.
/// </summary>
public sealed class AuthenticationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
{
    /// <summary>
    /// Defers native cascades until User deactivation has been converted at the save boundary.
    /// </summary>
    /// <param name="dbContextOptions">The host's provider, model and interceptor configuration.</param>
    public AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> dbContextOptions) : base(dbContextOptions)
    {
        // User interception runs before save-time cascading; explicitly removed dependents retain their own intent.
        ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
    }

    /// <summary>
    /// Gets the application identities owned by Authentication.
    /// </summary>
    public DbSet<Client> Clients => Set<Client>();

    /// <summary>
    /// Gets credential metadata and digests; raw bearer values never belong in this set.
    /// </summary>
    public DbSet<ClientToken> ClientTokens => Set<ClientToken>();

    /// <summary>
    /// Applies service-owned configurations after the native Identity and OpenIddict model conventions.
    /// </summary>
    /// <param name="builder">The model builder provided by EF Core.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Scan only this service's known configuration assembly, as in the reference Authentication source.
        builder.ApplyConfigurationsFromAssembly(typeof(AuthenticationDbContext).Assembly);
    }
}
