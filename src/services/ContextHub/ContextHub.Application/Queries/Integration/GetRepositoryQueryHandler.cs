namespace ContextHub.Application.Queries.Integration;

/// <summary>
/// Returns the approved integration result after shared authorization and validation.
/// </summary>
/// <param name="repositoryService">The application-owned integration port.</param>
public sealed class GetRepositoryQueryHandler(IRepositoryService repositoryService)
    : IRequestHandler<GetRepositoryQuery, ApiResponse<RepositoryResponseDto>>
{
    /// <summary>
    /// Delegates the validated operation without exposing persistence entities.
    /// </summary>
    /// <param name="request">The authorized integration operation.</param>
    /// <param name="cancellationToken">Cancels the underlying IO boundary.</param>
    /// <returns>The approved metadata in the common response envelope.</returns>
    public async ValueTask<ApiResponse<RepositoryResponseDto>> Handle(
        GetRepositoryQuery request,
        CancellationToken cancellationToken) =>
        new(await repositoryService.GetAsync(request.RepositoryId, cancellationToken));
}
