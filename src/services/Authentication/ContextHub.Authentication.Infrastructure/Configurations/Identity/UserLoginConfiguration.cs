namespace ContextHub.Authentication.Infrastructure.Configurations.Identity;

/// <summary>
/// Defines service-owned physical mapping for the native Identity store while preserving native model behavior.
/// </summary>
public sealed class UserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> entity)
    {
        entity.ToTable("UserLogin", "identity");
        entity.Property(value => value.LoginProvider).HasMaxLength(128);
        entity.Property(value => value.ProviderKey).HasMaxLength(128);
    }
}
