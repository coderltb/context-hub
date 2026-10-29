namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Repositories;

/// <summary>
/// Maps isolated parent, optional object and two child levels for real relational search tests.
/// </summary>
/// <param name="options">The disposable PostgreSQL configuration and optional command observer.</param>
internal sealed class NavigationSearchContext(DbContextOptions<NavigationSearchContext> options) : DbContext(options)
{
    /// <summary>
    /// Defines only fixture tables; no product mappings or migrations are used.
    /// </summary>
    /// <param name="modelBuilder">The fixture model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NavigationProduct>().Property(row => row.Id).ValueGeneratedNever();
        modelBuilder.Entity<NavigationCategory>().Property(row => row.Id).ValueGeneratedNever();
        modelBuilder.Entity<NavigationVariant>().Property(row => row.Id).ValueGeneratedNever();
        modelBuilder.Entity<NavigationLabel>().Property(row => row.Id).ValueGeneratedNever();

        modelBuilder.Entity<NavigationProduct>().HasOne(row => row.Category).WithMany();
        modelBuilder.Entity<NavigationProduct>().HasMany(row => row.Variants).WithOne();
        modelBuilder.Entity<NavigationVariant>().HasMany(row => row.Labels).WithOne();
    }
}

/// <summary>
/// Supplies independent scope, scalar search and mapped related data.
/// </summary>
internal sealed class NavigationProduct
{
    /// <summary>
    /// Holds the explicit parent key and ordering tie-breaker.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Holds the mandatory visibility scope.
    /// </summary>
    public int Scope { get; init; } = 1;

    /// <summary>
    /// Holds a root-level searchable value.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Keeps false meaningful in optional field filters.
    /// </summary>
    public bool Active { get; init; }

    /// <summary>
    /// Keeps zero meaningful in optional numeric filters.
    /// </summary>
    public int Stock { get; init; }

    /// <summary>
    /// Holds a nullable reference navigation without requiring automatic Include.
    /// </summary>
    public NavigationCategory? Category { get; init; }

    /// <summary>
    /// Holds mapped children, including deliberately conflicting conditions on different children.
    /// </summary>
    public List<NavigationVariant> Variants { get; init; } = [];
}

/// <summary>
/// Supplies an optional object field for scalar search and ordering.
/// </summary>
internal sealed class NavigationCategory
{
    /// <summary>
    /// Holds the explicit related-object key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Holds the nullable related-object text.
    /// </summary>
    public string? Name { get; init; }
}

/// <summary>
/// Supplies searchable child text, field filters and a second collection level.
/// </summary>
internal sealed class NavigationVariant
{
    /// <summary>
    /// Holds the explicit child key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Holds nullable text selected by CollectionTextFields.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Holds a nullable field used to distinguish equality and IS NULL filters.
    /// </summary>
    public string? Color { get; init; }

    /// <summary>
    /// Holds the numeric condition that must sometimes match the same child as Color.
    /// </summary>
    public int Stock { get; init; }

    /// <summary>
    /// Supplies mapped grandchildren for SelectMany translation.
    /// </summary>
    public List<NavigationLabel> Labels { get; init; } = [];
}

/// <summary>
/// Supplies a grandchild text field without a custom query engine.
/// </summary>
internal sealed class NavigationLabel
{
    /// <summary>
    /// Holds the explicit grandchild key.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Holds nullable text reached through two collection levels.
    /// </summary>
    public string? Text { get; init; }
}
