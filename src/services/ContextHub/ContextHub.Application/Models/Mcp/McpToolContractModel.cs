namespace ContextHub.Application.Models.Mcp;

/// <summary>
/// Carries one authoritative tool row without exposing persistence entities.
/// </summary>
/// <param name="Name">The registered capability name.</param>
/// <param name="IsEnabled">The current global enablement.</param>
/// <param name="InputSchema">The input JSON Schema from the same row snapshot.</param>
/// <param name="OutputSchema">The paired output JSON Schema.</param>
/// <param name="Title">The current display title.</param>
/// <param name="Description">The current discovery summary.</param>
public sealed record McpToolContractModel(
    string Name,
    bool IsEnabled,
    string InputSchema,
    string OutputSchema,
    string Title = "",
    string Description = "");
