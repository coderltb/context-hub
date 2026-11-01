namespace ContextHub.Application.Interfaces.Telemetry;

/// <summary>
/// Defines atomic coordination and persistence operations without use-case policy decisions.
/// </summary>
public interface IQualitySignalService
{
    /// <summary>
    /// Increments one client's existing fixed-window submission counter.
    /// </summary>
    /// <param name="clientId">The application identity selected by the authorized handler.</param>
    /// <param name="cancellationToken">Cancels Redis coordination.</param>
    /// <returns>The atomic count for the current window.</returns>
    Task<long> IncrementBudgetAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves authoritative package metadata and atomically merges the coded signal.
    /// </summary>
    /// <param name="request">The already validated signal codes.</param>
    /// <param name="clientId">The authenticated application identity.</param>
    /// <param name="cancellationToken">Cancels PostgreSQL operations.</param>
    /// <returns>True for a new signal, or false for an existing deduplicated item.</returns>
    Task<bool> PersistAsync(QualitySignalRequestDto request, Guid clientId, CancellationToken cancellationToken);
}
