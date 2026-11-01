namespace ContextHub.Application.Enums;

/// <summary>
/// Identifies an internal resource route without changing public URI values.
/// </summary>
public enum McpResourceKind
{
    /// <summary>
    /// Rejects an unspecified resource kind.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Selects the public catalog index.
    /// </summary>
    Index = 1,

    /// <summary>
    /// Selects public lifecycle changes.
    /// </summary>
    Revocations = 2,

    /// <summary>
    /// Selects the allowlisted package manifest.
    /// </summary>
    Manifest = 3,

    /// <summary>
    /// Selects exact package content.
    /// </summary>
    Content = 4,

    /// <summary>
    /// Selects declared client capabilities.
    /// </summary>
    Requirements = 5
}
