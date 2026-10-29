namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Creates a passwordless account through the explicit invitation flow rather than treating a missing password as a switch.
/// </summary>
/// <param name="UserName">The login name for the invited account.</param>
/// <param name="Email">The unique contact address for this account.</param>
/// <param name="RoleNames">The non-empty set of provisioned role names; each name must be non-blank.</param>
/// <param name="IsActive">Whether the invited account may complete setup.</param>
public sealed record InviteUserRequestDto(
    string UserName,
    string Email,
    string[] RoleNames,
    bool IsActive = true);
