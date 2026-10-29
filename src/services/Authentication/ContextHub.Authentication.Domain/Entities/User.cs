namespace ContextHub.Authentication.Domain.Entities;

/// <summary>
/// Extends the framework user without replacing Identity password or security-stamp handling.
/// </summary>
public sealed class User : IdentityUser<Guid>
{
    /// <summary>
    /// Gets or sets whether the account may authenticate or retain Admin access.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC creation time, protected from later updates.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp of the latest persisted account change.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

}
