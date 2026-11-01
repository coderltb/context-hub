namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected Repositories operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.REPOSITORIES)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
public sealed class RepositoriesController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists registered authoring repositories without source contents.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<RepositoryResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetRepositoriesRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRepositoriesQuery(request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets the Owner-managed source connection metadata.
    /// </summary>
    /// <param name="repositoryId">The exact route or concurrency identifier.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet(AdminRouteConstants.REPOSITORY_ID)]
    [ProducesResponseType<ApiResponse<RepositoryResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid repositoryId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRepositoryQuery(repositoryId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Registers a GitHub source without importing or publishing packages.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<RepositoryResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRepositoryRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateRepositoryCommand(request), cancellationToken);

        return CreatedAtAction(nameof(Get), new { repositoryId = result.Data!.Id }, result);
    }

    /// <summary>
    /// Updates source settings with optimistic concurrency.
    /// </summary>
    /// <param name="repositoryId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPut(AdminRouteConstants.REPOSITORY_ID)]
    [ProducesResponseType<ApiResponse<RepositoryResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid repositoryId,
        [FromBody] UpdateRepositoryRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdateRepositoryCommand(repositoryId, request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Inspects the requested source ref without automatically publishing.
    /// </summary>
    /// <param name="repositoryId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost(AdminRouteConstants.SYNC)]
    [ProducesResponseType<ApiResponse<RepositorySyncResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Sync(
        Guid repositoryId,
        [FromBody] SyncRepositoryRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SyncRepositoryCommand(repositoryId, request), cancellationToken);

        return Ok(result);
    }

}
