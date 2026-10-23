namespace ContextHub.Core.Interfaces.Concurrency;

/// <summary>
/// Owns lock release and exposes native lease-loss notification without a second renewal engine.
/// </summary>
public interface IDistributedLockLease : IAsyncDisposable
{
    /// <summary>
    /// Gets a token cancelled when the provider can no longer maintain ownership.
    /// </summary>
    CancellationToken HandleLostToken { get; }
}
