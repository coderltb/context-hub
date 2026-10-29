namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Requires the latest read version before regenerating a one-time invitation.
/// </summary>
/// <param name="ConcurrencyToken">The concurrencyToken from the latest account response; stale values produce a conflict.</param>
public sealed record InvitationRequestDto(string ConcurrencyToken);
