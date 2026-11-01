namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Coordinates UpdatePackage after shared authorization and validation.
/// </summary>
/// <param name="catalogService">The catalog operation port; persistence entities remain behind it.</param>
public sealed class UpdatePackageCommandHandler(ICatalogService catalogService)
    : IRequestHandler<UpdatePackageCommand, ApiResponse<PackageResponseDto>>
{
    /// <summary>
    /// Returns the approved catalog result in the shared HTTP envelope.
    /// </summary>
    /// <param name="request">The validated catalog operation.</param>
    /// <param name="cancellationToken">Cancels underlying source and database I/O.</param>
    /// <returns>The approved result without persistence entities.</returns>
    public async ValueTask<ApiResponse<PackageResponseDto>> Handle(
        UpdatePackageCommand request,
        CancellationToken cancellationToken)
    {
        var result = await catalogService.UpdatePackageAsync(request.PackageId, request.Request, cancellationToken);

        return new(result);
    }
}
