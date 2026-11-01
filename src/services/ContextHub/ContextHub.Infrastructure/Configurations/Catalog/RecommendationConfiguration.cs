namespace ContextHub.Infrastructure.Configurations.Catalog;

/// <summary>
/// Maps Recommendation to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class RecommendationConfiguration : IEntityTypeConfiguration<Recommendation>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<Recommendation> builder)
    {
        builder.ToTable("Recommendation", "recommendation");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();

        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
        builder.Property(value => value.Type).IsRequired();
        builder.Property(value => value.Origin).HasDefaultValue("owner").IsRequired();
        builder.Property(value => value.Title).IsRequired();
        builder.Property(value => value.Description).IsRequired();
        builder.Property(value => value.Context).HasColumnType("jsonb");
        builder.Property(value => value.Status).IsRequired();
        builder.Property(value => value.SignalCount).HasDefaultValue(1L);
        builder.HasOne<Package>().WithMany().HasForeignKey(value => value.PackageId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(value => value.DedupeKey).IsUnique()
            .HasFilter("\"Origin\"='client_signal' AND \"Status\" IN ('new','accepted')")
            .HasDatabaseName("uq_recommendation_open_client_signal");
        builder.HasIndex(value => value.PackageId).HasDatabaseName("ix_recommendation_package");
        builder.HasIndex(value => new { value.Status, value.CreatedAt }).IsDescending(false, true)
            .HasDatabaseName("ix_recommendation_status");
    }
}
