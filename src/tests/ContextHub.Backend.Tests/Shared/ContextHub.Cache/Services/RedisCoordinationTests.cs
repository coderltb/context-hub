namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.Services;

/// <summary>
/// Checks shared Redis coordination against a disposable container, never the product cache.
/// </summary>
public sealed class RedisCoordinationTests : IAsyncLifetime
{
    /// <summary>
    /// Owns isolated Redis infrastructure and no product data.
    /// </summary>
    private readonly RedisContainer _redis = new RedisBuilder("redis:7-alpine").Build();

    /// <summary>
    /// Starts test-only Redis.
    /// </summary>
    /// <returns>The asynchronous startup operation.</returns>
    public Task InitializeAsync() => _redis.StartAsync();

    /// <summary>
    /// Releases the disposable Redis container.
    /// </summary>
    /// <returns>The asynchronous cleanup operation.</returns>
    public Task DisposeAsync() => _redis.DisposeAsync().AsTask();

    /// <summary>
    /// Uses one multiplexer for native cache, atomic operations and distributed locking.
    /// </summary>
    /// <returns>The asynchronous DI and atomicity assertions.</returns>
    [Fact]
    public async Task RedisRegistration_ReusesOneConnectionAndAtomicCommands()
    {
        var services = new ServiceCollection().AddLogging()
            .AddRedisInfrastructure(options => options.Configuration = _redis.GetConnectionString());
        await using var provider = services.BuildServiceProvider();
        var connection = provider.GetRequiredService<IConnectionMultiplexer>();
        var nativeOptions = provider.GetRequiredService<IOptions<Microsoft.Extensions.Caching.StackExchangeRedis.RedisCacheOptions>>().Value;
        Assert.Same(connection, await nativeOptions.ConnectionMultiplexerFactory!());
        var atomic = provider.GetRequiredService<IAtomicCacheService>();
        Assert.IsType<AtomicCacheService>(atomic);
        Assert.IsType<DistributedLockService>(provider.GetRequiredService<IDistributedLockService>());

        var reservations = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => atomic.TrySetIfAbsentAsync("reserve", "value", TimeSpan.FromSeconds(5))));
        Assert.Single(reservations, value => value);
        var counts = await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => atomic.IncrementAsync("count", TimeSpan.FromSeconds(5))));
        Assert.Equal(Enumerable.Range(1, 50).Select(value => (long)value), counts.Order());

        var ttl = await connection.GetDatabase().KeyTimeToLiveAsync("count");
        Assert.True(ttl > TimeSpan.Zero && ttl <= TimeSpan.FromSeconds(5));
        await atomic.IncrementAsync("count", TimeSpan.FromHours(1));
        Assert.True(await connection.GetDatabase().KeyTimeToLiveAsync("count") <= ttl);
        await atomic.RemoveAsync("reserve");
        Assert.True(await atomic.TrySetIfAbsentAsync("reserve", "new", TimeSpan.FromSeconds(5)));

        var native = provider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        await native.SetAsync("hybrid-check", [1], new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions
            { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
        Assert.Equal(new byte[] { 1 }, await native.GetAsync("hybrid-check"));
    }

    /// <summary>
    /// Keeps a lock exclusive beyond its initial lease and releases it through the native handle.
    /// </summary>
    /// <returns>The asynchronous renewal and release assertions.</returns>
    [Fact]
    public async Task Lock_ContentionRenewalAndRelease_AreDistinct()
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var service = new DistributedLockService(connection, NullLogger<DistributedLockService>.Instance);
        var handle = await service.TryAcquireAsync("work", TimeSpan.FromSeconds(2));
        Assert.NotNull(handle);
        await using (handle)
        {
            Assert.False(handle.HandleLostToken.IsCancellationRequested);
            Assert.Null(await service.TryAcquireAsync("work", TimeSpan.FromSeconds(2)));
            // This delay crosses the initial lease boundary deliberately, rather than guessing provider outage timing.
            await Task.Delay(TimeSpan.FromSeconds(3));
            Assert.False(handle.HandleLostToken.IsCancellationRequested);
            Assert.Null(await service.TryAcquireAsync("work", TimeSpan.FromSeconds(2)));
        }

        await using var released = await service.TryAcquireAsync("work", TimeSpan.FromSeconds(1));
        Assert.NotNull(released);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.TryAcquireAsync("cancelled", TimeSpan.FromSeconds(1), new CancellationToken(true)));
    }

    /// <summary>
    /// Propagates an observed provider failure instead of silently switching to memory.
    /// </summary>
    /// <returns>The asynchronous failure assertions.</returns>
    [Fact]
    public async Task RedisFailure_AfterObservedDisconnect_DoesNotFallback()
    {
        var options = ConfigurationOptions.Parse(_redis.GetConnectionString());
        options.AsyncTimeout = 500;
        options.SyncTimeout = 500;
        options.ConnectTimeout = 500;
        options.BacklogPolicy = BacklogPolicy.FailFast;
        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var atomic = new AtomicCacheService(connection, NullLogger<AtomicCacheService>.Instance);
        var locks = new DistributedLockService(connection, NullLogger<DistributedLockService>.Instance);
        await _redis.StopAsync();

        // A real command observes the outage; IsConnected alone can retain its last state until network activity detects it.
        var atomicError = await Record.ExceptionAsync(() => atomic.IncrementAsync("count", TimeSpan.FromSeconds(5)));
        Assert.True(atomicError is RedisConnectionException or RedisTimeoutException,
            $"Expected a Redis connection or timeout failure, received {atomicError?.GetType().Name ?? "no exception"}.");

        // Keep the native acquisition budget above the configured command timeout, so this checks failure rather than budget exhaustion.
        var error = await Assert.ThrowsAsync<DistributedLockUnavailableException>(() => locks.TryAcquireAsync("work", TimeSpan.FromSeconds(20)));
        Assert.NotNull(error.InnerException);
    }

    /// <summary>
    /// Exposes native lease loss when the owned Redis key is removed externally.
    /// </summary>
    /// <returns>The loss notification and native disposal assertions.</returns>
    [Fact]
    public async Task LockLease_ReportsLossThroughNativeToken()
    {
        using var connection = await ConnectionMultiplexer.ConnectAsync(_redis.GetConnectionString());
        var service = new DistributedLockService(connection, NullLogger<DistributedLockService>.Instance);
        await using var lease = await service.TryAcquireAsync("lost-lease", TimeSpan.FromSeconds(1));
        Assert.NotNull(lease);
        var lost = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = lease.HandleLostToken.Register(() => lost.TrySetResult());

        await connection.GetDatabase().KeyDeleteAsync("lost-lease");
        await lost.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(lease.HandleLostToken.IsCancellationRequested);
    }

    /// <summary>
    /// Treats insufficient native acquisition time as not-acquired, not proof of contention or provider failure.
    /// </summary>
    /// <returns>The delayed Redis acquisition assertion.</returns>
    [Fact]
    public async Task Lock_AcquisitionBudgetCanExpireWithoutContention()
    {
        // CLIENT PAUSE is admin-only and affects only this test's disposable Redis container.
        var options = ConfigurationOptions.Parse(_redis.GetConnectionString());
        options.AllowAdmin = true;
        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var service = new DistributedLockService(connection, NullLogger<DistributedLockService>.Instance);
        await connection.GetDatabase().ExecuteAsync("CLIENT", "PAUSE", "800", "WRITE");

        // The lock has a roughly 200ms native validity budget; writes resume before Redis command timeout.
        var lease = await service.TryAcquireAsync("slow-acquisition", TimeSpan.FromSeconds(2));

        Assert.Null(lease);
    }

    /// <summary>
    /// Preserves cancellation while native acquisition is pending, rather than wrapping it as an outage.
    /// </summary>
    /// <returns>The pending acquisition cancellation assertion.</returns>
    [Fact]
    public async Task Lock_PendingAcquisitionPreservesCancellation()
    {
        // Permit controlled write suspension only on the isolated fixture, never on product Redis.
        var options = ConfigurationOptions.Parse(_redis.GetConnectionString());
        options.AllowAdmin = true;
        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var service = new DistributedLockService(connection, NullLogger<DistributedLockService>.Instance);
        await connection.GetDatabase().ExecuteAsync("CLIENT", "PAUSE", "1000", "WRITE");
        using var cancellation = new CancellationTokenSource();
        var pending = service.TryAcquireAsync("cancel-pending", TimeSpan.FromSeconds(20), cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

}
