namespace ContextHub.Authentication.Infrastructure.Configurations.Clients;

/// <summary>
/// Defines service-owned physical mapping for application credential metadata while preserving native model behavior.
/// </summary>
public sealed class ClientTokenConfiguration : IEntityTypeConfiguration<ClientToken>
{
    /// <summary>
    /// Applies physical names and the explicit provider constraints owned by this entity.
    /// </summary>
    /// <param name="entity">The native entity mapping builder.</param>
    public void Configure(EntityTypeBuilder<ClientToken> entity)
    {
        entity.ToTable("ClientToken", "authentication");
        entity.HasKey(value => value.Id);
        entity.HasOne(value => value.Client).WithMany(value => value.Tokens).HasForeignKey(value => value.ClientId);
        entity.Property(value => value.SecretHash).IsRequired();
        entity.Property(value => value.RevokedAt).IsConcurrencyToken();
        entity.Property(value => value.CreatedBy).IsRequired();
        entity.Property(value => value.AllowedTools).HasColumnType("text[]").IsRequired();
        entity.HasIndex(value => value.ClientId);
    }
}
