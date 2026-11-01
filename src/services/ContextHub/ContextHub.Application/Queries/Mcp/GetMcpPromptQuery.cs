namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests the protected prompt configuration operation.
/// </summary>
/// <param name="Id">The approved operation input.</param>
public sealed record GetMcpPromptQuery(Guid Id) : IRequest<ApiResponse<McpPromptResponseDto>>, IAdminRequest;
