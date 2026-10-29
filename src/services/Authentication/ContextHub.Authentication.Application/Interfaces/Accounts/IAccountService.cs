namespace ContextHub.Authentication.Application.Interfaces.Accounts;

/// <summary>
/// Exposes interactive account outcomes without native Identity or ASP.NET types.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Checks credentials with native lockout before establishing the Owner password session.
    /// </summary>
    /// <param name="request">The credentials consumed only by native Identity; never log this DTO.</param>
    /// <returns>Whether native credential verification established a session.</returns>
    Task<bool> LoginAsync(LoginRequestDto request);

    /// <summary>
    /// Accepts an invitation through native password and concurrency handling; no separate password or SQL engine is needed.
    /// </summary>
    /// <param name="request">The invitation proof and initial password entered only at SSO.</param>
    /// <param name="cancellationToken">Cancels persistence.</param>
    /// <returns>Whether setup succeeded; every rejected invitation has the same public outcome.</returns>
    Task<bool> AcceptInvitationAsync(
        AcceptInvitationRequestDto request,
        CancellationToken cancellationToken);
}
