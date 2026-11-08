namespace ContextHub.Infrastructure.Repositories.Telemetry;

/// <summary>
/// Executes bounded aggregate reads without introducing a second read model or raw event storage.
/// </summary>
/// <param name="contextHubDbContext">The service-owned scoped context.</param>
public sealed class DashboardRepository(ContextHubDbContext contextHubDbContext) : IDashboardRepository
{
    /// <summary>
    /// Aggregates only approved counters and public catalog identifiers.
    /// </summary>
    /// <param name="from">The inclusive first UTC date.</param>
    /// <param name="to">The inclusive last UTC date.</param>
    /// <param name="cancellationToken">Cancels sequential database reads.</param>
    /// <returns>The dashboard projection.</returns>
    public async Task<DashboardSummaryResponseDto> GetSummaryAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken)
    {
        var usage = contextHubDbContext.DailyUsage.AsNoTracking().Where(value => value.Date >= from && value.Date <= to);
        var totals = await usage.GroupBy(value => 1).Select(group => new
        {
            Total = group.Sum(value => value.TotalCount),
            Success = group.Sum(value => value.SuccessCount),
            Errors = group.Sum(value => value.ErrorCount)
        }).FirstOrDefaultAsync(cancellationToken);
        var tools = await usage.GroupBy(value => value.ToolName).Select(group => new DashboardToolResponseDto
        {
            ToolName = group.Key,
            TotalCount = group.Sum(value => value.TotalCount),
            SuccessCount = group.Sum(value => value.SuccessCount),
            ErrorCount = group.Sum(value => value.ErrorCount)
        }).OrderByDescending(value => value.TotalCount).ThenBy(value => value.ToolName).Take(TelemetryConstants.DASHBOARD_TOP_COUNT).ToArrayAsync(cancellationToken);
        var packageTotals = await (from item in usage
                                   join package in contextHubDbContext.Packages.AsNoTracking() on item.PackageId equals package.Id
                                   group item by new { package.Key, package.Name, package.Type } into aggregate
                                   select new
                                   {
                                       PackageId = aggregate.Key.Key,
                                       DisplayName = aggregate.Key.Name,
                                       PackageType = aggregate.Key.Type,
                                       Total = aggregate.Sum(value => value.TotalCount),
                                       Errors = aggregate.Sum(value => value.ErrorCount)
                                   }).OrderByDescending(value => value.Total).ThenBy(value => value.PackageId).Take(TelemetryConstants.DASHBOARD_TOP_COUNT).ToArrayAsync(cancellationToken);

        // One scoped DbContext executes these reads sequentially; counts are observations, not a snapshot guarantee.
        var active = await contextHubDbContext.Packages.LongCountAsync(value => value.IsActive, cancellationToken);
        var published = await contextHubDbContext.Versions.LongCountAsync(value => value.Status == CatalogConstants.PUBLISHED, cancellationToken);
        var open = await contextHubDbContext.Recommendations.LongCountAsync(
            value => value.Status == TelemetryConstants.STATUS_NEW || value.Status == TelemetryConstants.STATUS_ACCEPTED,
            cancellationToken);
        return new()
        {
            Range = new() {
                From = from,
                To = to
            },
            Usage = new() {
                TotalRequests = totals?.Total ?? 0,
                SuccessCount = totals?.Success ?? 0,
                ErrorCount = totals?.Errors ?? 0,
                ErrorRate = totals is { Total: > 0 } ? (double)totals.Errors / totals.Total : 0
            },
            Catalog = new() {
                ActivePackages = active,
                PublishedVersions = published
            },
            Recommendations = new() { OpenCount = open },
            TopTools = tools.Select(value => value with { ErrorRate = value.TotalCount > 0 ? (double)value.ErrorCount / value.TotalCount : 0 }).ToArray(),
            TopPackages = packageTotals.Select(value => new DashboardPackageResponseDto {
                PackageId = value.PackageId,
                DisplayName = value.DisplayName,
                PackageType = value.PackageType,
                TotalCount = value.Total,
                ErrorRate = value.Total > 0 ? (double)value.Errors / value.Total : 0
            }).ToArray()
        };
    }
}
