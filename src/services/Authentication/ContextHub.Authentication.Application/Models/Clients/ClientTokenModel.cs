namespace ContextHub.Authentication.Application.Models.Clients;

/// <summary>
/// Carries Application state across the persistence port without exposing an EF entity.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed record ClientTokenModel
{
    /// <summary>
    /// Gets or initializes the credential metadata identifier.
    /// </summary>
    public Guid TokenId { get; init; }

    /// <summary>
    /// Gets or initializes the owning application identifier.
    /// </summary>
    public Guid ClientId { get; init; }

    /// <summary>
    /// Gets or initializes the non-secret display prefix.
    /// </summary>
    public string Prefix { get; init; } = "";

    /// <summary>
    /// Gets or initializes the digest for persistence; never return or log this model.
    /// </summary>
    public byte[] SecretHash { get; init; } = [];

    /// <summary>
    /// Gets or initializes the UTC issuance timestamp.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the issuing administrator.
    /// </summary>
    public Guid CreatedBy { get; init; }

    /// <summary>
    /// Gets or initializes the latest UTC lifecycle timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>
    /// Gets or initializes the administrator changing the lifecycle state.
    /// </summary>
    public Guid? UpdatedBy { get; init; }

    /// <summary>
    /// Gets or initializes the optional UTC expiry; null means no fixed expiration.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Gets or initializes the exact tool allowlist, never a list of feature/action permissions.
    /// </summary>
    public string[] AllowedTools { get; init; } = [];

    /// <summary>
    /// Gets or initializes the original revocation timestamp.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; init; }

    /// <summary>
    /// Gets or initializes the original revoking administrator.
    /// </summary>
    public Guid? RevokedBy { get; init; }
}
