namespace ContextHub.Authentication.Application.Dtos.Accounts;

/// <summary>
/// Verifies a native email OTP without creating an authenticated Owner session.
/// </summary>
public sealed class VerifyPasswordResetOtpRequestDto
{
    /// <summary>
    /// Gets or sets the address used for the recovery request.
    /// </summary>
    [Required, EmailAddress, StringLength(AuthenticationConstants.MAX_ACCOUNT_FIELD_LENGTH)]
    public string Email { get; set; } = "";

    /// <summary>
    /// Gets or sets the six-digit native email proof, including leading zeroes.
    /// </summary>
    [Required, RegularExpression(AuthenticationRecoveryConstants.OTP_PATTERN)]
    public string OtpCode { get; set; } = "";
}
