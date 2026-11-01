namespace ContextHub.Application.Commands.Integration;

/// <summary>
/// Dispatches the authorized SaveExternalMcpCommand use case through the existing Mediator pipeline.
/// </summary>
/// <param name="Name">The validated Name input.</param>
/// <param name="Request">The validated Request input.</param>
public sealed record SaveExternalMcpCommand(string Name, SaveExternalMcpRequestDto Request) : IRequest<ApiResponse<ExternalMcpResponseDto>>, IAdminRequest;
