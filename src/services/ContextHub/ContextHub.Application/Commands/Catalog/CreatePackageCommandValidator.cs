namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Rejects malformed mutation input before catalog I/O.
/// </summary>
public sealed class CreatePackageCommandValidator : AbstractValidator<CreatePackageCommand>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public CreatePackageCommandValidator()
    {
        RuleFor(value => value.Request).NotNull().SetValidator(new CreatePackageRequestDtoValidator());
    }
}
