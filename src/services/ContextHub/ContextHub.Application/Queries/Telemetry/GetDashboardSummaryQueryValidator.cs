namespace ContextHub.Application.Queries.Telemetry;

/// <summary>
/// Rejects unsupported public input before Infrastructure performs I/O.
/// </summary>
public sealed class GetDashboardSummaryQueryValidator : AbstractValidator<GetDashboardSummaryQuery>
{
    /// <summary>
    /// Registers the fixed contract rules for this operation.
    /// </summary>
    public GetDashboardSummaryQueryValidator()
    {
        RuleFor(value => value.Request).Must(value =>
        {
            var to = value.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var from = value.From ?? to.AddDays(-29);
            return from <= to && to.DayNumber - from.DayNumber < 90;
        }).WithMessage("The inclusive UTC range must contain between one and ninety days.");
    }
}
