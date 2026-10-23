namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.Services;

/// <summary>
/// Checks memory-mode atomicity and expiry without presenting it as Redis evidence.
/// </summary>
public sealed class AtomicCacheServiceTests
{
    /// <summary>
    /// Matches Redis numeric-counter failure semantics without replacing invalid values.
    /// </summary>
    /// <returns>The asynchronous counter assertions.</returns>
    [Fact]
    public async Task InvalidCounter_DoesNotSilentlyReplaceItsValue()
    {
        var cache = new InMemoryAtomicCacheService(TimeProvider.System, NullLogger<InMemoryAtomicCacheService>.Instance);
        await cache.TrySetIfAbsentAsync("counter", long.MaxValue.ToString(global::System.Globalization.CultureInfo.InvariantCulture), TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<OverflowException>(() => cache.IncrementAsync("counter", TimeSpan.FromMinutes(1)));
        Assert.False(await cache.TrySetIfAbsentAsync("counter", "new", TimeSpan.FromMinutes(1)));

        await cache.RemoveAsync("counter");
        await cache.TrySetIfAbsentAsync("counter", "not-a-number", TimeSpan.FromMinutes(1));
        await Assert.ThrowsAsync<FormatException>(() => cache.IncrementAsync("counter", TimeSpan.FromMinutes(1)));
    }

    /// <summary>
    /// Allows exactly one reservation and serializes concurrent increments.
    /// </summary>
    /// <returns>The asynchronous concurrency assertions.</returns>
    [Fact]
    public async Task ConcurrentOperations_DoNotLoseUpdates()
    {
        var cache = new InMemoryAtomicCacheService(TimeProvider.System, NullLogger<InMemoryAtomicCacheService>.Instance);
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiting = 0;
        var workers = Enumerable.Range(0, 100).Select(_ => Task.Run(async () =>
        {
            // Queue real workers before releasing the shared start gate; memory calls complete synchronously.
            if (Interlocked.Increment(ref waiting) == 100)
            {
                ready.SetResult();
            }

            await start.Task;
            var reserved = await cache.TrySetIfAbsentAsync("reservation", "value", TimeSpan.FromMinutes(1));
            var count = await cache.IncrementAsync("counter", TimeSpan.FromMinutes(1));

            return (Reserved: reserved, Count: count);
        })).ToArray();

        await ready.Task.WaitAsync(TimeSpan.FromSeconds(10));
        start.SetResult();
        var results = await Task.WhenAll(workers);
        var reservations = results.Select(result => result.Reserved);
        Assert.Single(reservations, value => value);

        var counts = results.Select(result => result.Count);
        Assert.Equal(Enumerable.Range(1, 100).Select(value => (long)value), counts.Order());
    }

    /// <summary>
    /// Keeps the original TTL rather than renewing it on every increment.
    /// </summary>
    /// <returns>The asynchronous expiry assertions.</returns>
    [Fact]
    public async Task Expiration_IsFixedAndFreshReservationsSurviveSweeps()
    {
        var clock = new ManualClock();
        var cache = new InMemoryAtomicCacheService(clock, NullLogger<InMemoryAtomicCacheService>.Instance);
        Assert.Equal(1, await cache.IncrementAsync("counter", TimeSpan.FromSeconds(10)));
        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.Equal(2, await cache.IncrementAsync("counter", TimeSpan.FromHours(1)));
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(1, await cache.IncrementAsync("counter", TimeSpan.FromHours(1)));

        // Exercise cleanup while repeatedly replacing expired reservations; a stale sweep must not remove the replacement.
        for (var index = 0; index < 200; index++)
        {
            await cache.RemoveAsync("reservation");
            Assert.True(await cache.TrySetIfAbsentAsync("reservation", "fresh", TimeSpan.FromMinutes(1)));
            Assert.False(await cache.TrySetIfAbsentAsync("reservation", "other", TimeSpan.FromMinutes(1)));
        }
    }

    /// <summary>
    /// Rejects invalid expiry and preserves cancellation before altering entries.
    /// </summary>
    /// <returns>The asynchronous validation assertions.</returns>
    [Fact]
    public async Task Guards_RejectInvalidLifetimeAndCancellation()
    {
        var cache = new InMemoryAtomicCacheService(TimeProvider.System, NullLogger<InMemoryAtomicCacheService>.Instance);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => cache.IncrementAsync("key", TimeSpan.Zero));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => cache.RemoveAsync(" "));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cache.IncrementAsync("key", TimeSpan.FromSeconds(1), new CancellationToken(true)));
    }

    /// <summary>
    /// Supplies controlled UTC time without sleeping.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        /// <summary>
        /// Stores the simulated UTC instant.
        /// </summary>
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-10-06T00:00:00Z", global::System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>
        /// Returns the current simulated instant.
        /// </summary>
        /// <returns>The controlled UTC instant.</returns>
        public override DateTimeOffset GetUtcNow() => _now;

        /// <summary>
        /// Advances the fixed test clock.
        /// </summary>
        /// <param name="amount">The elapsed duration.</param>
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
