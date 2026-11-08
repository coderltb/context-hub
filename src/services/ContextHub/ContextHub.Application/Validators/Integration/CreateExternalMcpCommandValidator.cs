namespace ContextHub.Application.Validators.Integration;

/// <summary>
/// Validates creation identity before provider or database work.
/// </summary>
public sealed class CreateExternalMcpCommandValidator : AbstractValidator<CreateExternalMcpCommand>
{
    /// <summary>
    /// Applies the existing connection-name bound to the body field.
    /// </summary>
    public CreateExternalMcpCommandValidator()
    {
        RuleFor(value => value.Request.Name).NotEmpty().MaximumLength(120);
    }
}
