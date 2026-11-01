namespace ContextHub.Application.Dtos.Telemetry;

/// <summary>
/// Contains privacy-safe dashboard aggregates.
/// </summary>
public sealed record DashboardSummaryResponseDto
{
    /// <summary>
    /// Gets the applied UTC range.
    /// </summary>
    public DashboardRangeResponseDto Range { get; init; } = new();

    /// <summary>
    /// Gets aggregate tool outcomes.
    /// </summary>
    public DashboardUsageResponseDto Usage { get; init; } = new();

    /// <summary>
    /// Gets current catalog totals.
    /// </summary>
    public DashboardCatalogResponseDto Catalog { get; init; } = new();

    /// <summary>
    /// Gets current open review totals.
    /// </summary>
    public DashboardRecommendationsResponseDto Recommendations { get; init; } = new();

    /// <summary>
    /// Gets at most ten tool aggregates.
    /// </summary>
    public IReadOnlyList<DashboardToolResponseDto> TopTools { get; init; } = [];

    /// <summary>
    /// Gets at most ten public package aggregates.
    /// </summary>
    public IReadOnlyList<DashboardPackageResponseDto> TopPackages { get; init; } = [];
}
