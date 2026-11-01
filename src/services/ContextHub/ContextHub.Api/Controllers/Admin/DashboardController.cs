namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected Dashboard operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.DASHBOARD_SUMMARY)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
public sealed class DashboardController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Summarizes daily aggregates without raw invocation records.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpGet]
    [ProducesResponseType<ApiResponse<DashboardSummaryResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DashboardRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new GetDashboardSummaryQuery(request), cancellationToken);

        return Ok(result);
    }

}
