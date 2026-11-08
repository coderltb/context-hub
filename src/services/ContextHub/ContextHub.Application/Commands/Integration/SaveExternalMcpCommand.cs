namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized SaveExternalMcpCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="ConnectionId">The stable connection UUID selected by Owner.</param>
/// <param name="Request">The validated Request input.</param>
public sealed record SaveExternalMcpCommand(Guid ConnectionId, SaveExternalMcpRequestDto Request) : IRequest<ApiResponse<ExternalMcpResponseDto>>, IAdminRequest;
