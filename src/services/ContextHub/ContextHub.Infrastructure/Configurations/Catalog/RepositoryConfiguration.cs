namespace ContextHub.Infrastructure.Configurations.Catalog;

/// <summary>
/// Maps Repository to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class RepositoryConfiguration : IEntityTypeConfiguration<Repository>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<Repository> builder)
    {
        builder.ToTable("Repository", "integration");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.Property(value => value.CreatedBy).IsRequired();
        builder.Property(value => value.UpdatedBy).IsRequired();
        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
        builder.Property(value => value.Url).IsRequired();
        builder.Property(value => value.DefaultRef).IsRequired();
        builder.HasIndex(value => value.Url).IsUnique().HasDatabaseName(IntegrationInfrastructureConstants.REPOSITORY_URL_CONSTRAINT);
    }
}
