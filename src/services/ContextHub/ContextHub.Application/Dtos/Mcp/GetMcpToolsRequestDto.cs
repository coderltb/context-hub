namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Selects an Owner tool page without changing native MCP discovery.
/// </summary>
public sealed class GetMcpToolsRequestDto
{
    /// <summary>
    /// Gets the optional metadata query.
    /// </summary>
    public string? Query { get; init; }

    /// <summary>
    /// Gets the optional global enabled filter.
    /// </summary>
    public bool? IsEnabled { get; init; }

    /// <summary>
    /// Gets the one-based page.
    /// </summary>
    public int Page { get; init; } = SharedConstants.PaginationVariables.FIRST_PAGE;

    /// <summary>
    /// Gets the bounded number of records to return.
    /// </summary>
    public int PageSize { get; init; } = SharedConstants.PaginationVariables.DEFAULT_PAGE_SIZE;
}
