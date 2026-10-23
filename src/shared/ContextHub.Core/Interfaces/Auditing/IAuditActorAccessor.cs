namespace ContextHub.Core.Interfaces.Auditing;

/// <summary>
/// Supplies a save-boundary actor from HTTP identity or explicit background-operation context.
/// </summary>
public interface IAuditActorAccessor
{
    /// <summary>
    /// Returns null when no actor exists; required EF actor fields must reject that absence.
    /// </summary>
    Guid? ActorId { get; }
}
