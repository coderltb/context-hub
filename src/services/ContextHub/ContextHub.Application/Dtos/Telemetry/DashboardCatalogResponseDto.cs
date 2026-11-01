namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardCatalogResponseDto
{
    /// <summary>
    /// Gets the current active package count.
    /// </summary>
    public long ActivePackages { get; init; }

    /// <summary>
    /// Gets the current published version count.
    /// </summary>
    public long PublishedVersions { get; init; }
}
