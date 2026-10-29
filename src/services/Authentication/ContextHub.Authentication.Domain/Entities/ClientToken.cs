namespace ContextHub.Authentication.Domain.Entities;

/// <summary>
/// Stores credential metadata and a digest; raw bearer values are never persisted.
/// </summary>
public sealed class ClientToken : BaseEntity<Guid>
{
    /// <summary>
    /// Gets or sets the owning application identifier.
    /// </summary>
    public Guid ClientId { get; set; }

    /// <summary>
    /// Gets or sets the owning application; its current active state participates in validation.
    /// </summary>
    public Client Client { get; set; } = null!;

    /// <summary>
    /// Gets or sets the non-secret display prefix, which cannot authenticate a caller.
    /// </summary>
    public string Prefix { get; set; } = "";

    /// <summary>
    /// Gets or sets the HMAC digest; the pepper and raw credential are never persisted here.
    /// </summary>
    public byte[] SecretHash { get; set; } = [];

    /// <summary>
    /// Gets or sets an existing credential's explicit expiry; null means no expiry and is the V1 issuance default.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets exact tool names granted to this token; each grant covers every feature of that tool.
    /// </summary>
    public string[] AllowedTools { get; set; } = [];

    /// <summary>
    /// Gets or sets the original UTC revocation time, preserved by repeated revocation.
    /// </summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// Gets or sets the revoking administrator, or null before revocation.
    /// </summary>
    public Guid? RevokedBy { get; set; }
}
