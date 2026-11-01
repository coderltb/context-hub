namespace ContextHub.Infrastructure.Repositories.Integration;

/// <summary>
/// Reuses generic query composition and staging for repository metadata.
/// </summary>
/// <param name="dbContext">The service-owned scoped context.</param>
/// <param name="unitOfWork">The shared save boundary for this same context.</param>
/// <param name="logger">The enclosing repository metadata logger.</param>
public class AuthoringRepository(
    ContextHubDbContext dbContext,
    IUnitOfWork unitOfWork,
    ILogger<AuthoringRepository> logger)
    : GenericRepository<Repository>(dbContext, logger), IAuthoringRepository
{
    /// <summary>
    /// Gets only approved metadata through the existing mapped paging pipeline.
    /// </summary>
    /// <param name="request">The bounded query DTO.</param>
    /// <param name="cancellationToken">Cancels count and items queries.</param>
    /// <returns>The provider-projected page.</returns>
    public virtual async Task<PageResponse<RepositoryResponseDto>> GetPageAsync(
        GetRepositoriesRequestDto request,
        CancellationToken cancellationToken)
    {
        request.Sort.TryParseRepositorySort(out var clauses);
        QueryOrder<Repository>? order = null;
        foreach (var clause in clauses)
        {
            order = clause.Field switch
            {
                "repositoryUrl" => order is null
                    ? QueryOrder<Repository>.By(value => value.Url, clause.Descending)
                    : order.ThenBy(value => value.Url, clause.Descending),
                "lastSyncedAt" => order is null
                    ? QueryOrder<Repository>.By(value => value.LastSyncedAt, clause.Descending)
                    : order.ThenBy(value => value.LastSyncedAt, clause.Descending),
                _ => order
            };
        }

        // Add the technical key only when the caller chose an ordering.
        order = order?.ThenBy(value => value.Id);
        Expression<Func<Repository, bool>>? syncStatusFilter = request.LastSyncStatus switch
        {
            "never" => value => value.LastSyncStatus == null,
            { } status => value => value.LastSyncStatus == status,
            _ => null
        };
        var search = new SearchOptions<Repository>
        {
            SearchTerm = request.Query,
            TextFields = [value => value.Url],
            FieldFilters =
            [
                request.IsEnabled is { } enabled ? value => value.IsEnabled == enabled : null,
                syncStatusFilter
            ]
        };

        try
        {
            return await GetPageAsync<RepositoryResponseDto>(
                new PageRequest(request.Page, request.PageSize),
                order,
                search: search,
                cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Gets a tracked entity so the service can preserve creation metadata.
    /// </summary>
    /// <param name="repositoryId">The exact source integration.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The tracked entity or null.</returns>
    public virtual async Task<Repository?> GetEntityAsync(
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FirstOrDefaultAsync(predicate: value => value.Id == repositoryId, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE, exception);
        }
    }

    /// <summary>
    /// Saves only this source mutation and classifies its confirmed uniqueness/concurrency failures.
    /// </summary>
    /// <param name="repository">The tracked source entity.</param>
    /// <param name="expectedVersion">The expected database row version, or null for create.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The committed approved metadata.</returns>
    public virtual async Task<RepositoryResponseDto> SaveAsync(
        Repository repository,
        uint? expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is { } version)
        {
            dbContext.Entry(repository).Property(value => value.ConcurrencyVersion).OriginalValue = version;
        }
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, repository))
        {
            logger.LogWarning(IntegrationLogConstants.REPOSITORY_CONFLICT);
            throw new BusinessException(IntegrationConstants.CONFLICT, IntegrationConstants.CONFLICT_MESSAGE, BusinessErrorKind.Conflict);
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: IntegrationInfrastructureConstants.REPOSITORY_URL_CONSTRAINT })
        {
            logger.LogWarning(IntegrationLogConstants.REPOSITORY_DUPLICATE);
            throw new BusinessException(IntegrationConstants.CONFLICT, IntegrationConstants.CONFLICT_MESSAGE, BusinessErrorKind.Conflict);
        }
        catch (Exception exception) when (DatabaseExceptionHelper.IsUnavailable(exception, cancellationToken))
        {
            throw new DependencyUnavailableException(IntegrationInfrastructureConstants.DATABASE_UNAVAILABLE, exception);
        }
        return repository.Adapt<RepositoryResponseDto>();
    }
}
