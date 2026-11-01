namespace ContextHub.Application.Commands.Telemetry;

/// <summary>
/// Reuses the coded-signal rules before any budget or database IO.
/// </summary>
public sealed class SubmitQualitySignalCommandValidator : AbstractValidator<SubmitQualitySignalCommand>
{
    /// <summary>
    /// Connects the existing DTO validator to the shared request validation pipeline.
    /// </summary>
    /// <param name="qualitySignalValidator">The existing fixed-code and package-binding rules.</param>
    public SubmitQualitySignalCommandValidator(IValidator<QualitySignalRequestDto> qualitySignalValidator)
    {
        RuleFor(command => command.Request).NotNull().SetValidator(qualitySignalValidator);
    }
}
