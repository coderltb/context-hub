namespace ContextHub.Core.Interfaces.Concurrency;

/// <summary>
/// Coordinates cross-instance work without replacing database transactions or unique constraints.
/// </summary>
public interface IDistributedLockService
{
    /// <summary>
    /// Attempts one auto-renewed lease with zero contention wait.
    /// </summary>
    /// <param name="key">The namespaced resource key.</param>
    /// <param name="leaseDuration">A positive lease renewed while the handle is alive.</param>
    /// <param name="cancellationToken">Cancels lock acquisition.</param>
    /// <returns>An owned lease, or null when native acquisition cannot finish within its validity budget. Provider errors throw.</returns>
    Task<IDistributedLockLease?> TryAcquireAsync(string key, TimeSpan leaseDuration, CancellationToken cancellationToken = default);
}
