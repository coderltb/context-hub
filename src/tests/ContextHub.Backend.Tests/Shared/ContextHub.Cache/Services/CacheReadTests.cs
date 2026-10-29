namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.Services;

/// <summary>
/// Verifies cache-only reads through native HybridCache and disposable Redis infrastructure.
/// </summary>
public sealed class CacheReadTests
{
    /// <summary>
    /// Reads stored values and returns type defaults on misses without caching a placeholder.
    /// </summary>
    /// <returns>The local hit, miss, default-value and removal assertions.</returns>
    [Fact]
    public async Task GetAsync_ReadsStoredValuesAndDoesNotPoisonMisses()
    {
        await using var provider = CreateProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "read-only");
        var numberKey = CacheKey.Create("catalog", 1, "zero");
        var booleanKey = CacheKey.Create("catalog", 1, "false");
        var nullKey = CacheKey.Create("catalog", 1, "null");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));

        Assert.Null(await cache.GetAsync<string>(key));
        Assert.Equal(0, await cache.GetAsync<int>(numberKey));
        Assert.False(await cache.GetAsync<bool>(booleanKey));
        Assert.Null(await cache.GetAsync<int?>(numberKey));

        var calls = 0;
        var created = await cache.GetOrCreateAsync(key, _ =>
        {
            calls++;
            return ValueTask.FromResult("from-source");
        }, policy);
        await cache.SetAsync(numberKey, 0, policy);
        await cache.SetAsync(booleanKey, false, policy);
        await cache.SetAsync<string?>(nullKey, null, policy);

        Assert.Equal("from-source", created);
        Assert.Equal(1, calls);
        Assert.Equal("from-source", await cache.GetAsync<string>(key));
        Assert.Equal(0, await cache.GetAsync<int>(numberKey));
        Assert.False(await cache.GetAsync<bool>(booleanKey));
        Assert.Null(await cache.GetAsync<string?>(nullKey));

        // Default-valued entries are genuine hits, not an excuse to invoke an underlying source.
        Assert.Equal(0, await cache.GetOrCreateAsync<int>(numberKey, _ => throw new InvalidOperationException(), policy));
        Assert.False(await cache.GetOrCreateAsync<bool>(booleanKey, _ => throw new InvalidOperationException(), policy));
        Assert.Null(await cache.GetOrCreateAsync<string?>(nullKey, _ => throw new InvalidOperationException(), policy));

        await cache.RemoveAsync(key);
        Assert.Null(await cache.GetAsync<string>(key));
    }

    /// <summary>
    /// Does not join a source-producing call or prevent that call from caching its eventual value.
    /// </summary>
    /// <returns>The deterministic concurrent miss assertions.</returns>
    [Fact]
    public async Task GetAsync_DoesNotJoinAnInFlightSourceFactory()
    {
        await using var provider = CreateProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "in-flight");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var creationOperation = cache.GetOrCreateAsync<string>(key, async token =>
        {
            started.TrySetResult();
            await release.Task.WaitAsync(token);

            return "created";
        }, policy);
        var creation = creationOperation.AsTask();

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var readOperation = cache.GetAsync<string>(key);
            Assert.Null(await readOperation.AsTask().WaitAsync(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            // Release the real factory even after a failed assertion so no test-owned work remains pending.
            release.TrySetResult();
            await creation.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.Equal("created", await cache.GetAsync<string>(key));
    }

    /// <summary>
    /// Keeps host-level local read bypass effective without changing host options or removing the entry.
    /// </summary>
    /// <returns>The configured bypass and retained-value assertions.</returns>
    [Fact]
    public async Task GetAsync_PreservesHostReadFlagsWithoutMutatingOptions()
    {
        await using var provider = CreateProvider(defaultFlags: HybridCacheEntryFlags.DisableLocalCacheRead);
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "default-flags");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
        await cache.SetAsync(key, "stored", policy);

        Assert.Null(await cache.GetAsync<string>(key));
        Assert.Equal(HybridCacheEntryFlags.DisableLocalCacheRead,
            provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value.DefaultEntryOptions!.Flags);

        // Explicit native options can still read the stored entry, proving that bypass did not invalidate it.
        Assert.Equal("stored", await provider.GetRequiredService<HybridCache>().GetOrCreateAsync<string>(
            key.Value, _ => throw new InvalidOperationException(), new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.None }));
    }

    /// <summary>
    /// Leaves absolute local expiration unchanged even when the entry is read repeatedly.
    /// </summary>
    /// <returns>The real expiration assertions within a bounded observation window.</returns>
    [Fact]
    public async Task GetAsync_DoesNotRenewLocalExpiration()
    {
        await using var provider = CreateProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "expiration");
        var policy = new CacheEntryPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        await cache.SetAsync(key, "short-lived", policy);
        Assert.Equal("short-lived", await cache.GetAsync<string>(key));

        var elapsed = Stopwatch.StartNew();
        string? value;
        do
        {
            // Cross the configured expiration while keeping reads active; no delay is used to manufacture contention.
            await Task.Delay(25);
            value = await cache.GetAsync<string>(key);
        } while (value is not null && elapsed.Elapsed < TimeSpan.FromSeconds(5));

        Assert.Null(value);
    }

    /// <summary>
    /// Preserves native cancellation and emits only safe namespace metadata for successful reads and misses.
    /// </summary>
    /// <returns>The cancellation and privacy assertions.</returns>
    [Fact]
    public async Task GetAsync_PreservesCancellationAndSafeLogs()
    {
        var logger = new TestLogger<HybridCacheService>();
        var services = new ServiceCollection().AddLogging().AddCache();
        services.AddSingleton<ILogger<HybridCacheService>>(logger);
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "private-read-key");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
        await cache.SetAsync(key, "private-read-payload", policy);

        Assert.Equal("private-read-payload", await cache.GetAsync<string>(key));
        Assert.Null(await cache.GetAsync<string>(CacheKey.Create("catalog", 1, "missing")));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cache.GetAsync<string>(key, new CancellationToken(true)).AsTask());

        var output = string.Join('\n', logger.Entries.Select(entry => entry.Message));
        Assert.Contains("GetAsync", output);
        Assert.DoesNotContain("private-read-key", output);
        Assert.DoesNotContain("private-read-payload", output);
        Assert.DoesNotContain("factory reading source", output);
        Assert.DoesNotContain(logger.Entries, entry => entry.Message.Contains("cancelled", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Reads Redis without writing a miss, refilling L1 or extending the stored Redis lifetime.
    /// </summary>
    /// <returns>The real Redis tier, expiration, default flags and tag-invalidation assertions.</returns>
    [Fact]
    public async Task GetAsync_ReadsRedisWithoutWritingOrRefillingLocalCache()
    {
        await using var redis = new RedisBuilder("redis:7.4-alpine").Build();
        await redis.StartAsync();
        await using var writer = CreateProvider(redis.GetConnectionString());
        await using var reader = CreateProvider(redis.GetConnectionString());
        await using var bypass = CreateProvider(redis.GetConnectionString(), HybridCacheEntryFlags.DisableDistributedCacheRead);
        var writeCache = writer.GetRequiredService<ICacheService>();
        var readCache = reader.GetRequiredService<ICacheService>();
        var distributed = reader.GetRequiredService<IDistributedCache>();
        using var connection = await ConnectionMultiplexer.ConnectAsync(redis.GetConnectionString());
        var database = connection.GetDatabase();
        var key = CacheKey.Create("catalog", 1, "redis-read");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1), ["catalog-read"]);

        Assert.Null(await readCache.GetAsync<string>(key));
        Assert.Null(await distributed.GetAsync(key.Value));

        await writeCache.SetAsync(key, "stored-in-redis", policy);
        var originalTtl = await database.KeyTimeToLiveAsync(key.Value);
        Assert.Equal("stored-in-redis", await readCache.GetAsync<string>(key));
        var remainingTtl = await database.KeyTimeToLiveAsync(key.Value);
        Assert.True(originalTtl > TimeSpan.Zero && remainingTtl > TimeSpan.Zero && remainingTtl <= originalTtl);
        Assert.Null(await bypass.GetRequiredService<ICacheService>().GetAsync<string>(key));

        // A read-only L2 hit must not leave an L1 copy behind after the writer removes the shared entry.
        await writeCache.RemoveAsync(key);
        Assert.Null(await readCache.GetAsync<string>(key));
        Assert.Equal("after-miss", await writeCache.GetOrCreateAsync(key, _ => ValueTask.FromResult("after-miss"), policy));

        // GetOrCreate releases callers before its background L2 write; verify the originating L1, not immediate replication.
        Assert.Equal("after-miss", await writeCache.GetAsync<string>(key));

        var zeroKey = CacheKey.Create("catalog", 1, "redis-zero");
        var falseKey = CacheKey.Create("catalog", 1, "redis-false");
        var nullKey = CacheKey.Create("catalog", 1, "redis-null");
        await writeCache.SetAsync(zeroKey, 0, policy);
        await writeCache.SetAsync(falseKey, false, policy);
        await writeCache.SetAsync<int?>(nullKey, null, policy);

        Assert.Equal(0, await readCache.GetAsync<int>(zeroKey));
        Assert.False(await readCache.GetAsync<bool>(falseKey));
        Assert.Null(await readCache.GetAsync<int?>(nullKey));
        Assert.Null(await reader.GetRequiredService<HybridCache>().GetOrCreateAsync<int?>(
            nullKey.Value, _ => throw new InvalidOperationException(),
            new HybridCacheEntryOptions { Flags = HybridCacheEntryFlags.DisableLocalCacheRead }));

        // Invalidation is checked on its owning instance; this does not claim immediate invalidation on every host.
        await writeCache.RemoveByTagAsync("catalog-read");
        Assert.Null(await writeCache.GetAsync<string>(key));
    }

    /// <summary>
    /// Keeps native L2 failure-as-miss semantics instead of adding a probe or a replacement cache engine.
    /// </summary>
    /// <returns>The provider-failure and no-write assertions.</returns>
    [Fact]
    public async Task GetAsync_KeepsNativeSecondaryFailureAsMiss()
    {
        var backend = new FailingSecondaryCache();
        var services = new ServiceCollection().AddLogging().AddCache();
        services.AddSingleton<IDistributedCache>(backend);
        await using var provider = services.BuildServiceProvider();

        Assert.Null(await provider.GetRequiredService<ICacheService>()
            .GetAsync<string>(CacheKey.Create("catalog", 1, "secondary-failure")));
        Assert.Equal(0, backend.Writes);
    }

    /// <summary>
    /// Builds the production cache adapter with native options and optional test-owned Redis.
    /// </summary>
    /// <param name="connectionString">The optional connection to a disposable Redis container.</param>
    /// <param name="defaultFlags">The host-level native cache flags.</param>
    /// <returns>The provider owned and disposed by the test.</returns>
    private static ServiceProvider CreateProvider(string? connectionString = null, HybridCacheEntryFlags? defaultFlags = null)
    {
        var services = new ServiceCollection().AddLogging();
        if (connectionString is null)
        {
            services.AddCache();
        }
        else
        {
            services.AddRedisCache(options => options.Configuration = connectionString);
        }

        services.Configure<HybridCacheOptions>(options =>
            options.DefaultEntryOptions = new HybridCacheEntryOptions { Flags = defaultFlags });

        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    /// <summary>
    /// Fails only the external L2 boundary while leaving native HybridCache read/error handling in place.
    /// </summary>
    private sealed class FailingSecondaryCache : IDistributedCache
    {
        /// <summary>
        /// Gets the number of attempted writes, including writes that would incorrectly cache a miss.
        /// </summary>
        public int Writes { get; private set; }

        /// <summary>
        /// Simulates an unavailable secondary store without changing the native cache implementation.
        /// </summary>
        /// <param name="key">The key requested by native HybridCache.</param>
        /// <returns>No payload because this fixture always fails the external read.</returns>
        public byte[] Get(string key) => throw new IOException("fixture secondary store unavailable");

        /// <summary>
        /// Simulates the same secondary failure for asynchronous native reads.
        /// </summary>
        /// <param name="key">The key requested by native HybridCache.</param>
        /// <param name="token">The native caller cancellation token.</param>
        /// <returns>The faulted external read.</returns>
        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(new IOException("fixture secondary store unavailable"));

        /// <summary>
        /// Records an unexpected write without simulating any cache policy of its own.
        /// </summary>
        /// <param name="key">The key native HybridCache tried to store.</param>
        /// <param name="value">The native serialized payload.</param>
        /// <param name="options">The native expiration policy.</param>
        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => Writes++;

        /// <summary>
        /// Records an unexpected asynchronous write.
        /// </summary>
        /// <param name="key">The key native HybridCache tried to store.</param>
        /// <param name="value">The native serialized payload.</param>
        /// <param name="options">The native expiration policy.</param>
        /// <param name="token">The native caller cancellation token.</param>
        /// <returns>The completed write observation.</returns>
        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options,
            CancellationToken token = default)
        {
            Set(key, value, options);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Accepts an unused refresh operation without introducing fixture expiration behavior.
        /// </summary>
        /// <param name="key">The native cache key.</param>
        public void Refresh(string key) { }

        /// <summary>
        /// Accepts an unused asynchronous refresh operation.
        /// </summary>
        /// <param name="key">The native cache key.</param>
        /// <param name="token">The native caller cancellation token.</param>
        /// <returns>The completed no-op.</returns>
        public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

        /// <summary>
        /// Accepts an unused removal operation without storing fixture data.
        /// </summary>
        /// <param name="key">The native cache key.</param>
        public void Remove(string key) { }

        /// <summary>
        /// Accepts an unused asynchronous removal operation.
        /// </summary>
        /// <param name="key">The native cache key.</param>
        /// <param name="token">The native caller cancellation token.</param>
        /// <returns>The completed no-op.</returns>
        public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
    }
}
