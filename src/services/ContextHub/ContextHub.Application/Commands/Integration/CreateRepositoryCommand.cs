namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized CreateRepositoryCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="Request">The validated Request input.</param>
public sealed record CreateRepositoryCommand(CreateRepositoryRequestDto Request) : IRequest<ApiResponse<RepositoryResponseDto>>, IAdminRequest;
