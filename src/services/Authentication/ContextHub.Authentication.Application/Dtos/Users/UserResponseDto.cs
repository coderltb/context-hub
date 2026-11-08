namespace ContextHub.Authentication.Application.Dtos.Users;

/// <summary>
/// Contains only approved account fields; framework secrets and security stamps are excluded.
/// </summary>
/// <param name="Id">The account identifier.</param>
/// <param name="UserName">The login name.</param>
/// <param name="Email">The optional recovery email address; legacy accounts may have no email.</param>
/// <param name="IsActive">The account's current status.</param>
/// <param name="Roles">The current role memberships.</param>
/// <param name="CreatedAt">The UTC creation time.</param>
/// <param name="UpdatedAt">The UTC update time.</param>
/// <param name="ConcurrencyToken">The opaque current version; copy this exact value into the next status, roles or invitation update.</param>
[PublicAPI]
public sealed record UserResponseDto(
    Guid Id,
    string UserName,
    string? Email,
    bool IsActive,
    string[] Roles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken)
{
    /// <summary>
    /// Gets stable role IDs for the Owner editor independently of display names.
    /// </summary>
    public Guid[] RoleIds { get; init; } = [];
}
