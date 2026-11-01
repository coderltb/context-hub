namespace ContextHub.Application.Validators.Catalog;

/// <summary>
/// Enforces the published catalog input bounds before I/O.
/// </summary>
public sealed class UpdatePackageRequestDtoValidator : AbstractValidator<UpdatePackageRequestDto>
{
    /// <summary>
    /// Registers input constraints without duplicating authoritative database checks.
    /// </summary>
    public UpdatePackageRequestDtoValidator()
    {
        RuleFor(value => value.DisplayName).NotEmpty().MaximumLength(200);

        RuleFor(value => value.Summary).NotEmpty().MaximumLength(1000);

        RuleFor(value => value.ConcurrencyToken).NotEmpty().Must(value => uint.TryParse(value, out _));
    }
}
