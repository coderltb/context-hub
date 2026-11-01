namespace ContextHub.Application.Dtos.Integration;

/// <summary>
/// Describes one currently permitted capability without exposing provider routing.
/// </summary>
public sealed record ManagedToolDescriptionResponseDto
{
    /// <summary>
    /// Gets the current opaque routing reference.
    /// </summary>
    public string ToolRef { get; init; } = "";

    /// <summary>
    /// Gets the reviewed public title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the reviewed capability description.
    /// </summary>
    public string Description { get; init; } = "";

    /// <summary>
    /// Gets the current closed input schema.
    /// </summary>
    public JsonElement InputSchema { get; init; }

    /// <summary>
    /// Gets the reviewed public result overview.
    /// </summary>
    public string OutputOverview { get; init; } = "";

    /// <summary>
    /// Gets the declared absence of side effects.
    /// </summary>
    public string SideEffect { get; init; } = "";
}

