namespace ContextHub.Shared.Enums;

/// <summary>
/// Classifies expected business failures without selecting a transport or HTTP status.
/// </summary>
public enum BusinessErrorKind
{
    /// <summary>
    /// Indicates an operation that cannot satisfy a business requirement.
    /// </summary>
    Invalid = 0,

    /// <summary>
    /// Indicates that the requested business resource is absent.
    /// </summary>
    NotFound = 1,

    /// <summary>
    /// Indicates an operation conflicting with the current business state.
    /// </summary>
    Conflict = 2
}
