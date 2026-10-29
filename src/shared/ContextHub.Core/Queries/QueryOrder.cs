namespace ContextHub.Core.Queries;

/// <summary>
/// Builds immutable typed ordering without exposing a query builder or property-name sorting.
/// </summary>
/// <typeparam name="TEntity">The entity being ordered.</typeparam>
/// <remarks>Callers must include a unique final key when stable pagination is required.</remarks>
public sealed class QueryOrder<TEntity>
{
    /// <summary>
    /// Applies only native LINQ ordering while retaining each key's actual CLR type.
    /// </summary>
    private readonly Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> _apply;

    /// <summary>
    /// Captures one immutable ordering plan without retaining a context or executing a query.
    /// </summary>
    /// <param name="apply">The provider-readable ordering operation.</param>
    private QueryOrder(Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> apply)
    {
        _apply = apply;
    }

    /// <summary>
    /// Starts an ordering using a provider-translatable typed key.
    /// </summary>
    /// <typeparam name="TKey">The selected key type.</typeparam>
    /// <param name="selector">The required ordering key expression.</param>
    /// <param name="descending">Whether the first key sorts descending.</param>
    /// <returns>A new ordering plan; no database work is performed.</returns>
    public static QueryOrder<TEntity> By<TKey>(Expression<Func<TEntity, TKey>> selector, bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(selector);

        return new QueryOrder<TEntity>(query => descending
            ? query.OrderByDescending(selector)
            : query.OrderBy(selector));
    }

    /// <summary>
    /// Appends a tie-breaker without altering this ordering or another caller's query.
    /// </summary>
    /// <typeparam name="TKey">The additional key type.</typeparam>
    /// <param name="selector">The required provider-translatable tie-breaker.</param>
    /// <param name="descending">Whether this additional key sorts descending.</param>
    /// <returns>A new ordering that includes all previous keys and the additional key.</returns>
    public QueryOrder<TEntity> ThenBy<TKey>(Expression<Func<TEntity, TKey>> selector, bool descending = false)
    {
        ArgumentNullException.ThrowIfNull(selector);

        // Capture this plan, not mutable key state; branches can add different tie-breakers safely.
        return new QueryOrder<TEntity>(query => descending
            ? _apply(query).ThenByDescending(selector)
            : _apply(query).ThenBy(selector));
    }

    /// <summary>
    /// Applies ordering inside persistence without exposing provider queries to repository callers.
    /// </summary>
    /// <param name="query">The repository-owned filtered query.</param>
    /// <returns>The ordered provider query, still unexecuted.</returns>
    internal IOrderedQueryable<TEntity> Apply(IQueryable<TEntity> query)
    {
        return _apply(query);
    }
}
