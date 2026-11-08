namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Creates a new canonical connection after shared Owner authorization.
/// </summary>
/// <param name="Request">The reviewed connection input.</param>
public sealed record CreateExternalMcpCommand(CreateExternalMcpRequestDto Request)
    : IRequest<ApiResponse<ExternalMcpResponseDto>>, IAdminRequest;
