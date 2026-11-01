namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Requests the protected prompt configuration operation.
/// </summary>
/// <param name="Request">The approved operation input.</param>
public sealed record CreateMcpPromptCommand(CreateMcpPromptRequestDto Request) : IRequest<ApiResponse<McpPromptResponseDto>>, IAdminRequest;
