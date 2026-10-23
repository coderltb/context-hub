namespace ContextHub.Core.Persistence.UnitOfWork;

/// <summary>
/// Implements save and transaction boundaries for a service-owned EF Core context.
/// </summary>
/// <typeparam name="TContext">The concrete context owned by the service.</typeparam>
/// <param name="dbContext">The service-owned database context.</param>
/// <param name="logger">The structured application logger.</param>
public sealed class UnitOfWork<TContext>(
    TContext dbContext,
    ILogger<UnitOfWork<TContext>> logger) : IUnitOfWork
    where TContext : DbContext
{
    /// <summary>
    /// Reports the context's current EF transaction without opening or taking ownership of one.
    /// </summary>
    public bool HasActiveTransaction => dbContext.Database.CurrentTransaction is not null;

    /// <summary>
    /// Explicitly discards tracking state without changing the database or the caller's transaction.
    /// </summary>
    public void ClearTrackedChanges()
    {
        // This is caller-controlled recovery, not an automatic rollback action: all pending changes are discarded.
        dbContext.ChangeTracker.Clear();

        logger.LogDebug(CoreLogConstants.TransactionLogs.TRACKER_CLEARED, typeof(TContext).Name);
    }

    /// <summary>
    /// Persists the changes currently staged in the service-owned context.
    /// </summary>
    /// <param name="cancellationToken">Stops the save operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        SaveChangesAsync(acceptAllChangesOnSuccess: true, cancellationToken);

    /// <summary>
    /// Executes work in an owned transaction when no caller-owned transaction exists.
    /// </summary>
    /// <param name="action">The work to execute.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>A task that completes after the action and any locally owned save/commit; caller-owned transactions remain open.</returns>
    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        await ExecuteInTransactionAsync(
            async token =>
            {
                await action(token);

                return true;
            },
            cancellationToken);
    }

    /// <summary>
    /// Executes work that returns a value in an owned transaction when needed.
    /// </summary>
    /// <typeparam name="TResult">The value returned by the operation.</typeparam>
    /// <param name="action">The work to execute.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The result produced by the operation.</returns>
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        // Reject a missing callback before inspecting transaction state or allocating database resources.
        ArgumentNullException.ThrowIfNull(action);

        if (HasActiveTransaction)
        {
            logger.LogDebug(
                CoreLogConstants.TransactionLogs.OUTER_TRANSACTION_REUSED,
                typeof(TContext).Name);

            // The caller that opened the transaction also owns its save, commit and rollback decisions.
            return await action(cancellationToken);
        }

        // Let EF's configured strategy wrap the complete owned transaction rather than retrying isolated writes.
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(
            async token =>
            {
                var transaction = await dbContext.Database.BeginTransactionAsync(token);
                var operationFailed = false;

                try
                {
                    logger.LogDebug(CoreLogConstants.TransactionLogs.STARTED, typeof(TContext).Name);

                    // Keep pending states until commit succeeds: a native retry reuses this context after rollback.
                    var result = await action(token);
                    await SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
                    await transaction.CommitAsync(token);
                    dbContext.ChangeTracker.AcceptAllChanges();
                    logger.LogDebug(CoreLogConstants.TransactionLogs.COMMITTED, typeof(TContext).Name);

                    return result;
                }
                catch (Exception exception)
                {
                    operationFailed = true;

                    try
                    {
                        // Cancellation may already be requested, but rollback must still be attempted.
                        await transaction.RollbackAsync(CancellationToken.None);
                        logger.LogWarning(
                            CoreLogConstants.TransactionLogs.ROLLED_BACK,
                            typeof(TContext).Name,
                            exception.GetType().Name);
                    }
                    catch (Exception rollbackException)
                    {
                        logger.LogError(
                            CoreLogConstants.TransactionLogs.ROLLBACK_FAILED,
                            typeof(TContext).Name,
                            rollbackException.GetType().Name);

                        // Preserve the original application or cancellation exception.
                    }

                    throw;
                }
                finally
                {
                    try
                    {
                        // Dispose only locally owned transactions, even after commit, rollback or cancellation fails.
                        await transaction.DisposeAsync();
                    }
                    catch (Exception cleanupException) when (operationFailed)
                    {
                        // A secondary cleanup failure must not replace the original exception being rethrown.
                        // Without an earlier failure this filter does not match, so disposal errors still reach the caller.
                        logger.LogError(
                            CoreLogConstants.TransactionLogs.CLEANUP_FAILED,
                            typeof(TContext).Name,
                            cleanupException.GetType().Name);
                    }
                }
            },
            cancellationToken);
    }

    /// <summary>
    /// Shares save diagnostics while letting the owning transaction defer EF acceptance until commit.
    /// </summary>
    /// <param name="acceptAllChangesOnSuccess">Whether a successful database save also accepts tracked changes.</param>
    /// <param name="cancellationToken">Cancellation forwarded to the native EF save.</param>
    /// <returns>The number of state entries written by EF.</returns>
    private async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken)
    {
        logger.LogDebug(CoreLogConstants.TransactionLogs.SAVE_STARTED, typeof(TContext).Name);

        // Preserve the public save's usual virtual overload; owned saves need EF's explicit deferred-acceptance overload.
        var count = acceptAllChangesOnSuccess
            ? await dbContext.SaveChangesAsync(cancellationToken)
            : await dbContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, cancellationToken);

        logger.LogDebug(CoreLogConstants.TransactionLogs.SAVE_COMPLETED, typeof(TContext).Name, count);

        return count;
    }
}
