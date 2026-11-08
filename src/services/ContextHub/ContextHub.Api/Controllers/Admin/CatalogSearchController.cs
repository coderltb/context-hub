namespace ContextHub.Api.Controllers.Admin;

/// <summary>
/// Adapts protected CatalogSearch operations to the shared Application dispatcher.
/// </summary>
/// <param name="mediator">The scoped use-case dispatcher.</param>
[Route(AdminRouteConstants.CATALOG_SEARCH)]
[Authorize(Policy = HubConstants.ADMIN_POLICY)]
[ApiMetadataOnlyLogging]
public sealed class CatalogSearchController(IMediator mediator) : BaseApiController(mediator)
{
    /// <summary>
    /// Searches the latest eligible releases with lexical and semantic ranking.
    /// </summary>
    /// <param name="request">The grouped operation input.</param>
    /// <param name="cancellationToken">Cancels the owned operation.</param>
    /// <returns>The handler-owned response envelope.</returns>
    [HttpPost]
    [ProducesResponseType<ApiResponse<AdminCatalogSearchResponseDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromBody] CatalogSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await Mediator.Send(new SearchCatalogQuery(request), cancellationToken);

        return Ok(result);
    }

}
