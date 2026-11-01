namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Coordinates RevokePackageVersion after shared authorization and validation.
/// </summary>
/// <param name="catalogService">The catalog operation port; persistence entities remain behind it.</param>
/// <param name="logger">The lifecycle-event logger; it never receives source content.</param>
public sealed class RevokePackageVersionCommandHandler(
    ICatalogService catalogService,
    ILogger<RevokePackageVersionCommandHandler> logger)
    : IRequestHandler<RevokePackageVersionCommand, ApiResponse<VersionResponseDto>>
{
    /// <summary>
    /// Returns the approved catalog result in the shared HTTP envelope.
    /// </summary>
    /// <param name="request">The validated catalog operation.</param>
    /// <param name="cancellationToken">Cancels underlying source and database I/O.</param>
    /// <returns>The approved result without persistence entities.</returns>
    public async ValueTask<ApiResponse<VersionResponseDto>> Handle(
        RevokePackageVersionCommand request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.RevokeAsync(request.PackageId, request.Version, request.Request, cancellationToken);

        logger.LogInformation(CatalogLogConstants.VERSION_REVOKED, result.PackageId, result.Version);

        return new(result);
    }
}
