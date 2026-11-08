namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Shows the current stored schemas and related resources to an authorized Owner.
/// </summary>
public sealed record McpToolDetailResponseDto : McpToolResponseDto
{
    /// <summary>
    /// Gets the current input contract read with the tool metadata.
    /// </summary>
    public JsonObject InputSchema { get; init; } = new();

    /// <summary>
    /// Gets the current output contract read with the tool metadata.
    /// </summary>
    public JsonObject OutputSchema { get; init; } = new();

    /// <summary>
    /// Gets the fixed resource routes governed by this capability.
    /// </summary>
    public IReadOnlyList<McpRelatedResourceResponseDto> Resources { get; init; } = [];
}
