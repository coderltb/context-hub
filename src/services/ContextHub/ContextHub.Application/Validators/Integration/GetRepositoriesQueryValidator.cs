namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Rejects invalid pagination and public filters before persistence is reached.
/// </summary>
public sealed class GetRepositoriesQueryValidator : AbstractValidator<GetRepositoriesQuery>
{
    /// <summary>
    /// Composes the bounded repository query contract.
    /// </summary>
    public GetRepositoriesQueryValidator()
    {
        RuleFor(value => value.Request.Page).GreaterThanOrEqualTo(1);
        RuleFor(value => value.Request.PageSize).InclusiveBetween(1, 100);
        RuleFor(value => value.Request).Must(value => ((long)value.Page - 1) * value.PageSize <= int.MaxValue);
        RuleFor(value => value.Request.Query).MaximumLength(512);
        RuleFor(value => value.Request.LastSyncStatus)
            .Must(value => value is null or "success" or "failed" or "never");
        RuleFor(value => value.Request.Sort).Must(value => value.TryParseRepositorySort(out _));
    }
}
