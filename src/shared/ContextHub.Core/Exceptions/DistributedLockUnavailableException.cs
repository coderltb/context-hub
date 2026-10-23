namespace ContextHub.Core.Exceptions;

/// <summary>
/// Reports a native provider exception without treating every not-acquired result as contention or an outage.
/// </summary>
public sealed class DistributedLockUnavailableException : Exception
{
    /// <summary>
    /// Preserves the original infrastructure failure for internal diagnosis.
    /// </summary>
    /// <param name="innerException">The original provider failure, never serialized into a public response.</param>
    public DistributedLockUnavailableException(Exception innerException)
        : base(CoreErrorConstants.ConcurrencyErrors.LOCK_UNAVAILABLE, innerException)
    {
    }
}
