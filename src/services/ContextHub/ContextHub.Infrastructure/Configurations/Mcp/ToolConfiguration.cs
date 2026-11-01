namespace ContextHub.Infrastructure.Configurations.Mcp;

/// <summary>
/// Maps Tool to its reviewed DB-first table and native PostgreSQL concurrency contract.
/// </summary>
public sealed class ToolConfiguration : IEntityTypeConfiguration<Tool>
{
    /// <summary>
    /// Configures columns, ownership and indexes without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity configuration builder.</param>
    public void Configure(EntityTypeBuilder<Tool> builder)
    {
        builder.ToTable("Tool", "access");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();

        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
        builder.Property(value => value.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(value => value.Name).IsRequired();
        builder.Property(value => value.Title).IsRequired();
        builder.Property(value => value.Description).IsRequired();
        builder.Property(value => value.IsEnabled).HasDefaultValue(true);
        builder.Property(value => value.InputSchema).HasColumnType("jsonb").IsRequired();
        builder.Property(value => value.OutputSchema).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(value => value.Name).IsUnique().HasDatabaseName("UQ_Access_Tool_Name");
    }
}
