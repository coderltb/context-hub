namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized DeleteExternalMcpCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="ConnectionId">The validated Name input.</param>
/// <param name="ConcurrencyToken">The validated ConcurrencyToken input.</param>
public sealed record DeleteExternalMcpCommand(Guid ConnectionId, string ConcurrencyToken) : IRequest<ApiResponse<DeletedIntegrationResponseDto>>, IAdminRequest;
