namespace ContextHub.Application.Queries.Catalog;

/// <summary>
/// Coordinates GetPackageVersion after shared authorization and validation.
/// </summary>
/// <param name="catalogService">The catalog operation port; persistence entities remain behind it.</param>
public sealed class GetPackageVersionQueryHandler(ICatalogService catalogService)
    : IRequestHandler<GetPackageVersionQuery, ApiResponse<VersionResponseDto>>
{
    /// <summary>
    /// Returns the approved catalog result in the shared HTTP envelope.
    /// </summary>
    /// <param name="request">The validated catalog operation.</param>
    /// <param name="cancellationToken">Cancels underlying source and database I/O.</param>
    /// <returns>The approved result without persistence entities.</returns>
    public async ValueTask<ApiResponse<VersionResponseDto>> Handle(
        GetPackageVersionQuery request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.GetVersionAsync(request.PackageId, request.Version, cancellationToken);

        return new(result);
    }
}
