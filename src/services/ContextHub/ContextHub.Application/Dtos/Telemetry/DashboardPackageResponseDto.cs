namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardPackageResponseDto
{
    /// <summary>
    /// Gets the public package identifier.
    /// </summary>
    public string PackageId { get; init; } = "";

    /// <summary>
    /// Gets the current public display name.
    /// </summary>
    public string DisplayName { get; init; } = "";

    /// <summary>
    /// Gets the completed related count.
    /// </summary>
    public long TotalCount { get; init; }

    /// <summary>
    /// Gets the failure ratio.
    /// </summary>
    public double ErrorRate { get; init; }
}
