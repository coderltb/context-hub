namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Coordinates DeprecatePackageVersion after shared authorization and validation.
/// </summary>
/// <param name="catalogService">The catalog operation port; persistence entities remain behind it.</param>
public sealed class DeprecatePackageVersionCommandHandler(ICatalogService catalogService)
    : IRequestHandler<DeprecatePackageVersionCommand, ApiResponse<VersionResponseDto>>
{
    /// <summary>
    /// Returns the approved catalog result in the shared HTTP envelope.
    /// </summary>
    /// <param name="request">The validated catalog operation.</param>
    /// <param name="cancellationToken">Cancels underlying source and database I/O.</param>
    /// <returns>The approved result without persistence entities.</returns>
    public async ValueTask<ApiResponse<VersionResponseDto>> Handle(
        DeprecatePackageVersionCommand request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.DeprecateAsync(request.PackageId, request.Version, request.Request, cancellationToken);

        return new(result);
    }
}
