namespace ContextHub.Authentication.Application.Dtos.Accounts;

/// <summary>
/// Requests password recovery without distinguishing registered and unavailable email addresses.
/// </summary>
public sealed class ForgotPasswordRequestDto
{
    /// <summary>
    /// Gets or sets the recovery address, which is never a log property.
    /// </summary>
    [Required, EmailAddress, StringLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH)]
    public string Email { get; set; } = "";
}
