namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Returns the raw credential only at issuance; callers must not persist or log this DTO.
/// </summary>
/// <param name="ClientId">The receiving application.</param>
/// <param name="TokenId">The revocable credential identifier.</param>
/// <param name="Token">The raw bearer credential returned only by this issuance response; metadata endpoints never return it again.</param>
/// <param name="TokenType">The HTTP authorization scheme.</param>
/// <param name="ExpiresAt">The requested UTC expiry; null means no fixed expiration.</param>
public sealed record IssuedClientTokenResponseDto(
    Guid ClientId,
    Guid TokenId,
    string Token,
    string TokenType,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Gets or initializes the tool grants belonging to this credential rather than to all credentials of the client.
    /// </summary>
    /// <example>["example-tool"]</example>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];
}
