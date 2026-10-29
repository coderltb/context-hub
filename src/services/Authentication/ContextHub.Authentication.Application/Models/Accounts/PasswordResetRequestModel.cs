namespace ContextHub.Authentication.Application.Models.Accounts;

/// <summary>
/// Binds an authenticated recovery identity to the new password inside the service boundary.
/// </summary>
/// <param name="userId">The identifier obtained from the authenticated recovery principal.</param>
/// <param name="token">The native proof obtained from that same protected principal.</param>
/// <param name="password">The submitted replacement password.</param>
public sealed class PasswordResetRequestModel(
    Guid userId,
    string token,
    string password)
{
    /// <summary>
    /// Gets the identifier obtained from the authenticated recovery principal.
    /// </summary>
    public Guid UserId { get; } = userId;

    /// <summary>
    /// Gets the native proof obtained from that same protected principal.
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// Gets the submitted replacement password.
    /// </summary>
    public string Password { get; } = password;
}
