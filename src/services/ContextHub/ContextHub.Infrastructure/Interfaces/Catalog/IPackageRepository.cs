namespace ContextHub.Infrastructure.Interfaces.Catalog;

/// <summary>
/// Keeps catalog identity queries and staging inside persistence.
/// </summary>
public interface IPackageRepository : IGenericRepository<Package>
{
    /// <summary>
    /// Gets an approved package page through shared typed filtering and projection.
    /// </summary>
    /// <param name="request">The bounded administrative list query.</param>
    /// <param name="cancellationToken">Cancels database I/O.</param>
    /// <returns>The package page with its latest stable published releases.</returns>
    Task<PageResponse<PackageResponseDto>> GetPageAsync(PackageListRequestDto request, CancellationToken cancellationToken);
}
