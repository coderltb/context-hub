namespace ContextHub.Application.Commands.Mcp;

/// <summary>
/// Dispatches the protected UpdateAsync use case.
/// </summary>
/// <param name="ToolName">The approved toolname input.</param>
/// <param name="Request">The approved request input.</param>
public sealed record UpdateMcpToolCommand(
    string ToolName,
    UpdateMcpToolRequestDto Request) : IRequest<ApiResponse<McpToolResponseDto>>, IAdminRequest;
