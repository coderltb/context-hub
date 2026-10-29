namespace ContextHub.Core.Helpers.Persistence;

/// <summary>
/// Inspects PostgreSQL error metadata without interpreting messages or choosing business errors.
/// </summary>
public static class PostgreSqlExceptionHelper
{
    /// <summary>
    /// Reads the constraint name only for a direct or EF-wrapped PostgreSQL unique violation.
    /// </summary>
    /// <param name="exception">The optional database exception.</param>
    /// <returns>The reported unique constraint name, or null for any other error.</returns>
    public static string? GetUniqueConstraintName(Exception? exception)
    {
        // EF wraps provider errors once; do not walk unrelated exception chains or parse localized messages.
        var providerException = exception as PostgresException ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return providerException?.SqlState == PostgresErrorCodes.UniqueViolation ? providerException.ConstraintName : null;
    }
}
