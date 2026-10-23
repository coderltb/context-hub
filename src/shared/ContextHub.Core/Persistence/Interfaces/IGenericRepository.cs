namespace ContextHub.Core.Persistence.Interfaces;

/// <summary>
/// Provides the small set of persistence operations shared by service-owned repositories.
/// </summary>
/// <remarks>
/// Entity reads retain native EF tracking; DTO and selector reads explicitly disable tracking. Writes only stage changes. The caller owns
/// the commit boundary through <c>IUnitOfWork</c>. Capability-specific queries belong in a specific repository.
/// </remarks>
/// <typeparam name="TEntity">The EF Core entity type.</typeparam>
public interface IGenericRepository<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Gets one mapped result using the existing provider-side selector pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type configured in the host's Mapster profiles.</typeparam>
    /// <param name="order">Optional typed ordering; null does not introduce a sort.</param>
    /// <param name="predicate">Optional scope or filter combined with search using AND.</param>
    /// <param name="search">Optional text and field filters; null does not introduce search.</param>
    /// <param name="cancellationToken">Stops the existing database read pipeline.</param>
    /// <returns>The mapped result, or default(TResult) when no row matches.</returns>
    Task<TResult?> FirstOrDefaultAsync<TResult>(
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one matching entity with native tracking and optional ordering, without requiring uniqueness.
    /// </summary>
    /// <param name="predicate">An optional entity predicate; null reads without a filter.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>A matching entity, or null; without an order the first matching row is unspecified.</returns>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns at most one matching entity, preserving EF's exception when multiple rows match.
    /// </summary>
    /// <param name="predicate">An optional filter; null reads without a filter.</param>
    /// <param name="search">Optional typed search combined with scope using AND.</param>
    /// <param name="cancellationToken">Stops the database read.</param>
    /// <returns>The matching entity or null; multiple matches cause a native cardinality exception.</returns>
    Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects at most one row using a supplied selector or the configured global Mapster profile.
    /// </summary>
    /// <typeparam name="TResult">The DTO or scalar result type.</typeparam>
    /// <param name="selector">Optional provider-side selector; null uses the global mapping projection.</param>
    /// <param name="predicate">An optional filter; null reads without a filter.</param>
    /// <param name="search">Optional typed search combined with scope using AND.</param>
    /// <param name="cancellationToken">Stops the database read.</param>
    /// <returns>The projected value or default(TResult); multiple matches cause a native cardinality exception.</returns>
    [OverloadResolutionPriority(-1)]
    Task<TResult?> SingleOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>>? selector = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects one matching row with optional ordering and without requiring uniqueness.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="selector">The required provider-translatable projection.</param>
    /// <param name="predicate">An optional filter; null reads without filtering.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>The selected value, or default(TResult) when no row matches, including zero for int.</returns>
    /// <remarks>
    /// Use FirstOrDefaultAsync&lt;bool&gt; explicitly for boolean projections, preserving the existing predicate overload.
    /// </remarks>
    [OverloadResolutionPriority(-1)]
    Task<TResult?> FirstOrDefaultAsync<TResult>(Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null, QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists mapped results using the existing provider-side selector pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type configured in the host's Mapster profiles.</typeparam>
    /// <param name="order">Optional typed ordering; null does not introduce a sort.</param>
    /// <param name="predicate">Optional scope or filter combined with search using AND.</param>
    /// <param name="search">Optional text and field filters; null does not introduce search.</param>
    /// <param name="cancellationToken">Stops the existing database read pipeline.</param>
    /// <returns>The materialized mapped results without tracked entities.</returns>
    Task<IReadOnlyList<TResult>> GetListAsync<TResult>(
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists matching entities using native EF tracking for caller-owned edits.
    /// </summary>
    /// <param name="predicate">An optional entity predicate.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>A read-only list of matching entities.</returns>
    Task<IReadOnlyList<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects matching rows in the database and materializes the selected values without tracking.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="selector">The required provider-translatable projection.</param>
    /// <param name="predicate">An optional filter; null reads the entire set.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>A materialized read-only list of selected values.</returns>
    /// <remarks>
    /// Use GetListAsync&lt;bool&gt; explicitly for boolean projections; a single bool lambda remains an entity predicate.
    /// </remarks>
    [OverloadResolutionPriority(-1)]
    Task<IReadOnlyList<TResult>> GetListAsync<TResult>(Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null, QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Determines whether any non-tracked entity exists, optionally filtered by a typed predicate.
    /// </summary>
    /// <param name="predicate">An optional entity predicate; null checks the entire entity set.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns><see langword="true"/> when a matching entity exists.</returns>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts persisted rows without materializing entities.
    /// </summary>
    /// <param name="predicate">An optional filter; null counts the entire set.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>The number of matching rows as a 64-bit count.</returns>
    Task<long> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a mapped page using the existing sequential count and selector pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type configured in the host's Mapster profiles.</typeparam>
    /// <param name="page">The required bounded page request.</param>
    /// <param name="order">Optional typed ordering; null does not introduce a sort.</param>
    /// <param name="predicate">Optional scope or filter combined with search using AND.</param>
    /// <param name="search">Optional text and field filters; null does not introduce search.</param>
    /// <param name="cancellationToken">Stops the existing database read pipeline.</param>
    /// <returns>The mapped items and the independently observed matching count.</returns>
    Task<PageResponse<TResult>> GetPageAsync<TResult>(
        PageRequest page,
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity page with native tracking, optional ordering and an independent matching count.
    /// </summary>
    /// <param name="page">The required bounded page request; its offset must fit native Skip.</param>
    /// <param name="order">Optional order; omit it for unspecified ordering or include a unique final key for stable paging.</param>
    /// <param name="predicate">An optional filter; null reads without filtering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops both sequential database reads.</param>
    /// <returns>A materialized page; concurrent writes may make its count differ from its items.</returns>
    Task<PageResponse<TEntity>> GetPageAsync(PageRequest page, QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null, SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Projects a page in the database without loading unused columns or opening a read transaction.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="selector">The required provider-translatable projection.</param>
    /// <param name="page">The required bounded page request; its offset must fit native Skip.</param>
    /// <param name="order">Optional ordering; stable page boundaries require caller-supplied unique tie-breakers.</param>
    /// <param name="predicate">An optional filter; null reads without filtering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops both sequential database reads.</param>
    /// <returns>The projected page and the count observed before its items query.</returns>
    Task<PageResponse<TResult>> GetPageAsync<TResult>(Expression<Func<TEntity, TResult>> selector,
        PageRequest page, QueryOrder<TEntity>? order = null, Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages a new entity in the current EF Core change tracker.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Stops asynchronous value generation.</param>
    /// <returns>The tracked entity.</returns>
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stages multiple new entities in the current EF Core change tracker.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">Stops asynchronous value generation.</param>
    /// <returns>A task that completes after entities are staged; no database commit is performed.</returns>
    Task AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an entity as modified without committing it.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(TEntity entity);

    /// <summary>
    /// Marks multiple entities as modified without saving them.
    /// </summary>
    /// <param name="entities">The entities whose updates are staged.</param>
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Marks an entity for deletion without committing it.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    void Remove(TEntity entity);

    /// <summary>
    /// Marks multiple entities for deletion without saving them.
    /// </summary>
    /// <param name="entities">The entities whose removals are staged.</param>
    void RemoveRange(IEnumerable<TEntity> entities);
}
