namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Bounds paging and ordering before provider I/O.
/// </summary>
public sealed class GetMcpPromptsQueryValidator : AbstractValidator<GetMcpPromptsQuery>
{
    /// <summary>
    /// Registers the existing integer-offset and ordering constraints.
    /// </summary>
    public GetMcpPromptsQueryValidator()
    {
        RuleFor(value => value.Request.Page).GreaterThan(0);
        RuleFor(value => value.Request.PageSize).InclusiveBetween(
            SharedConstants.PaginationVariables.FIRST_PAGE,
            SharedConstants.PaginationVariables.MAX_PAGE_SIZE);
        RuleFor(value => value.Request).Must(value => (long)(value.Page - 1) * value.PageSize <= int.MaxValue);
        RuleFor(value => value.Request.Sort).Must(value => SortExpressionHelper.TryParse(value, McpPromptConstants.SORT_FIELDS, out _));
    }
}
