namespace ContextHub.ApiCommon.Health;

/// <summary>
/// Writes the native health contract using an explicit safe-field projection, never an application envelope.
/// </summary>
public static class HealthResponseWriter
{
    /// <summary>
    /// Keeps the native health wire contract separate from application JSON customization.
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Reports process liveness without exposing individual checks or diagnostic details.
    /// </summary>
    /// <param name="context">The health HTTP request whose cancellation controls writing.</param>
    /// <param name="report">The report selected by the host's liveness predicate.</param>
    /// <returns>The native JSON response write.</returns>
    public static Task WriteLivenessAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        var healthy = report.Status == HealthStatus.Healthy;
        context.Response.StatusCode = healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;

        return context.Response.WriteAsJsonAsync(new { status = healthy ? HealthConstants.HEALTHY : HealthConstants.UNHEALTHY },
            options: JsonOptions,
            cancellationToken: context.RequestAborted);
    }

    /// <summary>
    /// Reports required probe results while refusing to treat an empty set or self-check as readiness evidence.
    /// </summary>
    /// <param name="context">The health HTTP request.</param>
    /// <param name="report">The report selected by the host's required-readiness predicate.</param>
    /// <returns>The native JSON write containing only stable names, statuses and durations.</returns>
    public static Task WriteReadinessAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        // Descriptions, exceptions and Data can contain credentials. Project fields explicitly rather than serializing entries.
        // Check names must be stable internal labels, never endpoint URLs or connection strings.
        var entries = report.Entries
            .Where(entry => entry.Key != HealthConstants.SELF)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        var healthy = entries.Length > 0 &&
            report.Status == HealthStatus.Healthy &&
            entries.All(entry => entry.Value.Status == HealthStatus.Healthy);

        context.Response.StatusCode = healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable;

        var checks = entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status == HealthStatus.Healthy ? HealthConstants.HEALTHY : HealthConstants.UNHEALTHY,
            durationMs = entry.Value.Duration.TotalMilliseconds
        });

        return context.Response.WriteAsJsonAsync(new { status = healthy ? HealthConstants.HEALTHY : HealthConstants.UNHEALTHY, checks },
            options: JsonOptions,
            cancellationToken: context.RequestAborted);
    }
}
