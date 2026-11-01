namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Coordinates PublishPackageVersion after shared authorization and validation.
/// </summary>
/// <param name="catalogService">The catalog operation port; persistence entities remain behind it.</param>
/// <param name="logger">The lifecycle-event logger; it never receives source content.</param>
public sealed class PublishPackageVersionCommandHandler(
    ICatalogService catalogService,
    ILogger<PublishPackageVersionCommandHandler> logger)
    : IRequestHandler<PublishPackageVersionCommand, ApiResponse<VersionResponseDto>>
{
    /// <summary>
    /// Returns the approved catalog result in the shared HTTP envelope.
    /// </summary>
    /// <param name="request">The validated catalog operation.</param>
    /// <param name="cancellationToken">Cancels underlying source and database I/O.</param>
    /// <returns>The approved result without persistence entities.</returns>
    public async ValueTask<ApiResponse<VersionResponseDto>> Handle(
        PublishPackageVersionCommand request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.PublishAsync(request.PackageId, request.Request, cancellationToken);

        logger.LogInformation(CatalogLogConstants.PUBLICATION_ACCEPTED, result.PackageId, result.Version);

        return new(result);
    }
}
