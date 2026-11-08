namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected PackageVersions operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.PACKAGE_VERSIONS)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
[ApiMetadataOnlyLogging]
public sealed class PackageVersionsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists exact versions and current lifecycle metadata.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<VersionSummaryResponseDto>>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetList(
        Guid packageId,
        [FromQuery] VersionListRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPackageVersionsQuery(packageId, request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets the Owner projection of an exact published version.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="version">The exact route or concurrency identifier.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet(AdminRouteConstants.VERSION)]
    [ProducesResponseType<ApiResponse<VersionResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid packageId,
        string version,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetPackageVersionQuery(packageId, version), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Validates source content and compares it without saving a release.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost(AdminRouteConstants.PREVIEW)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<ApiResponse<PackagePreviewResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Preview(
        Guid packageId,
        [FromBody] PublishVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new PreviewPackageVersionCommand(packageId, request),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Publishes validated immutable content from an exact source commit.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<VersionResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Publish(
        Guid packageId,
        [FromBody] PublishVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new PublishPackageVersionCommand(packageId, request),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { packageId, version = result.Data!.Version }, result);
    }

    /// <summary>
    /// Deprecates an exact release while retaining immutable content.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="version">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost(AdminRouteConstants.DEPRECATE)]
    [ProducesResponseType<ApiResponse<VersionResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Deprecate(
        Guid packageId,
        string version,
        [FromBody] LifecycleVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new DeprecatePackageVersionCommand(packageId, version, request),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Revokes an exact release and blocks subsequent resolution.
    /// </summary>
    /// <param name="packageId">The exact route or concurrency identifier.</param>
    /// <param name="version">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost(AdminRouteConstants.REVOKE)]
    [ProducesResponseType<ApiResponse<VersionResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        Guid packageId,
        string version,
        [FromBody] LifecycleVersionRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(
            new RevokePackageVersionCommand(packageId, version, request),
            cancellationToken);

        return Ok(result);
    }

}
