namespace ContextHub.Core.Repositories;

/// <summary>
/// Implements the shared repository contract for one entity type.
/// </summary>
/// <typeparam name="TEntity">The EF Core entity type.</typeparam>
public class GenericRepository<TEntity> : IGenericRepository<TEntity>
    where TEntity : class
{
    /// <summary>
    /// References the service-owned context; this repository never creates a save boundary.
    /// </summary>
    protected readonly DbContext _dbContext;

    /// <summary>
    /// Provides EF tracking and query access for the repository's entity type.
    /// </summary>
    private readonly DbSet<TEntity> _entities;

    /// <summary>
    /// Records operation metadata without entity values, identifiers or query predicates.
    /// </summary>
    private readonly ILogger<GenericRepository<TEntity>> _logger;

    /// <summary>
    /// Creates a repository sharing the service's context and safe structured logger.
    /// </summary>
    /// <param name="dbContext">The service-owned EF context.</param>
    /// <param name="logger">The logger for operation metadata only.</param>
    public GenericRepository(
        DbContext dbContext,
        ILogger<GenericRepository<TEntity>> logger)
    {
        _dbContext = dbContext;
        _entities = dbContext.Set<TEntity>();
        _logger = logger;
    }

    /// <summary>
    /// Gets one mapped result using the existing provider-side selector pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type configured in the host's Mapster profiles.</typeparam>
    /// <param name="order">Optional typed ordering; null does not introduce a sort.</param>
    /// <param name="predicate">Optional scope or filter combined with search using AND.</param>
    /// <param name="search">Optional text and field filters; null does not introduce search.</param>
    /// <param name="cancellationToken">Stops the existing database read pipeline.</param>
    /// <returns>The mapped result, or default(TResult) when no row matches.</returns>
    public virtual Task<TResult?> FirstOrDefaultAsync<TResult>(
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to the virtual selector overload so query scope, logging and transaction ownership stay unchanged.
        var selector = CreateProjection<TResult>();

        return FirstOrDefaultAsync(selector, predicate, order, search, cancellationToken);
    }

    /// <summary>
    /// Gets one match with native tracking and optional ordering, without requiring uniqueness.
    /// </summary>
    /// <remarks>
    /// Queries persisted rows and retains EF identity resolution; pending tracked edits are not overwritten by materialization.
    /// </remarks>
    /// <param name="predicate">An optional entity predicate; null reads without a filter.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>A matching entity, or null when none exists.</returns>
    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(FirstOrDefaultAsync), typeof(TEntity).Name);

        // Ordering is optional; an unordered first match does not promise uniqueness or a stable row.
        var query = CreateQuery(predicate, search, order);

        var result = await query.FirstOrDefaultAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.QUERY_COMPLETED,
            nameof(FirstOrDefaultAsync),
            typeof(TEntity).Name,
            result is null ? 0 : 1);

        return result;
    }

    /// <summary>
    /// Projects one match in the database with optional ordering and without requiring uniqueness.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="selector">The required database projection.</param>
    /// <param name="predicate">An optional filter; null reads without filtering.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>The selected value or the native default(TResult) when no row matches.</returns>
    [OverloadResolutionPriority(-1)]
    public virtual async Task<TResult?> FirstOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(FirstOrDefaultAsync), typeof(TEntity).Name);

        var query = CreateQuery(predicate, search, order);

        // A value-type default does not prove whether a row existed; callers needing that distinction project a nullable value.
        var result = await query.AsNoTracking().Select(selector).FirstOrDefaultAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.PROJECTION_COMPLETED,
            nameof(FirstOrDefaultAsync),
            typeof(TEntity).Name);

        return result;
    }

    /// <summary>
    /// Returns at most one matching entity without hiding native cardinality or provider failures.
    /// </summary>
    /// <param name="predicate">An optional filter; null reads without a filter.</param>
    /// <param name="search">Optional typed search combined with scope using AND.</param>
    /// <param name="cancellationToken">Stops the database read.</param>
    /// <returns>The matching entity or null; multiple matches cause a native cardinality exception.</returns>
    public virtual async Task<TEntity?> SingleOrDefaultAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(SingleOrDefaultAsync), typeof(TEntity).Name);

        var query = CreateQuery(predicate, search);

        var result = await query.SingleOrDefaultAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.QUERY_COMPLETED,
            nameof(SingleOrDefaultAsync),
            typeof(TEntity).Name,
            result is null ? 0 : 1);

        return result;
    }

    /// <summary>
    /// Projects at most one row with an explicit selector or the existing global Mapster projection.
    /// </summary>
    /// <typeparam name="TResult">The DTO or scalar result type.</typeparam>
    /// <param name="selector">Optional provider-side selector; null uses the global mapping projection.</param>
    /// <param name="predicate">An optional filter; null reads without a filter.</param>
    /// <param name="search">Optional typed search combined with scope using AND.</param>
    /// <param name="cancellationToken">Stops the database read.</param>
    /// <returns>The projected value or default(TResult); multiple matches cause a native cardinality exception.</returns>
    [OverloadResolutionPriority(-1)]
    public virtual async Task<TResult?> SingleOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>>? selector = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(SingleOrDefaultAsync), typeof(TEntity).Name);

        // Build a provider-side projection, never a fallback that loads full entities and maps in memory.
        selector ??= CreateProjection<TResult>();
        var query = CreateQuery(predicate, search);

        var result = await query.AsNoTracking().Select(selector).SingleOrDefaultAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.PROJECTION_COMPLETED,
            nameof(SingleOrDefaultAsync),
            typeof(TEntity).Name);

        return result;
    }

    /// <summary>
    /// Lists mapped results using the existing provider-side selector pipeline.
    /// </summary>
    /// <typeparam name="TResult">The result type configured in the host's Mapster profiles.</typeparam>
    /// <param name="order">Optional typed ordering; null does not introduce a sort.</param>
    /// <param name="predicate">Optional scope or filter combined with search using AND.</param>
    /// <param name="search">Optional text and field filters; null does not introduce search.</param>
    /// <param name="cancellationToken">Stops the existing database read pipeline.</param>
    /// <returns>The materialized mapped results without tracked entities.</returns>
    public virtual Task<IReadOnlyList<TResult>> GetListAsync<TResult>(
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to the virtual selector overload so query scope, logging and transaction ownership stay unchanged.
        var selector = CreateProjection<TResult>();

        return GetListAsync(selector, predicate, order, search, cancellationToken);
    }

    /// <summary>
    /// Lists matching entities using native EF tracking for caller-owned edits.
    /// </summary>
    /// <param name="predicate">An optional entity predicate.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>A read-only list of matching entities.</returns>
    public virtual async Task<IReadOnlyList<TEntity>> GetListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(GetListAsync), typeof(TEntity).Name);

        // Keep native entity tracking; callers choose when to save edits through the shared Unit of Work.
        var query = CreateQuery(predicate, search, order);

        var result = await query.ToListAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.QUERY_COMPLETED,
            nameof(GetListAsync),
            typeof(TEntity).Name,
            result.Count);

        return result;
    }

    /// <summary>
    /// Materializes a provider-translated projection without tracking any projected entities.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="selector">The required database projection.</param>
    /// <param name="predicate">An optional filter; null reads the entire set.</param>
    /// <param name="order">Optional typed ordering; null preserves unspecified ordering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>The materialized selected values.</returns>
    [OverloadResolutionPriority(-1)]
    public virtual async Task<IReadOnlyList<TResult>> GetListAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? predicate = null,
        QueryOrder<TEntity>? order = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(GetListAsync), typeof(TEntity).Name);

        // Select validates its required expression natively; a missing optional filter remains a no-op.
        var query = CreateQuery(predicate, search, order);

        var result = await query.AsNoTracking().Select(selector).ToListAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.QUERY_COMPLETED,
            nameof(GetListAsync),
            typeof(TEntity).Name,
            result.Count);

        return result;
    }

    /// <summary>
    /// Determines whether any non-tracked entity exists, optionally filtered by a typed predicate.
    /// </summary>
    /// <param name="predicate">An optional entity predicate; null checks the entire entity set.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns><see langword="true"/> when a matching entity exists.</returns>
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(AnyAsync), typeof(TEntity).Name);

        var query = CreateQuery(predicate, search);
        var exists = await query.AnyAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.QUERY_COMPLETED,
            nameof(AnyAsync),
            typeof(TEntity).Name,
            exists ? 1 : 0);

        return exists;
    }

    /// <summary>
    /// Counts persisted rows without loading their columns into entities.
    /// </summary>
    /// <param name="predicate">An optional filter; null counts the entire set.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops the database operation.</param>
    /// <returns>The 64-bit number of matching rows.</returns>
    public virtual async Task<long> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(CountAsync), typeof(TEntity).Name);

        // Count stays provider-side and shares the exact filtering rules of materialized reads.
        var query = CreateQuery(predicate, search);
        var count = await query.LongCountAsync(cancellationToken);

        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_COMPLETED, nameof(CountAsync), typeof(TEntity).Name, count);

        return count;
    }

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
    public virtual Task<PageResponse<TResult>> GetPageAsync<TResult>(
        PageRequest page,
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to the virtual selector overload so query scope, logging and transaction ownership stay unchanged.
        var selector = CreateProjection<TResult>();

        return GetPageAsync(selector, page, order, predicate, search, cancellationToken);
    }

    /// <summary>
    /// Gets a tracked entity page without creating a save or transaction boundary.
    /// </summary>
    /// <param name="page">The required bounded page request.</param>
    /// <param name="order">Optional ordering; null leaves row order unspecified.</param>
    /// <param name="predicate">An optional filter; null reads without filtering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops both database reads.</param>
    /// <returns>The materialized entities and the separately observed matching count.</returns>
    public virtual Task<PageResponse<TEntity>> GetPageAsync(
        PageRequest page,
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        return GetPageAsync(CreateQuery(predicate, search, order), entity => entity, page, cancellationToken);
    }

    /// <summary>
    /// Counts and reads a provider-projected page sequentially without creating a save or transaction boundary.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="selector">The required database projection.</param>
    /// <param name="page">The required page request whose offset must fit native Skip.</param>
    /// <param name="order">Optional ordering; stable page boundaries are the caller's responsibility.</param>
    /// <param name="predicate">An optional filter; null reads without filtering.</param>
    /// <param name="search">Optional typed text and field conditions combined with the predicate using AND.</param>
    /// <param name="cancellationToken">Stops both sequential reads.</param>
    /// <returns>The selected items and the count observed before reading them.</returns>
    public virtual Task<PageResponse<TResult>> GetPageAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        PageRequest page,
        QueryOrder<TEntity>? order = null,
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        CancellationToken cancellationToken = default)
    {
        return GetPageAsync(CreateQuery(predicate, search, order).AsNoTracking(), selector, page, cancellationToken);
    }

    /// <summary>
    /// Stages a new entity without committing the context.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">Stops asynchronous value generation.</param>
    /// <returns>The tracked entity.</returns>
    public virtual async Task<TEntity> AddAsync(
        TEntity entity,
        CancellationToken cancellationToken = default)
    {
        // Staging must not save: the owning use case commits its entire change set through Unit of Work.
        var entry = await _entities.AddAsync(entity, cancellationToken);
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.CHANGE_STAGED, nameof(AddAsync), typeof(TEntity).Name);

        return entry.Entity;
    }

    /// <summary>
    /// Stages multiple new entities without committing the context.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">Stops asynchronous value generation.</param>
    /// <returns>A task that completes after entities are staged; no database commit is performed.</returns>
    public virtual async Task AddRangeAsync(
        IEnumerable<TEntity> entities,
        CancellationToken cancellationToken = default)
    {
        // Stage the collection without introducing a second transaction or save boundary.
        await _entities.AddRangeAsync(entities, cancellationToken);
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.CHANGE_STAGED, nameof(AddRangeAsync), typeof(TEntity).Name);
    }

    /// <summary>
    /// Marks an entity as modified without committing the context.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    public virtual void Update(TEntity entity)
    {
        _entities.Update(entity);
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.CHANGE_STAGED, nameof(Update), typeof(TEntity).Name);
    }

    /// <summary>
    /// Stages native EF range updates while leaving the commit boundary with the caller.
    /// </summary>
    /// <param name="entities">The entities to mark as modified.</param>
    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        _entities.UpdateRange(entities);

        _logger.LogDebug(CoreLogConstants.RepositoryLogs.CHANGE_STAGED, nameof(UpdateRange), typeof(TEntity).Name);
    }

    /// <summary>
    /// Marks an entity for deletion without committing the context.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    public virtual void Remove(TEntity entity)
    {
        _entities.Remove(entity);
        _logger.LogDebug(CoreLogConstants.RepositoryLogs.CHANGE_STAGED, nameof(Remove), typeof(TEntity).Name);
    }

    /// <summary>
    /// Stages native EF range removals without saving or starting a transaction.
    /// </summary>
    /// <param name="entities">The entities to mark for deletion.</param>
    public virtual void RemoveRange(IEnumerable<TEntity> entities)
    {
        _entities.RemoveRange(entities);

        _logger.LogDebug(CoreLogConstants.RepositoryLogs.CHANGE_STAGED, nameof(RemoveRange), typeof(TEntity).Name);
    }

    /// <summary>
    /// Creates a provider projection from the global mappings configured once at startup.
    /// </summary>
    /// <typeparam name="TResult">The mapped result type.</typeparam>
    /// <returns>The expression passed to EF without compiling or materializing entities in memory.</returns>
    protected virtual Expression<Func<TEntity, TResult>> CreateProjection<TResult>()
    {
        // Project only the requested mapping; translation failures propagate without loading full entities.
        return (Expression<Func<TEntity, TResult>>)TypeAdapterConfig.GlobalSettings.CreateMapExpression(new TypeTuple(typeof(TEntity), typeof(TResult)), MapType.Projection);
    }

    /// <summary>
    /// Builds conditional queries with native EF tracking, mandatory scope, optional search and typed ordering.
    /// </summary>
    /// <remarks>
    /// Every public read uses this pipeline, including predicates on keys; derived repositories can enforce scope here.
    /// </remarks>
    /// <param name="predicate">The caller's scope or other mandatory predicate, when supplied.</param>
    /// <param name="search">Optional explicitly selected text fields and typed field conditions.</param>
    /// <param name="order">Optional multi-key ordering; null does not introduce an implicit sort.</param>
    /// <returns>The unexecuted persistence query, available only to this repository and its subclasses.</returns>
    protected virtual IQueryable<TEntity> CreateQuery(
        Expression<Func<TEntity, bool>>? predicate = null,
        SearchOptions<TEntity>? search = null,
        QueryOrder<TEntity>? order = null)
    {
        if (search is not null)
        {
            // Copy caller-owned collections before asynchronous I/O; expression trees themselves are immutable.
            var textFields = search.TextFields?.ToArray();
            var collectionTextFields = search.CollectionTextFields?.ToArray();
            var fieldFilters = search.FieldFilters?.ToArray();

            // Never OR search with the scope: only the selected text fields form an OR group.
            predicate = predicate
                .AndAlso(ExpressionExtension.SearchText(search.SearchTerm, textFields, collectionTextFields))
                .AndAlso(ExpressionExtension.SearchFields(fieldFilters));
        }

        // Preserve the DbContext's native tracking policy; projection callers explicitly opt out.
        var query = _entities.AsQueryable();

        if (predicate is not null)
        {
            query = query.Where(predicate);
        }

        return order is null ? query : order.Apply(query);
    }

    /// <summary>
    /// Executes count and page queries sequentially while retaining the prepared query's tracking policy.
    /// </summary>
    /// <typeparam name="TResult">The selected result type.</typeparam>
    /// <param name="query">The scoped and optionally ordered query, prepared once before either read.</param>
    /// <param name="selector">The required provider-side projection or entity identity selector.</param>
    /// <param name="page">The bounded page request.</param>
    /// <param name="cancellationToken">Cancellation passed to both sequential database reads.</param>
    /// <returns>The selected items and the independently observed count.</returns>
    private async Task<PageResponse<TResult>> GetPageAsync<TResult>(
        IQueryable<TEntity> query,
        Expression<Func<TEntity, TResult>> selector,
        PageRequest page,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(page);

        // Required inputs and the long-to-int boundary must fail before the count query performs I/O.
        if (page.Offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(page), CoreErrorConstants.RepositoryErrors.PAGE_OFFSET_TOO_LARGE);
        }

        _logger.LogDebug(CoreLogConstants.RepositoryLogs.QUERY_STARTED, nameof(GetPageAsync), typeof(TEntity).Name);

        // Count and items reuse one query sequentially; separate SQL statements do not promise a snapshot.
        var total = await query.LongCountAsync(cancellationToken);

        // No implicit key or sort is invented: without an order, page membership is not guaranteed to be stable.
        var items = await query.Skip((int)page.Offset).Take(page.PageSize)
            .Select(selector).ToListAsync(cancellationToken);

        _logger.LogDebug(
            CoreLogConstants.RepositoryLogs.QUERY_COMPLETED,
            nameof(GetPageAsync),
            typeof(TEntity).Name,
            items.Count);

        return new PageResponse<TResult>(items, page, total);
    }
}
