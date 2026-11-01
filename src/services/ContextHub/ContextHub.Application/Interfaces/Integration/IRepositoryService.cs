namespace ContextHub.Application.Interfaces.Integration;

/// <summary>
/// Keeps native source and persistence work behind application-owned contracts.
/// </summary>
public interface IRepositoryService
{
    /// <summary>
    /// Gets an administrative repository page.
    /// </summary>
    /// <param name="request">The validated list inputs.</param>
    /// <param name="cancellationToken">Cancels authoritative database reads.</param>
    /// <returns>The approved metadata page.</returns>
    Task<PageResponse<RepositoryResponseDto>> GetPageAsync(GetRepositoriesRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Gets repository metadata by its integration identifier.
    /// </summary>
    /// <param name="repositoryId">The existing integration identifier.</param>
    /// <param name="cancellationToken">Cancels the database read.</param>
    /// <returns>The approved metadata.</returns>
    Task<RepositoryResponseDto> GetAsync(Guid repositoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the approved source metadata without synchronizing it.
    /// </summary>
    /// <param name="request">The validated source settings.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The committed metadata.</returns>
    Task<RepositoryResponseDto> CreateAsync(CreateRepositoryRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Updates source settings without changing sync metadata.
    /// </summary>
    /// <param name="repositoryId">The existing integration identifier.</param>
    /// <param name="request">The approved editable settings and expected version.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>The committed metadata.</returns>
    Task<RepositoryResponseDto> UpdateAsync(Guid repositoryId, UpdateRepositoryRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// Inspects the stored reference and records only the bounded sync outcome.
    /// </summary>
    /// <param name="repositoryId">The existing enabled integration.</param>
    /// <param name="request">The expected metadata version.</param>
    /// <param name="cancellationToken">Cancels source reads and persistence.</param>
    /// <returns>The exact commit and inspected-file count.</returns>
    Task<RepositorySyncResponseDto> SyncAsync(Guid repositoryId, SyncRepositoryRequestDto request, CancellationToken cancellationToken);
}
