namespace ContextHub.Authentication.Infrastructure.Configurations.Authorizations;

/// <summary>
/// Defines service-owned physical mapping for the native OpenIddict store while preserving native model behavior.
/// </summary>
public sealed class TokenConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreToken<Guid>>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreToken<Guid>> entity)
    {
        entity.ToTable("Token", "openiddict");
    }
}
