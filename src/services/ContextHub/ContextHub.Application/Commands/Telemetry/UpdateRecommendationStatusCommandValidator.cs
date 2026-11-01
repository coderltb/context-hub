namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Rejects unsupported public input before Infrastructure performs I/O.
/// </summary>
public sealed class UpdateRecommendationStatusCommandValidator : AbstractValidator<UpdateRecommendationStatusCommand>
{
    /// <summary>
    /// Registers the fixed contract rules for this operation.
    /// </summary>
    public UpdateRecommendationStatusCommandValidator()
    {
        RuleFor(value => value.Id).NotEmpty();
        RuleFor(value => value.Request.Status).Must(value => TelemetryConstants.STATUSES.Contains(value));
        RuleFor(value => value.Request.ConcurrencyToken).NotEmpty().MaximumLength(32);
    }
}
