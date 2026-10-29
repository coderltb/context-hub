namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.Services;

/// <summary>
/// Verifies cache key safety, phase logging, factory coalescing and real Redis secondary-cache behavior.
/// </summary>
public sealed class CacheServiceTests
{
    /// <summary>
    /// Supplies one shared lifetime when callers do not select a feature-specific policy.
    /// </summary>
    [Fact]
    public void RegistrationHasCommonLifetimeDefaults()
    {
        var services = new ServiceCollection().AddLogging().AddCache();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(60), options.DefaultEntryOptions!.Expiration);
        Assert.Equal(TimeSpan.FromSeconds(15), options.DefaultEntryOptions.LocalCacheExpiration);
    }

    /// <summary>
    /// Accepts omitted and explicit-null policies while preserving normal native caching behavior.
    /// </summary>
    /// <returns>The actual native cache read/write assertions.</returns>
    [Fact]
    public async Task OptionalPolicyUsesSharedDefaultsWithoutAnExtraResolver()
    {
        var services = new ServiceCollection().AddLogging().AddCache();
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("optional-policy", 1, "entry");
        await cache.SetAsync(key, "stored");

        Assert.Equal("stored", await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("unexpected")));
        await cache.SetAsync(key, "replaced", policy: null);
        Assert.Equal("replaced", await cache.GetAsync<string>(key));
    }

    /// <summary>
    /// Keeps native host flags and lifetimes across repeated registration and null-policy calls.
    /// </summary>
    /// <returns>The native bypass and explicit-override assertions.</returns>
    [Fact]
    public async Task HostDefaultsSurviveRepeatedRegistrationAndExplicitPolicyCanOverride()
    {
        var services = new ServiceCollection().AddLogging().AddCache(options => options.DefaultEntryOptions = new()
        {
            Expiration = TimeSpan.FromSeconds(120),
            LocalCacheExpiration = TimeSpan.FromSeconds(30),
            Flags = HybridCacheEntryFlags.DisableLocalCacheWrite
        });
        services.AddCache();
        using var provider = services.BuildServiceProvider();
        var defaults = provider.GetRequiredService<IOptions<HybridCacheOptions>>().Value.DefaultEntryOptions!;
        Assert.Equal(TimeSpan.FromSeconds(120), defaults.Expiration);
        Assert.Equal(TimeSpan.FromSeconds(30), defaults.LocalCacheExpiration);
        Assert.Equal(HybridCacheEntryFlags.DisableLocalCacheWrite, defaults.Flags);
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("optional-policy", 1, "host-flags");
        await cache.SetAsync(key, "not-stored");
        Assert.Null(await cache.GetAsync<string>(key));

        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(20), flags: HybridCacheEntryFlags.None);
        await cache.SetAsync(key, "explicit", policy);
        Assert.Equal("explicit", await cache.GetAsync<string>(key));
    }

    /// <summary>
    /// Forwards native read and write bypass flags without changing the default cache policy.
    /// </summary>
    /// <param name="useRedis">Whether two providers share a real Redis secondary cache.</param>
    /// <returns>The native cache bypass assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EntryFlags_ControlNativeReadAndWriteBypass(bool useRedis)
    {
        await using var redis = useRedis ? new RedisBuilder("redis:7-alpine").Build() : null;
        if (useRedis)
        {
            await redis!.StartAsync();
        }

        var connection = redis?.GetConnectionString();
        await using var first = CreateProvider(connection);
        await using var second = CreateProvider(connection);
        var cache = first.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "flags");
        var normal = new CacheEntryPolicy(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(30));
        var noRead = new CacheEntryPolicy(normal.Expiration, normal.LocalExpiration, flags:
            HybridCacheEntryFlags.DisableLocalCacheRead | HybridCacheEntryFlags.DisableDistributedCacheRead);
        var noWrite = new CacheEntryPolicy(normal.Expiration, normal.LocalExpiration, flags:
            HybridCacheEntryFlags.DisableLocalCacheWrite | HybridCacheEntryFlags.DisableDistributedCacheWrite);
        await cache.SetAsync(key, "original", normal);

        Assert.Null(normal.Flags);
        Assert.Equal("fresh", await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("fresh"), noRead));
        await cache.SetAsync(key, "not-stored", noWrite);
        Assert.Equal("fresh", await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("unexpected"), normal));
        if (useRedis)
        {
            // A second provider starts without an L1 copy, proving that bypass also reaches native L2 options.
            Assert.Equal("fresh", await second.GetRequiredService<ICacheService>()
                .GetOrCreateAsync(key, _ => ValueTask.FromResult("unexpected"), normal));
        }
    }

    /// <summary>
    /// Verifies cache phase logs and original-exception preservation without exposing keys, payloads or exception
    /// messages.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ReadWriteAndFailure_PreserveSafePhaseLogsWithoutDuplicateFailureDiagnostic()
    {
        var logger = new TestLogger<HybridCacheService>();
        var services = new ServiceCollection().AddLogging().AddCache();
        services.AddSingleton<ILogger<HybridCacheService>>(logger);
        await using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));
        var key = CacheKey.Create("catalog", 1, "private-key-part");
        await cache.SetAsync(key, "private-payload", policy);
        await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("unused"), policy);
        await cache.RemoveAsync(key);
        var original = new InvalidOperationException("password=private-error-value");
        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync<string>(key, _ => throw original, policy).AsTask());

        Assert.Same(original, actual);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("started", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("stored", StringComparison.Ordinal));
        Assert.Contains(
            logger.Entries,
            entry => entry.Message.Contains("resolved", StringComparison.Ordinal));
        Assert.Contains(
            logger.Entries,
            entry => entry.Message.Contains("factory reading source", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        var output = string.Join('\n', logger.Entries.Select(entry => entry.Message));
        Assert.DoesNotContain("private-key-part", output);
        Assert.DoesNotContain("private-payload", output);
        Assert.DoesNotContain("private-error-value", output);
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    /// <summary>
    /// Verifies that namespace, version and ordered part boundaries produce distinct opaque keys.
    /// </summary>
    [Fact]
    public void Key_EncodesPartBoundariesNamespaceAndVersion()
    {
        var first = CacheKey.Create("catalog", 1, "ab", "c");
        Assert.Equal(first, CacheKey.Create("catalog", 1, "ab", "c"));
        Assert.NotEqual(first, CacheKey.Create("catalog", 1, "a", "bc"));
        Assert.NotEqual(first, CacheKey.Create("catalog", 2, "ab", "c"));
        Assert.NotEqual(first, CacheKey.Create("manifest", 1, "ab", "c"));
        Assert.DoesNotContain("private-value", CacheKey.Create("catalog", 1, "private-value").Value);
    }

    /// <summary>
    /// Verifies that invalid namespaces and non-positive versions are rejected before cache access.
    /// </summary>
    /// <param name="name">The metadata name selected by this fixture.</param>
    /// <param name="version">The version value exercised by this scenario.</param>
    [Theory]
    [InlineData("Catalog", 1)]
    [InlineData("catalog:*", 1)]
    [InlineData("catalog", 0)]
    public void Key_RejectsInvalidNamesAndVersions(string name, int version)
    {
        Assert.ThrowsAny<ArgumentException>(() => CacheKey.Create(name, version, "id"));
    }

    /// <summary>
    /// Verifies that an exact entry can be stored, retrieved and removed from the local cache.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Cache_StagesAndRemovesAnExactEntry()
    {
        await using var provider = CreateProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "id");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));
        await cache.SetAsync(key, "stored", policy);

        Assert.Equal("stored", await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("fresh"), policy));
        await cache.RemoveAsync(key);
        Assert.Equal("fresh", await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("fresh"), policy));
    }

    /// <summary>
    /// Verifies that concurrent misses within one cache instance share a single source factory invocation.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ConcurrentMisses_ShareOneFactoryWithinTheInstance()
    {
        await using var provider = CreateProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "coalesced");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        async ValueTask<string> Factory(CancellationToken token)
        {
            Interlocked.Increment(ref calls);
            started.TrySetResult();
            await release.Task.WaitAsync(token);
            return "value";
        }
        var firstOperation = cache.GetOrCreateAsync(key, Factory, policy);
        var first = firstOperation.AsTask();
        // Hold the first factory open until the second caller joins; a delay alone would make coalescing flaky.
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var secondOperation = cache.GetOrCreateAsync(key, Factory, policy);
        var second = secondOperation.AsTask();
        release.TrySetResult();

        Assert.Equal(["value", "value"], await Task.WhenAll(first, second));
        Assert.Equal(1, calls);
    }

    /// <summary>
    /// Verifies that a factory exception is rethrown unchanged and does not poison a later lookup.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task FactoryFailure_IsPreservedAndNotCached()
    {
        await using var provider = CreateProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "failed");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));
        var expected = new InvalidOperationException("test failure");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.GetOrCreateAsync<string>(key, _ => throw expected, policy).AsTask());
        Assert.Same(expected, actual);
        Assert.Equal(
            "recovered",
            await cache.GetOrCreateAsync(key, _ => ValueTask.FromResult("recovered"), policy));
    }

    /// <summary>
    /// Verifies cross-instance Redis retrieval and removal using a disposable real Redis container.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task RedisSecondary_SharesDataAcrossInstancesAndHonorsExactRemoval()
    {
        await using var redis = new RedisBuilder("redis:7.4-alpine").Build();
        await redis.StartAsync();
        await using var first = CreateProvider(redis.GetConnectionString());
        await using var second = CreateProvider(redis.GetConnectionString());
        var cache = first.GetRequiredService<ICacheService>();
        var key = CacheKey.Create("catalog", 1, "shared");
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));
        await cache.SetAsync(key, "from-redis", policy);
        var calls = 0;
        var result = await second.GetRequiredService<ICacheService>().GetOrCreateAsync(key, _ =>
        {
            calls++;
            return ValueTask.FromResult("unexpected");
        }, policy);
        Assert.Equal("from-redis", result);
        Assert.Equal(0, calls);

        await cache.RemoveAsync(key);
        await using var third = CreateProvider(redis.GetConnectionString());
        // A fresh provider has no L1 copy, so this lookup proves removal from Redis rather than local invalidation.
        Assert.Equal("after-removal", await third.GetRequiredService<ICacheService>()
            .GetOrCreateAsync(key, _ => ValueTask.FromResult("after-removal"), policy));
    }

    /// <summary>
    /// Creates an isolated cache service provider with optional Redis secondary storage.
    /// </summary>
    /// <param name="connectionString">The fixture-owned database connection string, never included in diagnostic messages.</param>
    /// <returns>The configured service provider owned and disposed by the test.</returns>
    private static ServiceProvider CreateProvider(string? connectionString = null)
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
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }
}
