namespace ContextHub.Application.Models.Mcp;

/// <summary>
/// Defines a fixed public capability independently of live database state.
/// </summary>
public sealed record ToolDefinitionModel
{
    /// <summary>
    /// Gets the stable leaf name.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets whether the capability is read-only.
    /// </summary>
    public bool ReadOnly { get; init; }
}
