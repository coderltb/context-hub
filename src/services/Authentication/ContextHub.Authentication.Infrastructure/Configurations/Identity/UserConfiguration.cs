namespace ContextHub.Authentication.Infrastructure.Configurations.Identity;

/// <summary>
/// Defines service-owned physical mapping for the native Identity store while preserving native model behavior.
/// </summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<User> entity)
    {
        entity.ToTable("User", "identity");

        // Keep native normalization and legacy nulls; the database closes races that application validation cannot.
        entity.HasIndex(user => user.NormalizedEmail)
            .HasDatabaseName(AuthenticationInfrastructureConstants.UNIQUE_EMAIL_INDEX)
            .IsUnique();
    }
}
