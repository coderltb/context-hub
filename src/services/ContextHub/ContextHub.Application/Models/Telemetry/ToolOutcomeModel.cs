namespace ContextHub.Application.Models.Telemetry;

/// <summary>
/// Contains only approved telemetry dimensions and the actual completed outcome.
/// </summary>
public sealed record ToolOutcomeModel
{
    /// <summary>
    /// Gets the authenticated client identity.
    /// </summary>
    public Guid ClientId { get; init; }

    /// <summary>
    /// Gets the fixed tool name.
    /// </summary>
    public string ToolName { get; init; } = "";

    /// <summary>
    /// Gets whether the tool result succeeded.
    /// </summary>
    public bool Succeeded { get; init; }

    /// <summary>
    /// Gets the optional public package identifier.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the optional bounded diagnostic family.
    /// </summary>
    public string? ClientFamily { get; init; }
}
