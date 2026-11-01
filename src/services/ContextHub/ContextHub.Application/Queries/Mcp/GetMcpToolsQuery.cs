namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Dispatches the protected GetListAsync use case.
/// </summary>
public sealed record GetMcpToolsQuery : IRequest<ApiResponse<McpToolsResponseDto>>, IAdminRequest;
