namespace ContextHub.Infrastructure.Repositories.Integration;

/// <summary>
/// Reuses generic staging and lookup while keeping native row-version failures specific.
/// </summary>
/// <param name="dbContext">The scoped service context.</param>
/// <param name="unitOfWork">The save boundary for that same context.</param>
/// <param name="logger">The enclosing repository metadata logger.</param>
public class ExternalConnectionRepository(
    ContextHubDbContext dbContext,
    IUnitOfWork unitOfWork,
    ILogger<ExternalConnectionRepository> logger)
    : GenericRepository<ExternalMcp>(dbContext, logger), IExternalConnectionRepository
{
    /// <summary>
    /// Lists current stored connections in an explicit stable order.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database query.</param>
    /// <returns>The current stored connections.</returns>
    public virtual async Task<IReadOnlyList<ExternalMcp>> GetConnectionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await GetListAsync<ExternalMcp>(
                value => value,
                predicate: value => value.Status == ExternalMcpStatus.Active,
                order: QueryOrder<ExternalMcp>.By(value => value.Name).ThenBy(value => value.Id),
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(
                IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE,
                exception);
        }
    }

    /// <summary>
    /// Gets the exact named current connection.
    /// </summary>
    /// <param name="name">The saved Owner-only label.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tracked connection or null.</returns>
    public virtual async Task<ExternalMcp?> GetByNameAsync(
        string name,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FirstOrDefaultAsync(
                predicate: value => value.Name == name,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(
                IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE,
                exception);
        }
    }

    /// <summary>
    /// Rechecks a routed connection against current database state.
    /// </summary>
    /// <param name="id">The protected routing identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The current connection or null.</returns>
    public virtual async Task<ExternalMcp?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CreateQuery(predicate: value => value.Id == id).AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(
                IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE,
                exception);
        }
    }

    /// <summary>
    /// Saves canonical metadata with provider-enforced optimistic concurrency.
    /// </summary>
    /// <param name="entity">The staged connection entity.</param>
    /// <param name="expectedVersion">The expected row version, or null for create.</param>
    /// <param name="cancellationToken">Cancels staging and save.</param>
    /// <returns>The committed entity.</returns>
    public virtual async Task<ExternalMcp> SaveAsync(
        ExternalMcp entity,
        uint? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is { } expected)
        {
            if (dbContext.Entry(entity).State == EntityState.Detached)
            {
                dbContext.Attach(entity);
                dbContext.Entry(entity).Property(value => value.Name).IsModified = true;
                dbContext.Entry(entity).Property(value => value.Config).IsModified = true;
            }
            dbContext.Entry(entity).Property(value => value.ConcurrencyVersion).OriginalValue = expected;
        }
        else
        {
            await AddAsync(entity, cancellationToken);
        }
        await SaveChangesAsync(entity, cancellationToken);
        return entity;
    }

    /// <summary>
    /// Deletes an exact row version; stale callers cannot delete newer configuration.
    /// </summary>
    /// <param name="entity">The current connection.</param>
    /// <param name="expectedVersion">The caller's expected row version.</param>
    /// <param name="cancellationToken">Cancels the delete.</param>
    /// <returns>The committed deletion.</returns>
    public virtual async Task DeleteAsync(
        ExternalMcp entity,
        uint expectedVersion,
        CancellationToken cancellationToken)
    {
        if (dbContext.Entry(entity).State == EntityState.Detached)
        {
            dbContext.Attach(entity);
        }
        dbContext.Entry(entity).Property(value => value.ConcurrencyVersion).OriginalValue = expectedVersion;
        // Retain configuration and creation audit; native xmin still arbitrates concurrent writes.
        entity.Status = ExternalMcpStatus.Deleted;
        await SaveChangesAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Classifies only this entity's confirmed uniqueness or row-version failure.
    /// </summary>
    /// <param name="entity">The entity owned by this mutation.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The completed save.</returns>
    private async Task SaveChangesAsync(
        ExternalMcp entity,
        CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, entity))
        {
            if (entity.Status == ExternalMcpStatus.Deleted)
            {
                // A competing delete owns its original audit; only an already committed deletion is idempotent.
                dbContext.Entry(entity).State = EntityState.Detached;
                var committed = await GetByIdAsync(entity.Id, cancellationToken);
                if (committed?.Status == ExternalMcpStatus.Deleted)
                {
                    return;
                }
            }
            logger.LogWarning(IntegrationLogConstants.EXTERNAL_CONFLICT);
            throw new BusinessException(
                IntegrationConstants.CONFLICT,
                IntegrationConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IntegrationInfrastructureConstants.EXTERNAL_NAME_CONSTRAINT })
        {
            logger.LogWarning(IntegrationLogConstants.EXTERNAL_DUPLICATE);
            throw new BusinessException(
                IntegrationConstants.CONFLICT,
                IntegrationConstants.CONFLICT_MESSAGE,
                BusinessErrorKind.Conflict);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(
                IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE,
                exception);
        }
    }
}
