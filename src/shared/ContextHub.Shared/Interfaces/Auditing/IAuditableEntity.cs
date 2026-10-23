namespace ContextHub.Shared.Interfaces.Auditing;

/// <summary>
/// Opts an entity into audit timestamps and actors without imposing keys, lifecycle or soft deletion.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Records the original UTC creation instant.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Records the optional actor selected by the owning service's EF mapping.
    /// </summary>
    Guid? CreatedBy { get; set; }

    /// <summary>
    /// Records the latest UTC update instant.
    /// </summary>
    DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// Records the latest actor without fabricating an anonymous identifier.
    /// </summary>
    Guid? UpdatedBy { get; set; }
}
