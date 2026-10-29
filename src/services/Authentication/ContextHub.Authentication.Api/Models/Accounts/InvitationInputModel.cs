namespace ContextHub.Authentication.Api.Models.Accounts;

/// <summary>
/// Binds initial password setup only at the invitation form; Identity owns password policy.
/// </summary>
public sealed class InvitationInputModel
{
    /// <summary>
    /// Gets or sets the initial password supplied by the invited account owner.
    /// </summary>
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = "";

    /// <summary>
    /// Gets or sets confirmation without exposing the submitted password in validation diagnostics.
    /// </summary>
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = "";
}
