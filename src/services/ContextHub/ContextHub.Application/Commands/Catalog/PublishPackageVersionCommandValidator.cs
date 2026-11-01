namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Rejects malformed mutation input before catalog I/O.
/// </summary>
public sealed class PublishPackageVersionCommandValidator : AbstractValidator<PublishPackageVersionCommand>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public PublishPackageVersionCommandValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty().MaximumLength(128).Matches(CatalogConstants.PACKAGE_ID_PATTERN);

        RuleFor(value => value.Request).NotNull().SetValidator(new PublishVersionRequestDtoValidator());
    }
}
