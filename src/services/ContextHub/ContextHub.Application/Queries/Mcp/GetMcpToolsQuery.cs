namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Dispatches the protected GetListAsync use case.
/// </summary>
/// <param name="Request">The bounded Owner metadata query.</param>
public sealed record GetMcpToolsQuery(GetMcpToolsRequestDto Request) : IRequest<ApiResponse<PageResponse<McpToolResponseDto>>>, IAdminRequest;
