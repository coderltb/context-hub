namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Returns an invitation only at issuance; the URL must never enter logs or persistent client caches.
/// </summary>
/// <param name="User">The approved account projection.</param>
/// <param name="InvitationUrl">The one-time setup URL.</param>
/// <param name="InvitationExpiresAt">The expiry in UTC.</param>
public sealed record InvitedUserResponseDto(
    UserResponseDto User,
    string InvitationUrl,
    DateTimeOffset InvitationExpiresAt);
