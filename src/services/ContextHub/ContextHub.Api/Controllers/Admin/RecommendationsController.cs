namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected Recommendations operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.RECOMMENDATIONS)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
public sealed class RecommendationsController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Lists review items and their current disposition.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PageResponse<RecommendationResponseDto>>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetList(
        [FromQuery] GetRecommendationsRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRecommendationsQuery(request), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets one review item without raw client task data.
    /// </summary>
    /// <param name="recommendationId">The exact route or concurrency identifier.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet(AdminRouteConstants.RECOMMENDATION_ID)]
    [ProducesResponseType<ApiResponse<RecommendationResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid recommendationId,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetRecommendationQuery(recommendationId), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Creates an Owner-authored review item without publishing content.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<RecommendationResponseDto>>(StatusCodes.Status201Created)]
    [OpenApiOperation(StatusCodes.Status201Created, StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecommendationRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new CreateRecommendationCommand(request), cancellationToken);

        return CreatedAtAction(nameof(Get), new { recommendationId = result.Data!.Id }, result);
    }

    /// <summary>
    /// Applies an allowed review transition using optimistic concurrency.
    /// </summary>
    /// <param name="recommendationId">The exact route or concurrency identifier.</param>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPut(AdminRouteConstants.RECOMMENDATION_STATUS)]
    [ProducesResponseType<ApiResponse<RecommendationResponseDto>>(StatusCodes.Status200OK)]
    [OpenApiOperation(StatusCodes.Status200OK, StatusCodes.Status404NotFound, StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid recommendationId,
        [FromBody] UpdateRecommendationStatusRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new UpdateRecommendationStatusCommand(recommendationId, request), cancellationToken);

        return Ok(result);
    }

}
