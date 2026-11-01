namespace ContextHub.Application.Queries.Integration;

/// <summary>
/// Dispatches the authorized GetRepositoryQuery use case through the existing Mediator pipeline.
/// </summary>
/// <param name="RepositoryId">The validated RepositoryId input.</param>
public sealed record GetRepositoryQuery(Guid RepositoryId) : IRequest<ApiResponse<RepositoryResponseDto>>, IAdminRequest;
