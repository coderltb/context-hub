namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected Packages operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.PACKAGES)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
public sealed class PackagesController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists package metadata with optional filters and stable paging.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<PackageResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] PackageListRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPackagesQuery(request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets package metadata and its current version summary.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet(AdminRouteConstants.PACKAGE_ID)]
    [ProducesResponseType<ApiResponse<PackageResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid packageId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPackageQuery(packageId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Creates a package identity without publishing a version.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<PackageResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePackageRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreatePackageCommand(request), cancellationToken);

        return CreatedAtAction(nameof(Get), new { packageId = result.Data!.PackageId }, result);
    }

    /// <summary>
    /// Updates editable metadata using the supplied concurrency token.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPut(AdminRouteConstants.PACKAGE_ID)]
    [ProducesResponseType<ApiResponse<PackageResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid packageId,
        [FromBody] UpdatePackageRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdatePackageCommand(packageId, request), cancellationToken);

        return Ok(result);
    }

}
