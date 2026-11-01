namespace ContextHub.Application.Interfaces.Telemetry;

/// <summary>
/// Defines an Application-owned port for privacy-safe feature operations.
/// </summary>
public interface IUsageTelemetryService
{
    /// <summary>
    /// Atomically records the actual completed tool outcome.
    /// </summary>
    /// <param name="outcome">The approved outcome input.</param>
    /// <param name="cancellationToken">Cancels the owned database or coordination operation.</param>
    /// <returns>The completed operation or approved result.</returns>
    Task RecordAsync(ToolOutcomeModel outcome, CancellationToken cancellationToken);
}
