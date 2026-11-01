namespace ContextHub.Infrastructure.Repositories.Catalog;

/// <summary>
/// Adds package-specific filters while reusing shared EF staging and Mapster projection.
/// </summary>
/// <param name="contextHubDbContext">The service's sole scoped context.</param>
/// <param name="logger">The correctly categorized repository metadata logger.</param>
public sealed class PackageRepository(
    ContextHubDbContext contextHubDbContext,
    ILogger<PackageRepository> logger)
    : GenericRepository<Package>(contextHubDbContext, logger), IPackageRepository
{
    /// <summary>
    /// Gets catalog metadata and computes latest versions with native Semver precedence.
    /// </summary>
    /// <param name="request">The validated list filters and sort.</param>
    /// <param name="cancellationToken">Cancels the count, page and version metadata queries.</param>
    /// <returns>The page without loading package bundles.</returns>
    public Task<PageResponse<PackageResponseDto>> GetPageAsync(
        PackageListRequestDto request,
        CancellationToken cancellationToken)
    {
        var order = request.Sort switch
        {
            "-packageId" => QueryOrder<Package>.By(value => value.Key, true),
            "displayName" => QueryOrder<Package>.By(value => value.Name).ThenBy(value => value.Key),
            "-updatedAt" => QueryOrder<Package>.By(value => value.UpdatedAt, true).ThenBy(value => value.Key),
            _ => QueryOrder<Package>.By(value => value.Key)
        };

        var search = new SearchOptions<Package>
        {
            SearchTerm = request.Query,
            TextFields = [value => value.Key, value => value.Name, value => value.Summary],
            FieldFilters =
            [
                request.IsActive is { } active ? value => value.IsActive == active : null,
                request.PackageType is { } type ? value => value.Type == type : null
            ]
        };

        return GetPageAsync<PackageResponseDto>(
            new PageRequest(request.Page, request.PageSize),
            order,
            search: search,
            cancellationToken: cancellationToken);
    }
}
