namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized SyncRepositoryCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="RepositoryId">The validated RepositoryId input.</param>
/// <param name="Request">The validated Request input.</param>
public sealed record SyncRepositoryCommand(Guid RepositoryId, SyncRepositoryRequestDto Request) : IRequest<ApiResponse<RepositorySyncResponseDto>>, IAdminRequest;
