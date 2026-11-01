namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized UpdateRepositoryCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="RepositoryId">The validated RepositoryId input.</param>
/// <param name="Request">The validated Request input.</param>
public sealed record UpdateRepositoryCommand(Guid RepositoryId, UpdateRepositoryRequestDto Request) : IRequest<ApiResponse<RepositoryResponseDto>>, IAdminRequest;
