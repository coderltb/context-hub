namespace ContextHub.Application.Queries.Mcp;

/// <summary>
/// Rejects invalid paging before database work.
/// </summary>
public sealed class GetMcpToolsQueryValidator : AbstractValidator<GetMcpToolsQuery>
{
    /// <summary>
    /// Applies existing page bounds to the Owner list contract.
    /// </summary>
    public GetMcpToolsQueryValidator()
    {
        RuleFor(value => value.Request.Query).MaximumLength(256);
        RuleFor(value => value.Request.Page).GreaterThanOrEqualTo(SharedConstants.PaginationVariables.FIRST_PAGE)
            .Must((value, page) => ((long)page - SharedConstants.PaginationVariables.FIRST_PAGE) * value.Request.PageSize <= int.MaxValue);
        RuleFor(value => value.Request.PageSize).InclusiveBetween(
            SharedConstants.PaginationVariables.FIRST_PAGE,
            SharedConstants.PaginationVariables.MAX_PAGE_SIZE);
    }
}
