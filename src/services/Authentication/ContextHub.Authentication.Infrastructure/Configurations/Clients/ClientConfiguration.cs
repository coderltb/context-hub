namespace ContextHub.Authentication.Infrastructure.Configurations.Clients;

/// <summary>
/// Defines service-owned physical mapping for application identities while preserving native model behavior.
/// </summary>
public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.ToTable("Client", "authentication");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.Name).HasMaxLength(256).IsRequired();
        entity.Property(value => value.CreatedBy).IsRequired();
        entity.Property(value => value.UpdatedBy).IsRequired();
        entity.Property(value => value.Version).IsRowVersion();
    }
}
