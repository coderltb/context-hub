namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Defines explicit tool grants for one new application credential.
/// </summary>
[PublicAPI]
public sealed class IssueClientTokenRequestDto
{
    /// <summary>
    /// Gets or initializes exact tool names; a permitted tool includes all its features.
    /// </summary>
    /// <example>["example-tool"]</example>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// Gets or initializes the expiry; null keeps this credential valid until revocation or client disablement; a supplied value must be in the future.
    /// </summary>
    /// <example>2099-01-01T00:00:00Z</example>
    public DateTimeOffset? ExpiresAt { get; init; }
}
