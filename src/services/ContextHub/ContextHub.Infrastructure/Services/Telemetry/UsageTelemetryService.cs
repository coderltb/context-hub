namespace ContextHub.Infrastructure.Services.Telemetry;

/// <summary>
/// Accepts only bounded telemetry metadata; the caller owns whether diagnostic failure may affect its operation.
/// </summary>
/// <param name="usageTelemetryRepository">The native aggregate persistence boundary.</param>
public sealed class UsageTelemetryService(IUsageTelemetryRepository usageTelemetryRepository) : IUsageTelemetryService
{
    /// <summary>
    /// Records the completed tool result rather than an HTTP transport status.
    /// </summary>
    /// <param name="outcome">The authenticated bounded outcome.</param>
    /// <param name="cancellationToken">Bounds the awaited diagnostic work.</param>
    /// <returns>The completed write.</returns>
    public Task RecordAsync(
        ToolOutcomeModel outcome,
        CancellationToken cancellationToken)
    {
        // Bound diagnostic dimensions before persistence without treating client metadata as an access decision.
        if (!ToolCatalogConstants.TOOLS.Any(value => value.Name == outcome.ToolName) || outcome.ClientId == Guid.Empty ||
            outcome.ClientFamily?.Length > TelemetryConstants.MAX_CLIENT_FAMILY_LENGTH)
        {
            throw new ArgumentException(TelemetryErrorConstants.INVALID_DIMENSIONS_MESSAGE, nameof(outcome));
        }

        return usageTelemetryRepository.RecordAsync(outcome, cancellationToken);
    }
}
