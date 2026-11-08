namespace ContextHub.Domain.Enums;

/// <summary>
/// Keeps retained deletion independent of a connection's enabled configuration.
/// </summary>
public enum ExternalMcpStatus
{
    /// <summary>
    /// The retained connection can be configured and routed when enabled.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The retained connection must never participate in discovery or routing.
    /// </summary>
    Deleted = 1
}
