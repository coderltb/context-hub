namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Describes a fixed related resource without exposing routing internals.
/// </summary>
/// <param name="Uri">The resource URI or URI template.</param>
/// <param name="Name">The discovery label.</param>
public sealed record McpRelatedResourceResponseDto(string Uri, string Name);
