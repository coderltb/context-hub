namespace ContextHub.Authentication.Application.Queries.Users.GetUsers;

/// <summary>
/// Validates list inputs before creating bounded persistence queries.
/// </summary>
public sealed class GetUsersQueryValidator : AbstractValidator<GetUsersQuery>
{
    /// <summary>
    /// Defines page bounds and the allowlisted multi-field ordering contract for the shared validation pipeline.
    /// </summary>
    public GetUsersQueryValidator()
    {
        RuleFor(query => query.Request.Page)
            .GreaterThanOrEqualTo(SharedConstants.PaginationVariables.FIRST_PAGE);

        RuleFor(query => query.Request.PageSize)
            .InclusiveBetween(
                SharedConstants.PaginationVariables.FIRST_PAGE,
                SharedConstants.PaginationVariables.MAX_PAGE_SIZE);

        // Widen before multiplication so invalid external offsets fail validation before repository/database work.
        RuleFor(query => query.Request.Page)
            .Must((query, page) => ((long)page - SharedConstants.PaginationVariables.FIRST_PAGE) * query.Request.PageSize <= int.MaxValue)
            .WithMessage(CoreErrorConstants.RepositoryErrors.PAGE_OFFSET_TOO_LARGE);

        // Reject unknown public aliases before the repository maps them to typed selectors; no property path is accepted.
        RuleFor(query => query.Request.Sort)
            .Must(sort => sort.TryParseUserSort(out _));
    }
}
