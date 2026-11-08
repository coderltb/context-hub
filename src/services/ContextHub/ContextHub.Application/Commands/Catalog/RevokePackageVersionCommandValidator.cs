namespace ContextHub.Application.Commands.Catalog;

/// <summary>
/// Rejects malformed mutation input before catalog I/O.
/// </summary>
public sealed class RevokePackageVersionCommandValidator : AbstractValidator<RevokePackageVersionCommand>
{
    /// <summary>
    /// Registers structural checks; live state and concurrency stay inside the operation.
    /// </summary>
    public RevokePackageVersionCommandValidator()
    {
        RuleFor(value => value.PackageId).NotEmpty();

        RuleFor(value => value.Version).NotEmpty().Matches(CatalogConstants.STABLE_VERSION_PATTERN);

        RuleFor(value => value.Request).NotNull().SetValidator(new LifecycleVersionRequestDtoValidator());
    }
}
