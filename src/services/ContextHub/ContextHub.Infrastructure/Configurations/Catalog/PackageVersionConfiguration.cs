namespace ContextHub.Infrastructure.Configurations.Catalog;

/// <summary>
/// Maps PackageVersion to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class PackageVersionConfiguration : IEntityTypeConfiguration<PackageVersion>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<PackageVersion> builder)
    {
        builder.ToTable("Version", "catalog");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();

        builder.Property(value => value.CreatedBy).IsRequired();
        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
        builder.Property(value => value.Number).IsRequired();
        builder.Property(value => value.Status).IsRequired();
        builder.Property(value => value.Manifest).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.Bundle).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.ContentHash).IsRequired();
        builder.Property(value => value.SourceCommitSha).IsRequired();
        builder.Property(value => value.SearchText).IsRequired();
        builder.Property<NpgsqlTsVector>("SearchVector")
            .HasComputedColumnSql("to_tsvector('simple'::regconfig, \"SearchText\")", stored: true).IsRequired();

        // Domain stores a portable float array; only the persistence adapter knows pgvector.
        builder.Property(value => value.Embedding)
            .HasConversion(
                value => value == null ? null : new Vector(value),
                value => value == null ? null : value.ToArray())
            .HasColumnType("vector");

        builder.Property(value => value.Embedding).Metadata.SetValueComparer(new ValueComparer<float[]?>(
            (left, right) => left == null ? right == null : right != null && left.SequenceEqual(right),
            values => values == null ? 0 : values.Aggregate(0, (hash, value) => HashCode.Combine(hash, value)),
            values => values == null ? null : values.ToArray()));
        builder.HasIndex(value => new { value.PackageId, value.Number }).IsUnique()
            .HasDatabaseName("UQ_Catalog_Version_PackageId_Number");
        builder.HasIndex(value => new { value.PackageId, value.Status }).HasDatabaseName("ix_version_package_status");
        builder.HasIndex("SearchVector").HasMethod("gin").HasDatabaseName("ix_version_fts");
        builder.HasIndex(value => value.SearchText).HasMethod("gin").HasOperators("gin_trgm_ops")
            .HasDatabaseName("ix_version_trgm");
    }
}
