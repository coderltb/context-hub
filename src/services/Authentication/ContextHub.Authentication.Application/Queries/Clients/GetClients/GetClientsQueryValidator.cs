namespace ContextHub.Authentication.Application.Queries.Clients.GetClients;

/// <summary>
/// Validates list inputs before creating bounded persistence queries.
/// </summary>
public sealed class GetClientsQueryValidator : AbstractValidator<GetClientsQuery>
{
    /// <summary>
    /// Defines page bounds for the shared validation pipeline.
    /// </summary>
    public GetClientsQueryValidator()
    {
        RuleFor(query => query.Request.Page)
            .GreaterThanOrEqualTo(SharedConstants.PaginationVariables.FIRST_PAGE);

        RuleFor(query => query.Request.PageSize)
            .InclusiveBetween(SharedConstants.PaginationVariables.FIRST_PAGE, SharedConstants.PaginationVariables.MAX_PAGE_SIZE);

        // Widen before multiplication so invalid external offsets fail validation before repository/database work.
        RuleFor(query => query.Request.Page)
            .Must((query, page) => ((long)page - SharedConstants.PaginationVariables.FIRST_PAGE) * query.Request.PageSize <= int.MaxValue)
            .WithMessage(CoreErrorConstants.RepositoryErrors.PAGE_OFFSET_TOO_LARGE);
    }
}
