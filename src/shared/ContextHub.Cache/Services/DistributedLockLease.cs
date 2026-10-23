namespace ContextHub.Cache.Services;

/// <summary>
/// Adapts the native Redis handle without changing renewal, loss detection or release semantics.
/// </summary>
/// <param name="handle">The acquired provider handle whose lifetime belongs to this lease.</param>
internal sealed class DistributedLockLease(RedisDistributedLockHandle handle) : IDistributedLockLease
{
    /// <summary>
    /// Gets the native notification token; observation remains lazy until the caller requests it.
    /// </summary>
    public CancellationToken HandleLostToken => handle.HandleLostToken;

    /// <summary>
    /// Releases this lease through the provider that acquired it.
    /// </summary>
    /// <returns>The native asynchronous release operation.</returns>
    public ValueTask DisposeAsync() => handle.DisposeAsync();
}
