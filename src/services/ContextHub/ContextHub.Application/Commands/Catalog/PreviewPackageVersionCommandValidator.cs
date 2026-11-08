namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Rejects malformed mutation input before catalog I/O.
/// </summary>
public sealed class PreviewPackageVersionCommandValidator : AbstractValidator<PreviewPackageVersionCommand>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public PreviewPackageVersionCommandValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty();

        RuleFor(value => value.Request).NotNull().SetValidator(new PublishVersionRequestDtoValidator());
    }
}
