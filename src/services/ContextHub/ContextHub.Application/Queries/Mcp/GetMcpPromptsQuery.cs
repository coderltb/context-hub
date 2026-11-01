namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Requests the protected prompt configuration operation.
/// </summary>
/// <param name="Request">The approved operation input.</param>
public sealed record GetMcpPromptsQuery(GetMcpPromptsRequestDto Request) : IRequest<ApiResponse<PageResponse<McpPromptSummaryResponseDto>>>, IAdminRequest;
