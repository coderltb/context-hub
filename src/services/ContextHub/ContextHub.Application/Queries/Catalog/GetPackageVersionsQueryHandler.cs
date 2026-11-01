namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Coordinates GetPackageVersions after shared authorization and validation.
/// </summary>
/// <param name="catalogService">The catalog operation port; persistence entities remain behind it.</param>
public sealed class GetPackageVersionsQueryHandler(ICatalogService catalogService)
    : IRequestHandler<GetPackageVersionsQuery, ApiResponse<PageResponse<VersionSummaryResponseDto>>>
{
    /// <summary>
    /// Returns the approved catalog result in the shared HTTP envelope.
    /// </summary>
    /// <param name="request">The validated catalog operation.</param>
    /// <param name="cancellationToken">Cancels underlying source and database I/O.</param>
    /// <returns>The approved result without persistence entities.</returns>
    public async ValueTask<ApiResponse<PageResponse<VersionSummaryResponseDto>>> Handle(
        GetPackageVersionsQuery request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GetVersionsAsync(request.PackageId, request.Request, cancellationToken);

        return new(result);
    }
}
