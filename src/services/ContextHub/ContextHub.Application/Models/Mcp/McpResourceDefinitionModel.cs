namespace ContextHub.Application.Models.Mcp;

/// <summary>
/// Binds a fixed resource route to its native kind and required capability.
/// </summary>
/// <param name="Kind">The resource discriminator.</param>
/// <param name="Uri">The fixed URI or URI template.</param>
/// <param name="Name">The native discovery name.</param>
/// <param name="ToolName">The independently enforced tool grant.</param>
/// <param name="Leaf">The package-resource leaf, or null for fixed resources.</param>
public sealed record McpResourceDefinitionModel(
    McpResourceKind Kind,
    string Uri,
    string Name,
    string ToolName,
    string? Leaf = null);
