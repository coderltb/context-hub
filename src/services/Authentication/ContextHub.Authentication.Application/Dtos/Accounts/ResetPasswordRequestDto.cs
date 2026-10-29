namespace ContextHub.Authentication.Application.Dtos.Accounts;

/// <summary>
/// Supplies a new password after proof has been authenticated by the dedicated recovery cookie.
/// </summary>
public sealed class ResetPasswordRequestDto
{
    /// <summary>
    /// Gets or sets the replacement password consumed only by native Identity.
    /// </summary>
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>
    /// Gets or sets the form confirmation, never a separate credential authority.
    /// </summary>
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}
