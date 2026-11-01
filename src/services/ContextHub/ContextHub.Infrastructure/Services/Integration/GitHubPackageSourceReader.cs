namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Resolves publication aliases to current approved sources before immutable source reads.
/// </summary>
/// <param name="authoringRepository">The specific source repository.</param>
/// <param name="gitHubSourceClient">The native bounded installation client.</param>
/// <param name="gitHubOptions">The deployment-owned logical-source allowlist.</param>
public sealed class GitHubPackageSourceReader(
    IAuthoringRepository authoringRepository,
    IGitHubSourceClient gitHubSourceClient,
    IOptions<GitHubOptions> gitHubOptions) : IPackageSourceReader
{
    /// <summary>
    /// Reads only from a currently enabled source already approved by the Owner.
    /// </summary>
    /// <param name="request">The exact publication source coordinates.</param>
    /// <param name="cancellationToken">Cancels authority and immutable source reads.</param>
    /// <returns>The two source documents from the requested commit.</returns>
    public async Task<PackageSourceModel> ReadAsync(
        PackageSourceRequestModel request,
        CancellationToken cancellationToken)
    {
        if (!gitHubOptions.Value.AuthoringRepositories.TryGetValue(request.AuthoringRepository, out var repositoryId))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        var repository = await authoringRepository.GetEntityAsync(repositoryId, cancellationToken);
        if (repository is not { IsEnabled: true })
        {
            throw new BusinessException(IntegrationConstants.NOT_FOUND, IntegrationConstants.MISSING_MESSAGE, BusinessErrorKind.NotFound);
        }

        // Aliases select stored sources; publication input never becomes an arbitrary outbound URL.
        return await gitHubSourceClient.ReadAsync(repository.Url, request.SourceCommitSha, request.SourcePath, cancellationToken);
    }
}
