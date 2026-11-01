namespace ContextHub.Application.Models.Mcp;

/// <summary>
/// Carries the composed guidance to the native MCP adapter without SDK dependencies.
/// </summary>
/// <param name="Description">The guidance description used by the existing protocol contract.</param>
/// <param name="Text">The request-local message; it is never stored.</param>
public sealed record McpPromptMessageModel(string Description, string Text);
