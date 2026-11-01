namespace ContextHub.Infrastructure.Configurations.Catalog;

/// <summary>
/// Maps DailyUsage to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class DailyUsageConfiguration : IEntityTypeConfiguration<DailyUsage>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<DailyUsage> builder)
    {
        builder.ToTable("DailyUsage", "telemetry");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();

        builder.Property(value => value.Id).UseIdentityByDefaultColumn();
        builder.Property(value => value.ToolName).IsRequired();
        builder.HasOne<Package>().WithMany().HasForeignKey(value => value.PackageId).OnDelete(DeleteBehavior.SetNull);
        builder.HasIndex(value => new { value.Date, value.ToolName, value.PackageId, value.ClientId, value.ClientFamily })
            .IsUnique().AreNullsDistinct(false).HasDatabaseName("UQ_Telemetry_DailyUsage_Dimensions");
        builder.HasIndex(value => value.Date).IsDescending().HasDatabaseName("ix_usage_date");
        builder.HasIndex(value => value.PackageId).HasDatabaseName("ix_usage_package");
        builder.HasIndex(value => new { value.ClientId, value.Date }).IsDescending(false, true)
            .HasDatabaseName("ix_usage_mcp_client");
    }
}
