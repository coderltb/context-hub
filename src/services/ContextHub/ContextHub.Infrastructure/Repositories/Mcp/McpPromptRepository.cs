namespace ContextHub.Infrastructure.Repositories.Mcp;

/// <summary>
/// Reuses the shared predicate, projection and paging pipeline for prompt configuration.
/// </summary>
/// <param name="contextHubDbContext">The sole scoped service context.</param>
/// <param name="logger">The enclosing repository metadata logger.</param>
public sealed class McpPromptRepository(
    ContextHubDbContext contextHubDbContext,
    ILogger<McpPromptRepository> logger)
    : GenericRepository<McpPrompt>(contextHubDbContext, logger), IMcpPromptRepository
{
    /// <summary>
    /// Applies allowlisted ordering with the unique prompt name as a stable tie-breaker.
    /// </summary>
    /// <param name="request">The validated list input.</param>
    /// <param name="cancellationToken">Cancels sequential page queries.</param>
    /// <returns>The metadata-only page.</returns>
    public Task<PageResponse<McpPromptSummaryResponseDto>> GetPageAsync(
        GetMcpPromptsRequestDto request,
        CancellationToken cancellationToken)
    {
        SortExpressionHelper.TryParse(request.Sort, McpPromptConstants.SORT_FIELDS, out var clauses);
        QueryOrder<McpPrompt>? order = null;
        foreach (var clause in clauses)
        {
            order = clause.Field switch
            {
                "updatedAt" => order is null
                    ? QueryOrder<McpPrompt>.By(value => value.UpdatedAt, clause.Descending)
                    : order.ThenBy(value => value.UpdatedAt, clause.Descending),
                _ => order is null
                    ? QueryOrder<McpPrompt>.By(value => value.Name, clause.Descending)
                    : order.ThenBy(value => value.Name, clause.Descending)
            };
        }

        order = order is null ? QueryOrder<McpPrompt>.By(value => value.Name) : order.ThenBy(value => value.Name);
        var search = new SearchOptions<McpPrompt>
        {
            SearchTerm = request.Query,
            TextFields = [value => value.Name, value => value.Title!, value => value.Description!]
        };
        return GetPageAsync<McpPromptSummaryResponseDto>(
            new PageRequest(request.Page, request.PageSize),
            order,
            search: search,
            cancellationToken: cancellationToken);
    }
}
