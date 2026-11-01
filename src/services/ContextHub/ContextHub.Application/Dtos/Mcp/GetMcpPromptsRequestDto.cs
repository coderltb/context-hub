namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public sealed record GetMcpPromptsRequestDto
{
    /// <summary>
    /// Gets the one-based page number.
    /// </summary>
    public int Page { get; init; } = SharedConstants.PaginationVariables.FIRST_PAGE;

    /// <summary>
    /// Gets the bounded page size.
    /// </summary>
    public int PageSize { get; init; } = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE;

    /// <summary>
    /// Gets literal metadata search text.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets comma-separated name or updatedAt fields with asc or desc.
    /// </summary>
    public string? Sort { get; init; } = "name asc";
}
