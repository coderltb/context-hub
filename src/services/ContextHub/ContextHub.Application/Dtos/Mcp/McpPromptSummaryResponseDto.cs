namespace ContextHub.Application.Dtos.Mcp;

/// <summary>
/// Carries approved MCP configuration without persistence entities.
/// </summary>
public record McpPromptSummaryResponseDto
{
    /// <summary>
    /// Gets the database identifier used by Owner APIs.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the immutable MCP identifier.
    /// </summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Gets the optional display title.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Gets the optional discovery description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the current availability.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Gets the UTC configuration update time.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }

    /// <summary>
    /// Gets the expected version for the next update.
    /// </summary>
    public string ConcurrencyToken { get; init; } = "";
}
