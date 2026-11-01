namespace ContextHub.Infrastructure.Interfaces.Telemetry;

/// <summary>
/// Defines the feature-specific persistence boundary owned by Infrastructure.
/// </summary>
public interface IUsageTelemetryRepository
{
    /// <summary>
    /// Executes the named authoritative persistence operation.
    /// </summary>
    /// <param name="outcome">The approved outcome input.</param>
    /// <param name="cancellationToken">The approved cancellationToken input.</param>
    /// <returns>The completed persistence operation or approved projection.</returns>
    Task RecordAsync(ToolOutcomeModel outcome, CancellationToken cancellationToken);
}
