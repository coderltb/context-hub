namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Returns only the replacement invitation fields already exposed by the invitation endpoint.
/// </summary>
/// <param name="InvitationUrl">The one-time setup URL; never log or persist this response.</param>
/// <param name="InvitationExpiresAt">The expiry in UTC.</param>
[PublicAPI]
public sealed record InvitationResponseDto(string InvitationUrl, DateTimeOffset InvitationExpiresAt);
