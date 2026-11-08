namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Rejects unsupported public input before Infrastructure performs I/O.
/// </summary>
public sealed class GetRecommendationsQueryValidator : AbstractValidator<GetRecommendationsQuery>
{
    /// <summary>
    /// Registers the fixed contract rules for this operation.
    /// </summary>
    public GetRecommendationsQueryValidator()
    {
        RuleFor(value => value.Request.Page).GreaterThan(0);
        RuleFor(value => value.Request.PageSize).InclusiveBetween(1, 100);
        RuleFor(value => value.Request).Must(value => ((long)value.Page - 1) * value.PageSize <= int.MaxValue)
            .WithMessage("The requested page offset is too large.");
        RuleFor(value => value.Request.Status).Must(value => value is null || TelemetryConstants.STATUSES.Contains(value));
        RuleFor(value => value.Request.RecommendationType).Must(value => value is null ||
            TelemetryConstants.MANUAL_TYPES.Contains(value) || value == "reliability_issue");
        RuleFor(value => value.Request.Sort).Must(value => value is "createdAt" or "-createdAt" or "updatedAt" or "-updatedAt" or "signalCount" or "-signalCount");
    }
}
