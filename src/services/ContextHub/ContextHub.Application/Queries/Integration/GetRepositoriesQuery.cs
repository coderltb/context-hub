namespace ContextHub.Application.Queries.Integration;

/// <summary>
/// Dispatches the authorized GetRepositoriesQuery use case through the existing Mediator pipeline.
/// </summary>
/// <param name="Request">The validated Request input.</param>
public sealed record GetRepositoriesQuery(GetRepositoriesRequestDto Request) : IRequest<ApiResponse<PageResponse<RepositoryResponseDto>>>, IAdminRequest;
