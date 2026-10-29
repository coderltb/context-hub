namespace ContextHub.Authentication.Application.Dtos.Clients;

/// <summary>
/// Defines the administrator-supplied fields for application creation.
/// </summary>
/// <param name="DisplayName">The required application label.</param>
public sealed record CreateClientRequestDto(string DisplayName)
{
    /// <summary>
    /// Gets or initializes exact tools granted to the first token; omission grants no tools, not all tools.
    /// </summary>
    /// <example>["example-tool"]</example>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// Gets or initializes the expiry; null keeps this credential valid until revocation or client disablement; a supplied value must be in the future.
    /// </summary>
    /// <example>null</example>
    public DateTimeOffset? ExpiresAt { get; init; }
}
