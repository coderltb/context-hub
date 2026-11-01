namespace ContextHub.Application.Exceptions.Mcp;

/// <summary>
/// Reports incomplete authoritative tool state without exposing database details.
/// </summary>
public sealed class ToolStateUnavailableException() : DependencyUnavailableException("tool_state_unavailable");
