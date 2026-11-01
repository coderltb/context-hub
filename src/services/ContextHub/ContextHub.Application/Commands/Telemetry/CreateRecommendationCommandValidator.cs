namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Rejects unsupported public input before Infrastructure performs I/O.
/// </summary>
public sealed class CreateRecommendationCommandValidator : AbstractValidator<CreateRecommendationCommand>
{
    /// <summary>
    /// Registers the fixed contract rules for this operation.
    /// </summary>
    public CreateRecommendationCommandValidator()
    {
        RuleFor(value => value.Request.RecommendationType).Must(value => TelemetryConstants.MANUAL_TYPES.Contains(value));
        RuleFor(value => value.Request.Title).NotEmpty().MaximumLength(200);
        RuleFor(value => value.Request.Description).NotEmpty().MaximumLength(4000);
        RuleFor(value => value.Request.PackageId).MaximumLength(160);
    }
}
