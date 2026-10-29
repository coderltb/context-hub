namespace ContextHub.Authentication.Application.Dtos.Accounts;

/// <summary>
/// Carries one invitation acceptance without logging its password or native token.
/// </summary>
/// <param name="userId">The invited account identifier.</param>
/// <param name="token">The native invitation proof.</param>
/// <param name="password">The initial password consumed by Identity.</param>
public sealed class AcceptInvitationRequestDto(
    Guid userId,
    string token,
    string password)
{
    /// <summary>
    /// Gets the invited account identifier.
    /// </summary>
    public Guid UserId { get; } = userId;

    /// <summary>
    /// Gets the native invitation proof.
    /// </summary>
    public string Token { get; } = token;

    /// <summary>
    /// Gets the initial password consumed by Identity.
    /// </summary>
    public string Password { get; } = password;
}
