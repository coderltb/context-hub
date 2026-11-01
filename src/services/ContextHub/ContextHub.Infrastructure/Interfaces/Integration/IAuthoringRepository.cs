namespace ContextHub.Infrastructure.Interfaces.Integration;

/// <summary>
/// Keeps source-configuration entity queries inside persistence.
/// </summary>
public interface IAuthoringRepository
{
    /// <summary>
    /// Gets a provider-projected administrative page.
    /// </summary>
    /// <param name="request">The validated public query.</param>
    /// <param name="cancellationToken">Cancels both database reads.</param>
    /// <returns>The approved metadata page.</returns>
    Task<PageResponse<RepositoryResponseDto>> GetPageAsync(GetRepositoriesRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a tracked source entity for an owning Infrastructure operation.
    /// </summary>
    /// <param name="repositoryId">The exact integration identifier.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The current entity or null.</returns>
    Task<Repository?> GetEntityAsync(Guid repositoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new source configuration without saving.
    /// </summary>
    /// <param name="repository">The approved metadata.</param>
    /// <param name="cancellationToken">Cancels staging.</param>
    /// <returns>The tracked entity.</returns>
    Task<Repository> AddAsync(Repository repository, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves the staged source while preserving its original optimistic concurrency predicate.
    /// </summary>
    /// <param name="repository">The tracked entity owned by this persistence scope.</param>
    /// <param name="expectedVersion">The expected row version; null for a new staged entity.</param>
    /// <param name="cancellationToken">Cancels the save.</param>
    /// <returns>The committed metadata projection.</returns>
    Task<RepositoryResponseDto> SaveAsync(Repository repository, uint? expectedVersion, CancellationToken cancellationToken);
}
