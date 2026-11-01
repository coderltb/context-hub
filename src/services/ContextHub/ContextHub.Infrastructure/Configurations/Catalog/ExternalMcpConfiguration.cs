namespace ContextHub.Infrastructure.Configurations.Catalog;

/// <summary>
/// Maps ExternalMcp to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class ExternalMcpConfiguration : IEntityTypeConfiguration<ExternalMcp>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<ExternalMcp> builder)
    {
        builder.ToTable("ExternalMcp", "integration");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.Property(value => value.CreatedBy).IsRequired();
        builder.Property(value => value.UpdatedBy).IsRequired();
        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
        builder.Property(value => value.Name).IsRequired();
        builder.Property(value => value.Config).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(value => value.Name).IsUnique().HasDatabaseName("UQ_Integration_ExternalMcp_Name");
    }
}
