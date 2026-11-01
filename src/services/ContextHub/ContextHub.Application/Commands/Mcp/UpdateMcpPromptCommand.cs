namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Requests the protected prompt configuration operation.
/// </summary>
/// <param name="Id">The approved operation input.</param>
/// <param name="Request">The approved operation input.</param>
public sealed record UpdateMcpPromptCommand(
    Guid Id,
    UpdateMcpPromptRequestDto Request) : IRequest<ApiResponse<McpPromptResponseDto>>, IAdminRequest;
