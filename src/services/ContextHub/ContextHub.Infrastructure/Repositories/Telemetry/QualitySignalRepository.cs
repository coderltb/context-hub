namespace ContextHub.Infrastructure.Repositories.Telemetry;

/// <summary>
/// Keeps published-package authority and atomic client-signal deduplication inside persistence.
/// </summary>
/// <param name="contextHubDbContext">The scoped catalog authority.</param>
/// <param name="dapperService">The shared parameterized atomic command boundary.</param>
public sealed class QualitySignalRepository(ContextHubDbContext contextHubDbContext, IDapperService dapperService) : IQualitySignalRepository
{
    /// <summary>
    /// Verifies an optional exact published version and workflow step before deriving feedback.
    /// </summary>
    /// <param name="request">The validated fixed-code signal.</param>
    /// <param name="cancellationToken">Cancels the authoritative read.</param>
    /// <returns>The internal package identity, or null for a coverage gap.</returns>
    public async Task<Guid?> ResolvePackageAsync(
        QualitySignalRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.PackageId is null)
        {
            return null;
        }

        var version = await (from release in contextHubDbContext.Versions.AsNoTracking()
                             join package in contextHubDbContext.Packages.AsNoTracking() on release.PackageId equals package.Id
                             where package.Key == request.PackageId && package.IsActive && release.Number == request.Version &&
                                   (release.Status == CatalogConstants.PUBLISHED || release.Status == CatalogConstants.DEPRECATED)
                             select new { package.Id, release.Bundle }).FirstOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException(
                TelemetryErrorConstants.QUALITY_SIGNAL_PACKAGE_UNAVAILABLE,
                "The referenced published package version is unavailable.");
        if (request.WorkflowStepId is not null)
        {
            using var document = JsonDocument.Parse(version.Bundle);
            if (!document.RootElement.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array ||
                !steps.EnumerateArray().Any(step => step.TryGetProperty("id", out var id) && id.GetString() == request.WorkflowStepId))
            {
                throw new BusinessException(
                    TelemetryErrorConstants.QUALITY_SIGNAL_STEP_INVALID,
                    "The workflow step is not present in the published bundle.");
            }
        }

        return version.Id;
    }

    /// <summary>
    /// Performs one atomic insert-or-merge without retrying a potentially committed write.
    /// </summary>
    /// <param name="request">The fixed coded signal.</param>
    /// <param name="clientId">The authenticated application identity, never a request field.</param>
    /// <param name="packageId">The verified optional internal package.</param>
    /// <param name="cancellationToken">Cancels command dispatch.</param>
    /// <returns>Whether the signal created a new queue item.</returns>
    public async Task<bool> UpsertAsync(
        QualitySignalRequestDto request,
        Guid clientId,
        Guid? packageId,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var context = new
        {
            request.SignalType, request.IssueCode, request.ArtifactType, request.Operation,
            request.Capability, request.Version, request.WorkflowStepId, request.DesiredPackageType
        };
        var fingerprint = JsonSerializer.SerializeToUtf8Bytes(new object?[]
        {
            clientId.ToString("N"), request.SignalType, request.IssueCode, request.ArtifactType, request.Operation,
            request.PackageId, request.Version, request.WorkflowStepId, request.DesiredPackageType, request.Capability
        });
        var type = request.SignalType switch { "coverage_gap" => TelemetryConstants.SEARCH_GAP, "package_issue" => TelemetryConstants.PACKAGE_IMPROVEMENT, _ => TelemetryConstants.COMPATIBILITY_GAP };
        var inserted = await dapperService.ExecuteScalarAsync<bool?>(
            TelemetrySqlConstants.UPSERT_CLIENT_SIGNAL,
            new
        {
            Id = Guid.CreateVersion7(), PackageId = packageId, Type = type,
            DedupeKey = Convert.ToHexStringLower(SHA256.HashData(fingerprint)),
            Title = "Unverified client report: " + request.IssueCode.Replace('_', ' '),
            Description = "A client reported " + request.IssueCode + " for " + request.ArtifactType + "/" + request.Operation + ". Owner verification is required.",
            Context = JsonSerializer.Serialize(context, JsonSerializerOptions.Web),
            request.Version, Now = now
        },
            new DapperCommandOptions { CancellationToken = cancellationToken });

        // A lifecycle change between the earlier lookup and INSERT SELECT must not create stale feedback.
        return inserted ?? throw new BusinessException(
            TelemetryErrorConstants.QUALITY_SIGNAL_PACKAGE_UNAVAILABLE,
            "The referenced published package version is unavailable.");
    }
}
