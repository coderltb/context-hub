namespace ContextHub.Infrastructure.Helpers.Persistence;

/// <summary>
/// Recognizes native PostgreSQL availability failures without reclassifying SQL defects or transaction conflicts.
/// </summary>
public static class DatabaseExceptionHelper
{
    /// <summary>
    /// Matches only known Npgsql failures and the specific wrappers produced by EF and its native execution strategy.
    /// </summary>
    /// <param name="exception">The original boundary failure.</param>
    /// <param name="cancellationToken">The caller token; cancellation is never a dependency outage.</param>
    /// <returns>Whether the original failure represents a transient unavailable database.</returns>
    public static bool IsUnavailable(
        Exception exception,
        CancellationToken cancellationToken)
    {
        var provider = exception switch
        {
            NpgsqlException native => native,
            DbUpdateException { InnerException: NpgsqlException native } => native,
            InvalidOperationException { InnerException: NpgsqlException native } => native,
            InvalidOperationException { InnerException: DbUpdateException { InnerException: NpgsqlException native } } => native,
            _ => null
        };

        // Native serialization and deadlock decisions belong to transaction concurrency handling, not availability.
        return !cancellationToken.IsCancellationRequested && provider is { IsTransient: true } &&
            provider is not PostgresException { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected };
    }

    /// <summary>
    /// Recognizes the exact native serialization wrappers produced at the catalog transaction boundary.
    /// </summary>
    /// <param name="exception">The original failed transaction operation.</param>
    /// <returns>Whether PostgreSQL aborted the operation for a serialization conflict.</returns>
    public static bool IsSerializationFailure(Exception exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } or
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } } or
            InvalidOperationException { InnerException: DbUpdateException
                { InnerException: PostgresException { SqlState: PostgresErrorCodes.SerializationFailure } } };

}
