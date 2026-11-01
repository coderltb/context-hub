namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Coordinates explicit source IO while persistence stays in the specific repository.
/// </summary>
/// <param name="authoringRepository">The specific repository sharing the service context.</param>
/// <param name="gitHubSourceClient">The native bounded GitHub source client.</param>
/// <param name="logger">The metadata-only source outcome logger.</param>
public sealed class RepositoryService(
    IAuthoringRepository authoringRepository,
    IGitHubSourceClient gitHubSourceClient,
    ILogger<RepositoryService> logger) : IRepositoryService
{
    /// <summary>
    /// Gets a provider-projected metadata page.
    /// </summary>
    /// <param name="request">The validated list query.</param>
    /// <param name="cancellationToken">Cancels count and item reads.</param>
    /// <returns>The approved metadata page.</returns>
    public Task<PageResponse<RepositoryResponseDto>> GetPageAsync(GetRepositoriesRequestDto request, CancellationToken cancellationToken) =>
        authoringRepository.GetPageAsync(request, cancellationToken);

    /// <summary>
    /// Gets approved source metadata without contacting GitHub.
    /// </summary>
    /// <param name="repositoryId">The persisted integration identifier.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The approved repository metadata.</returns>
    public async Task<RepositoryResponseDto> GetAsync(
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var repository = await authoringRepository.GetEntityAsync(repositoryId, cancellationToken)
            ?? throw new BusinessException(IntegrationConstants.NOT_FOUND, IntegrationConstants.MISSING_MESSAGE, BusinessErrorKind.NotFound);
        return repository.Adapt<RepositoryResponseDto>();
    }

    /// <summary>
    /// Creates source configuration without fetching or publishing any package.
    /// </summary>
    /// <param name="request">The validated editable settings.</param>
    /// <param name="cancellationToken">Cancels staging and persistence.</param>
    /// <returns>The committed source metadata.</returns>
    public async Task<RepositoryResponseDto> CreateAsync(
        CreateRepositoryRequestDto request,
        CancellationToken cancellationToken)
    {
        var repository = request.Adapt<Repository>();
        repository.Id = Guid.CreateVersion7();
        repository.Url = GitHubSourceHelper.CanonicalRepositoryUrl(request.RepositoryUrl);
        await authoringRepository.AddAsync(repository, cancellationToken);
        return await authoringRepository.SaveAsync(repository, null, cancellationToken);
    }

    /// <summary>
    /// Changes only editable settings, retaining the previous sync outcome.
    /// </summary>
    /// <param name="repositoryId">The current source identifier.</param>
    /// <param name="request">The approved replacement settings and row version.</param>
    /// <param name="cancellationToken">Cancels the read and save.</param>
    /// <returns>The committed metadata.</returns>
    public async Task<RepositoryResponseDto> UpdateAsync(
        Guid repositoryId,
        UpdateRepositoryRequestDto request,
        CancellationToken cancellationToken)
    {
        var repository = await authoringRepository.GetEntityAsync(repositoryId, cancellationToken)
            ?? throw new BusinessException(IntegrationConstants.NOT_FOUND, IntegrationConstants.MISSING_MESSAGE, BusinessErrorKind.NotFound);
        var expected = uint.Parse(request.ConcurrencyToken, CultureInfo.InvariantCulture);
        var canonicalUrl = GitHubSourceHelper.CanonicalRepositoryUrl(request.RepositoryUrl);
        request.Adapt(repository);
        repository.Url = canonicalUrl;
        return await authoringRepository.SaveAsync(repository, expected, cancellationToken);
    }

    /// <summary>
    /// Inspects the stored reference before atomically saving only the resulting metadata.
    /// </summary>
    /// <param name="repositoryId">The enabled source identifier.</param>
    /// <param name="request">The expected metadata version.</param>
    /// <param name="cancellationToken">Cancels source IO and persistence.</param>
    /// <returns>The exact inspected commit and metadata.</returns>
    public async Task<RepositorySyncResponseDto> SyncAsync(
        Guid repositoryId,
        SyncRepositoryRequestDto request,
        CancellationToken cancellationToken)
    {
        var repository = await authoringRepository.GetEntityAsync(repositoryId, cancellationToken)
            ?? throw new BusinessException(IntegrationConstants.NOT_FOUND, IntegrationConstants.MISSING_MESSAGE, BusinessErrorKind.NotFound);
        var expected = uint.Parse(request.ConcurrencyToken, CultureInfo.InvariantCulture);
        if (!repository.IsEnabled || repository.ConcurrencyVersion != expected)
        {
            throw new BusinessException(IntegrationConstants.CONFLICT, IntegrationConstants.CONFLICT_MESSAGE, BusinessErrorKind.Conflict);
        }

        GitHubInspectionModel inspected;
        try
        {
            // No database transaction spans network IO; xmin still detects configuration changes made during inspection.
            inspected = await gitHubSourceClient.InspectAsync(repository.Url, repository.DefaultRef, cancellationToken);
        }
        catch (DependencyUnavailableException)
        {
            repository.LastSyncStatus = IntegrationInfrastructureConstants.SYNC_FAILED_STATUS;
            repository.LastSyncedAt = DateTimeOffset.UtcNow;
            repository.LastErrorMessage = IntegrationInfrastructureConstants.SOURCE_SYNC_ERROR;
            await authoringRepository.SaveAsync(repository, expected, cancellationToken);
            logger.LogWarning(IntegrationLogConstants.SOURCE_SYNC_FAILED, repository.Id);
            throw;
        }
        repository.LastSyncedCommitSha = inspected.CommitSha;
        repository.LastSyncStatus = IntegrationInfrastructureConstants.SYNC_SUCCESS_STATUS;
        repository.LastSyncedAt = DateTimeOffset.UtcNow;
        repository.LastErrorMessage = null;
        var response = await authoringRepository.SaveAsync(repository, expected, cancellationToken);
        return new() {
            Repository = response,
            ResolvedCommitSha = inspected.CommitSha,
            FilesInspected = inspected.FilesInspected
        };
    }
}
