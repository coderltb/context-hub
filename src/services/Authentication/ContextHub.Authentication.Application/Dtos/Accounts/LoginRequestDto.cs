namespace ContextHub.Authentication.Application.Dtos.Accounts;

/// <summary>
/// Binds credentials only at the SSO form boundary; never serialize or log this model.
/// </summary>
public sealed class LoginRequestDto
{
    /// <summary>
    /// Gets or sets the submitted account name.
    /// </summary>
    [Required]
    public string UserName { get; set; } = "";

    /// <summary>
    /// Gets or sets the password consumed only by native Identity verification.
    /// </summary>
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";
}
