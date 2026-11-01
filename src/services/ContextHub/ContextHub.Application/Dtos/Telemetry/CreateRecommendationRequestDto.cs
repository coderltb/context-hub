namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains only editable Owner feedback fields.
/// </summary>
public sealed record CreateRecommendationRequestDto
{
    /// <summary>
    /// Gets an optional public package identifier.
    /// </summary>
    public string? PackageId { get; init; }

    /// <summary>
    /// Gets the manual review category.
    /// </summary>
    public string RecommendationType { get; init; } = "";

    /// <summary>
    /// Gets the English review title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// Gets the English description.
    /// </summary>
    public string Description { get; init; } = "";
}
