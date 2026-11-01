namespace ContextHub.Infrastructure.Repositories.Telemetry;

/// <summary>
/// Writes bounded daily counters and idempotent reliability feedback with native PostgreSQL statements.
/// </summary>
/// <param name="contextHubDbContext">The scoped service context.</param>
public sealed class UsageTelemetryRepository(ContextHubDbContext contextHubDbContext) : IUsageTelemetryRepository
{
    /// <summary>
    /// Atomically increments the existing nullable dimension key and evaluates daily reliability.
    /// </summary>
    /// <param name="outcome">The approved actual tool outcome.</param>
    /// <param name="cancellationToken">Cancels native database work.</param>
    /// <returns>The completed counter observation.</returns>
    public async Task RecordAsync(
        ToolOutcomeModel outcome,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var packageId = outcome.PackageId is null ? null : await contextHubDbContext.Packages.AsNoTracking()
            .Where(value => value.Key == outcome.PackageId).Select(value => (Guid?)value.Id).FirstOrDefaultAsync(cancellationToken);
        var success = outcome.Succeeded ? 1L : 0L;
        var failure = outcome.Succeeded ? 0L : 1L;

        // NULLS NOT DISTINCT on the reviewed key makes the increment atomic even for global or unknown-family calls.
        await contextHubDbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO telemetry."DailyUsage"
                ("Date","ToolName","PackageId","ClientId","ClientFamily","TotalCount","SuccessCount","ErrorCount","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
            VALUES ({date},{outcome.ToolName},{packageId},{outcome.ClientId},{outcome.ClientFamily},1,{success},{failure},{now},NULL,{now},NULL)
            ON CONFLICT ("Date","ToolName","PackageId","ClientId","ClientFamily")
            DO UPDATE SET "TotalCount"=telemetry."DailyUsage"."TotalCount"+1,
                          "SuccessCount"=telemetry."DailyUsage"."SuccessCount"+EXCLUDED."SuccessCount",
                          "ErrorCount"=telemetry."DailyUsage"."ErrorCount"+EXCLUDED."ErrorCount",
                          "UpdatedAt"=GREATEST(telemetry."DailyUsage"."UpdatedAt",EXCLUDED."UpdatedAt");
            """,
            cancellationToken);
        var totals = await contextHubDbContext.DailyUsage.AsNoTracking().Where(value => value.Date == date && value.PackageId == packageId)
            .GroupBy(value => 1).Select(group => new { Total = group.Sum(value => value.TotalCount), Errors = group.Sum(value => value.ErrorCount) })
            .FirstAsync(cancellationToken);
        if (totals.Total < TelemetryConstants.RELIABILITY_MINIMUM_COUNT || (double)totals.Errors / totals.Total < TelemetryConstants.RELIABILITY_FAILURE_RATIO)
        {
            return;
        }

        // A stable primary key deduplicates this UTC day and package across clients without reusing the client-signal key.
        var identity = "context-hub:reliability:v1:" + date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ":" + (packageId?.ToString("N") ?? "global");
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
        await contextHubDbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO recommendation."Recommendation"
                ("Id","PackageId","Type","Origin","DedupeKey","Title","Description","Context","Status","SignalCount",
                 "LastSeenAt","CreatedAt","CreatedBy","UpdatedAt","UpdatedBy")
            VALUES ({id},{packageId},'reliability_issue','system_reliability',NULL,'Elevated tool failure rate',
                    'The daily tool outcome aggregate reached the reliability review threshold.',NULL,'new',1,
                    {now},{now},NULL,{now},NULL)
            ON CONFLICT ("Id") DO NOTHING;
            """,
            cancellationToken);
    }
}
