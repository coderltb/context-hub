namespace ContextHub.Infrastructure.Interfaces.Integration;

/// <summary>
/// Reads bounded GitHub source through the service's installation identity.
/// </summary>
public interface IGitHubSourceClient
{
    /// <summary>
    /// Inspects supported files at the configured default reference.
    /// </summary>
    /// <param name="repositoryUrl">The persisted canonical GitHub URL.</param>
    /// <param name="reference">The persisted branch, tag or commit.</param>
    /// <param name="cancellationToken">Cancels network and content reads.</param>
    /// <returns>The exact commit and inspected-file count.</returns>
    Task<GitHubInspectionModel> InspectAsync(string repositoryUrl, string reference, CancellationToken cancellationToken);

    /// <summary>
    /// Reads immutable manifest and bundle files from one exact directory and commit.
    /// </summary>
    /// <param name="repositoryUrl">The persisted allowlisted GitHub repository URL.</param>
    /// <param name="commitSha">The exact forty-character commit SHA.</param>
    /// <param name="sourcePath">The bounded relative directory.</param>
    /// <param name="cancellationToken">Cancels network and content reads.</param>
    /// <returns>The source documents and exact verified commit.</returns>
    Task<PackageSourceModel> ReadAsync(string repositoryUrl, string commitSha, string sourcePath, CancellationToken cancellationToken);
}
