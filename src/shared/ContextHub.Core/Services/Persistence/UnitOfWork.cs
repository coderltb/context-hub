namespace ContextHub.Core.Services.Persistence;

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
    /// Executes work using the provider's default isolation when no caller-owned transaction exists.
    /// </summary>
    /// <param name="action">The work to execute.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The callback and any locally owned save and commit.</returns>
    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(action, isolationLevel: null, cancellationToken);

    /// <summary>
    /// Executes work at an explicit isolation level without taking ownership of an existing matching transaction.
    /// </summary>
    /// <param name="action">The work to execute.</param>
    /// <param name="isolationLevel">The native isolation required before callback execution.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The callback and any locally owned save and commit.</returns>
    public Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(action, (IsolationLevel?)isolationLevel, cancellationToken);

    /// <summary>
    /// Executes value-returning work using the provider default when this unit of work owns the transaction.
    /// </summary>
    /// <typeparam name="TResult">The value returned by the operation.</typeparam>
    /// <param name="action">The work to execute.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The value produced after the callback and any locally owned save and commit.</returns>
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionCoreAsync(action, isolationLevel: null, cancellationToken);

    /// <summary>
    /// Executes value-returning work at the requested native isolation or borrows an exactly matching transaction.
    /// </summary>
    /// <typeparam name="TResult">The value returned by the operation.</typeparam>
    /// <param name="action">The work to execute.</param>
    /// <param name="isolationLevel">The native isolation required before callback execution.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The value produced after the callback and any locally owned save and commit.</returns>
    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        IsolationLevel isolationLevel,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionCoreAsync(action, isolationLevel, cancellationToken);

    /// <summary>
    /// Adapts both result-free overloads to the same transaction implementation without capturing a missing callback.
    /// </summary>
    /// <param name="action">The work to execute.</param>
    /// <param name="isolationLevel">The requested native isolation, or null for the provider default.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The callback and any locally owned save and commit.</returns>
    private Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        return ExecuteInTransactionCoreAsync(
            async token =>
            {
                await action(token);

                return true;
            },
            isolationLevel,
            cancellationToken);
    }

    /// <summary>
    /// Shares isolation validation and native execution-strategy ownership across all transaction overloads.
    /// </summary>
    /// <typeparam name="TResult">The callback result.</typeparam>
    /// <param name="action">The work to execute.</param>
    /// <param name="isolationLevel">The requested native isolation, or null for the provider default.</param>
    /// <param name="cancellationToken">Stops the operation.</param>
    /// <returns>The callback result after any locally owned save and commit.</returns>
    private async Task<TResult> ExecuteInTransactionCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        IsolationLevel? isolationLevel,
        CancellationToken cancellationToken)
    {
        // Reject a missing callback before inspecting transaction state or allocating database resources.
        ArgumentNullException.ThrowIfNull(action);

        var currentTransaction = dbContext.Database.CurrentTransaction;

        if (currentTransaction is not null)
        {
            // An explicit contract cannot be satisfied by silently borrowing a different isolation level.
            if (isolationLevel is { } requiredIsolation &&
                currentTransaction.GetDbTransaction().IsolationLevel != requiredIsolation)
            {
                logger.LogWarning(
                    CoreLogConstants.TransactionLogs.ISOLATION_CONFLICT,
                    typeof(TContext).Name,
                    requiredIsolation,
                    currentTransaction.GetDbTransaction().IsolationLevel);

                throw new InvalidOperationException(CoreErrorConstants.DatabaseErrors.TRANSACTION_ISOLATION_CONFLICT);
            }

            logger.LogDebug(CoreLogConstants.TransactionLogs.OUTER_TRANSACTION_REUSED, typeof(TContext).Name);

            // The caller that opened the transaction also owns its save, commit and rollback decisions.
            return await action(cancellationToken);
        }

        // Let EF's configured strategy wrap the complete owned transaction rather than retrying isolated writes.
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(
            async token =>
            {
                var transaction = isolationLevel is { } requestedIsolation
                    ? await dbContext.Database.BeginTransactionAsync(requestedIsolation, token)
                    : await dbContext.Database.BeginTransactionAsync(token);
                var operationFailed = true;

                try
                {
                    logger.LogDebug(CoreLogConstants.TransactionLogs.STARTED, typeof(TContext).Name);

                    // Keep pending states until commit succeeds: a native retry reuses this context after rollback.
                    var result = await action(token);
                    await SaveChangesAsync(acceptAllChangesOnSuccess: false, token);
                    await transaction.CommitAsync(token);
                    dbContext.ChangeTracker.AcceptAllChanges();
                    logger.LogDebug(CoreLogConstants.TransactionLogs.COMMITTED, typeof(TContext).Name);
                    operationFailed = false;

                    return result;
                }
                catch (Exception exception)
                {
                    // Record the original failure before rollback can fail independently.
                    logger.Log(
                        exception is OperationCanceledException ? LogLevel.Debug : LogLevel.Error,
                        CoreLogConstants.TransactionLogs.OPERATION_FAILED,
                        nameof(ExecuteInTransactionAsync),
                        typeof(TContext).Name,
                        transaction.TransactionId,
                        exception.GetType().Name);

                    try
                    {
                        // Cancellation may already be requested, but rollback must still be attempted.
                        await transaction.RollbackAsync(CancellationToken.None);
                        logger.LogWarning(CoreLogConstants.TransactionLogs.ROLLED_BACK, typeof(TContext).Name, exception.GetType().Name);
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
                        // Preserve the original failure; disposal errors after successful work still propagate.
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
    private async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken)
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
