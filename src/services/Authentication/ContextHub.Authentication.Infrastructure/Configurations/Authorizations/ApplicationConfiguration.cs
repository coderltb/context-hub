namespace ContextHub.Authentication.Infrastructure.Configurations.Authorizations;

/// <summary>
/// Defines service-owned physical mapping for the native OpenIddict store while preserving native model behavior.
/// </summary>
public sealed class ApplicationConfiguration : IEntityTypeConfiguration<OpenIddictEntityFrameworkCoreApplication<Guid>>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<OpenIddictEntityFrameworkCoreApplication<Guid>> entity)
    {
        entity.ToTable("Application", "openiddict");
    }
}
