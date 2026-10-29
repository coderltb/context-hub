namespace ContextHub.Shared.Entities;

/// <summary>
/// Shares a typed identifier and audit properties without owning key generation, persistence or lifecycle policy.
/// Audit values are populated by the existing opt-in save interceptor, not by entity construction.
/// </summary>
/// <typeparam name="TKey">The non-nullable key type chosen by the entity's owning service.</typeparam>
public abstract class BaseEntity<TKey> : IAuditableEntity
    where TKey : notnull
{
    /// <summary>
    /// Gets or sets the identifier assigned by the owning service or database.
    /// </summary>
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// Gets or sets the original UTC creation instant protected by the audit interceptor on updates.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the creating actor; the owning EF configuration decides whether absence is permitted.
    /// </summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the latest UTC save instant supplied by the audit interceptor.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Gets or sets the latest actor without fabricating an identifier for system-owned work.
    /// </summary>
    public Guid? UpdatedBy { get; set; }
}
