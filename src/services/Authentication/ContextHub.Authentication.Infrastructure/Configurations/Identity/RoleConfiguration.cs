namespace ContextHub.Authentication.Infrastructure.Configurations.Identity;

/// <summary>
/// Defines service-owned physical mapping for the native Identity store while preserving native model behavior.
/// </summary>
public sealed class RoleConfiguration : IEntityTypeConfiguration<IdentityRole<Guid>>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<IdentityRole<Guid>> entity)
    {
        entity.ToTable("Role", "identity");
    }
}
