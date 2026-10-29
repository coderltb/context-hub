namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Exposes lifecycle metadata that remains safe after the issuance response has been discarded.
/// </summary>
/// <param name="TokenId">The credential identifier.</param>
/// <param name="Prefix">The non-secret display prefix.</param>
/// <param name="CreatedAt">The issuance time.</param>
/// <param name="ExpiresAt">The optional expiry in UTC; null means valid until revocation or client disablement.</param>
/// <param name="RevokedAt">The original revocation time, if revoked.</param>
[PublicAPI]
public sealed record ClientTokenResponseDto(
    Guid TokenId,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? RevokedAt)
{
    /// <summary>
    /// Gets or initializes this credential's tool allowlist, safe to display without revealing the credential.
    /// </summary>
    /// <example>["example-tool"]</example>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}
