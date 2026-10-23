namespace ContextHub.Core.Persistence.Interfaces;

/// <summary>
/// Owns the save and transaction boundary for one service database context.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Indicates whether the service context currently owns or borrows an EF transaction.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Detaches all tracked entities, discarding pending changes without saving or rolling back a transaction.
    /// </summary>
    void ClearTrackedChanges();

    /// <summary>
    /// Persists the changes currently staged in the service-owned context.
    /// </summary>
    /// <param name="cancellationToken">Stops the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes work in a transaction owned by this unit of work when no outer transaction exists.
    /// </summary>
    /// <remarks>
    /// An owned transaction is saved and committed on success. An existing outer transaction remains
    /// owned by its caller and is neither saved nor committed automatically.
    /// Only the final owned save defers tracker acceptance until commit. A host-selected execution strategy
    /// may replay the callback; it must handle its own explicit saves and non-database side effects safely.
    /// A connection failure during commit can leave the database outcome unknown; exactly-once execution is not guaranteed.
    /// </remarks>
    /// <param name="action">The work to execute.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>A task that completes after the action and any locally owned save/commit; caller-owned transactions remain open.</returns>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes work that returns a value in a transaction owned by this unit of work when needed.
    /// </summary>
    /// <remarks>
    /// Owned final-save changes are accepted only after commit. Host-selected retries may replay the callback;
    /// its explicit saves and external side effects remain caller-owned, and an ambiguous commit is not exactly-once.
    /// </remarks>
    /// <typeparam name="TResult">The value returned by the operation.</typeparam>
    /// <param name="action">The work to execute.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The result produced by the operation.</returns>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default);
}
