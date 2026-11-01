namespace ContextHub.Infrastructure.Interfaces.Mcp;

/// <summary>
/// Keeps prompt entity queries behind the Infrastructure persistence boundary.
/// </summary>
public interface IMcpPromptRepository : IGenericRepository<McpPrompt>
{
    /// <summary>
    /// Reads a stable metadata page without loading guidance content.
    /// </summary>
    /// <param name="request">The validated paging and search input.</param>
    /// <param name="cancellationToken">Cancels count and items reads.</param>
    /// <returns>The mapped metadata page.</returns>
    Task<PageResponse<McpPromptSummaryResponseDto>> GetPageAsync(
        GetMcpPromptsRequestDto request,
        CancellationToken cancellationToken);
}
