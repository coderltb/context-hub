namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Describes a client without exposing credential material.
/// </summary>
/// <param name="ClientId">The stable application identifier.</param>
/// <param name="DisplayName">The administrator's label.</param>
/// <param name="IsActive">Whether its credentials may authenticate.</param>
/// <param name="CreatedAt">The creation time in UTC.</param>
/// <param name="UpdatedAt">The latest modification time in UTC.</param>
/// <param name="ConcurrencyToken">The opaque current version; copy it unchanged into the next client update.</param>
[PublicAPI]
public sealed record ClientResponseDto(
    Guid ClientId,
    string DisplayName,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);
