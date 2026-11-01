namespace ContextHub.Infrastructure.Configurations.Mcp;

/// <summary>
/// Maps prompt configuration to its DB-first table and native row version.
/// </summary>
public sealed class McpPromptConfiguration : IEntityTypeConfiguration<McpPrompt>
{
    /// <summary>
    /// Configures the JSONB column and immutable-name constraint without executing DDL.
    /// </summary>
    /// <param name="builder">The native entity builder.</param>
    public void Configure(EntityTypeBuilder<McpPrompt> builder)
    {
        builder.ToTable(
            "Prompt",
            "access",
            table => table.HasCheckConstraint("CK_Access_Prompt_Arguments_Array", "jsonb_typeof(\"Arguments\") = 'array'"));
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(value => value.Name).IsRequired();
        builder.HasIndex(value => value.Name).IsUnique().HasDatabaseName("UQ_Access_Prompt_Name");
        builder.Property(value => value.Content).IsRequired();
        builder.Property(value => value.Arguments).HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.Property(value => value.IsEnabled).HasDefaultValue(true);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.Property(value => value.UpdatedAt).IsRequired();
        builder.Property(value => value.ConcurrencyVersion).IsRowVersion().HasColumnName("xmin");
    }
}
