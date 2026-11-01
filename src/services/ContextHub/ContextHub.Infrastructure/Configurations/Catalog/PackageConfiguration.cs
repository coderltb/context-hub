namespace ContextHub.Infrastructure.Configurations.Catalog;

/// <summary>
/// Maps Package to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("Package", "catalog");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.Property(value => value.CreatedBy).IsRequired();
        builder.Property(value => value.UpdatedBy).IsRequired();
        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
        builder.Property(value => value.Key).IsRequired();
        builder.Property(value => value.Type).IsRequired();
        builder.Property(value => value.Name).IsRequired();
        builder.Property(value => value.Summary).IsRequired();
        builder.HasIndex(value => value.Key).IsUnique().HasDatabaseName("UQ_Catalog_Package_Key");
        builder.HasMany(value => value.Versions).WithOne(value => value.Package)
            .HasForeignKey(value => value.PackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
