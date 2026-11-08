namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Reads the configuration of one tool independently of runtime enablement.
/// </summary>
/// <param name="ToolId">The immutable Owner-facing UUID.</param>
public sealed record GetMcpToolQuery(Guid ToolId) : IRequest<ApiResponse<McpToolDetailResponseDto>>, IAdminRequest;
