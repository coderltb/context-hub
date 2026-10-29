namespace ContextHub.Authentication.Application.Models.Accounts;

/// <summary>
/// Carries a verified proof only between Mediator and the protected native recovery cookie.
/// </summary>
/// <param name="userId">The verified account identifier.</param>
/// <param name="token">The native password-reset proof; never log this model.</param>
public sealed class PasswordRecoveryGrantModel(
    Guid userId,
    string token)
{
    /// <summary>
    /// Gets the verified account identifier.
    /// </summary>
    public Guid UserId { get; } = userId;

    /// <summary>
    /// Gets the native password-reset proof; never log this model.
    /// </summary>
    public string Token { get; } = token;
}
