namespace ContextHub.Core.Persistence.Queries;

/// <summary>
/// Carries optional typed text and field conditions for repository reads without mixing paging, sorting or cache policy.
/// </summary>
/// <typeparam name="TEntity">The entity whose searchable fields and conditions are selected by service code.</typeparam>
/// <remarks>
/// This is a code-level query input, not an HTTP DTO. Repositories snapshot its collections while preparing
/// one predicate, before any asynchronous read, and never discover additional fields by reflection.
/// </remarks>
public sealed class SearchOptions<TEntity>
{
    /// <summary>
    /// Provides an unchanged literal keyword; null or whitespace skips text search without skipping field filters.
    /// </summary>
    public string? SearchTerm { get; init; }

    /// <summary>
    /// Selects root or related-object strings combined with OR across scalar and collection selectors.
    /// </summary>
    public IReadOnlyList<Expression<Func<TEntity, string?>>>? TextFields { get; init; }

    /// <summary>
    /// Selects mapped child strings searched with Any; empty collections and null strings do not match.
    /// </summary>
    public IReadOnlyList<Expression<Func<TEntity, IEnumerable<string?>>>>? CollectionTextFields { get; init; }

    /// <summary>
    /// Provides independent typed conditions combined with AND; null entries mean no condition.
    /// </summary>
    public IReadOnlyList<Expression<Func<TEntity, bool>>?>? FieldFilters { get; init; }
}
