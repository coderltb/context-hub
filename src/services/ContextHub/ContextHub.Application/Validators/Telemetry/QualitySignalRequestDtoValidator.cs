namespace ContextHub.Application.Validators.Telemetry;

/// <summary>
/// Restricts quality input to fixed public codes before deriving stored context.
/// </summary>
public sealed class QualitySignalRequestDtoValidator : AbstractValidator<QualitySignalRequestDto>
{
    /// <summary>
    /// Registers closed category combinations and package binding requirements.
    /// </summary>
    public QualitySignalRequestDtoValidator()
    {
        RuleFor(value => value.ArtifactType).Must(value => value is "code" or "document" or "presentation" or "spreadsheet" or "other");
        RuleFor(value => value.Operation).Must(value => value is "create" or "review" or "analyze" or "transform" or "other");
        RuleFor(value => value).Must(QualitySignalPolicy.IsValidCombination)
            .WithMessage("The quality signal codes and package binding are invalid.");
        RuleFor(value => value.PackageId).Matches("^[a-z0-9][a-z0-9._-]*$").MaximumLength(160).When(value => value.PackageId is not null);
        RuleFor(value => value.Version).Matches(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$").MaximumLength(64).When(value => value.Version is not null);
        RuleFor(value => value.WorkflowStepId).Matches("^[a-z0-9][a-z0-9_-]*$").MaximumLength(64).When(value => value.WorkflowStepId is not null);
        RuleFor(value => value.Capability).Must(value => value is null or "api_review" or "api_implementation" or
            "slide_authoring" or "spreadsheet_editing" or "document_editing" or "research_verification" or
            "workspace_read" or "workspace_write" or "command_execution" or "independent_reviewer" or "presentation_edit" or "spreadsheet_edit");
    }
}
