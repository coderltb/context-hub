namespace ContextHub.Application.Interfaces.Catalog;

/// <summary>
/// Reads reviewed package source at an exact authoring commit without publishing it.
/// </summary>
public interface IPackageSourceReader
{
    /// <summary>
    /// Reads manifest.json and bundle.json from an allowlisted source directory.
    /// </summary>
    /// <param name="request">The exact package and authoring locator.</param>
    /// <param name="cancellationToken">Cancels source I/O.</param>
    /// <returns>The immutable source payload and verified commit.</returns>
    Task<PackageSourceModel> ReadAsync(PackageSourceRequestModel request, CancellationToken cancellationToken);
}
