namespace ContextHub.Authentication.Application.Models.Accounts;

/// <summary>
/// Carries only the approved account identity needed to send a native recovery proof.
/// </summary>
/// <param name="userId">The approved account identifier.</param>
/// <param name="email">The address stored on that account.</param>
public sealed class PasswordRecoveryAccountModel(
    Guid userId,
    string email)
{
    /// <summary>
    /// Gets the approved account identifier.
    /// </summary>
    public Guid UserId { get; } = userId;

    /// <summary>
    /// Gets the address stored on that account.
    /// </summary>
    public string Email { get; } = email;
}
