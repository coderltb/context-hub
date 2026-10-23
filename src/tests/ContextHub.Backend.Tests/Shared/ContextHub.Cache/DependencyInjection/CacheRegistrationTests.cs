namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.DependencyInjection;

/// <summary>
/// Verifies explicit atomic mode selection without duplicate stores or accidental lock removal.
/// </summary>
public sealed class CacheRegistrationTests
{
    /// <summary>
    /// Rejects a missing callback before Redis composition changes the container.
    /// </summary>
    [Fact]
    public void RedisRoot_NullCallbackDoesNotMutateContainer()
    {
        var services = new ServiceCollection().AddLogging();
        var before = services.ToArray();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddRedisInfrastructure(null!));

        Assert.Equal("configure", exception.ParamName);
        Assert.Equal(before, services.ToArray());
    }

    /// <summary>
    /// Delegates container validation without running the host's configuration callback.
    /// </summary>
    [Fact]
    public void RedisRoot_NullContainerDoesNotRunCallback()
    {
        var callbackCalled = false;

        var exception = Assert.Throws<ArgumentNullException>(() =>
            CacheServiceRegistration.AddRedisInfrastructure(null!, _ => callbackCalled = true));

        Assert.Equal("services", exception.ParamName);
        Assert.False(callbackCalled);
    }

    /// <summary>
    /// One Redis root call captures connection configuration once and leaves the shared pool lazy until resolution.
    /// </summary>
    [Fact]
    public void RootRegistration_ConfiguresOneLazyPoolForAllCapabilities()
    {
        var services = new ServiceCollection().AddLogging();
        var configureCalls = 0;
        var connectionCalls = 0;

        services.AddRedisInfrastructure(options =>
        {
            configureCalls++;
            options.ConnectionMultiplexerFactory = () =>
            {
                connectionCalls++;
                throw new InvalidOperationException("Synthetic connection boundary must remain lazy.");
            };
        });

        Assert.Equal(1, configureCalls);
        Assert.Equal(0, connectionCalls);
        Assert.Single(services, item => item.ServiceType == typeof(IConnectionMultiplexer));
        Assert.Single(services, item => item.ServiceType == typeof(IAtomicCacheService));
        Assert.Single(services, item => item.ServiceType == typeof(IDistributedLockService));
        Assert.Single(services, item => item.ServiceType == typeof(ICacheService));
    }

    /// <summary>
    /// Repeated Redis composition retains one lazy pool and does not disturb independent lock ownership.
    /// </summary>
    [Fact]
    public void RedisRoot_RepeatedRegistrationRetainsPoolAndLock()
    {
        var services = new ServiceCollection().AddLogging();
        services.AddRedisInfrastructure(options => options.Configuration = "unused.invalid");
        var pool = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer));
        var lockDescriptor = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDistributedLockService));

        services.AddRedisInfrastructure(options => options.Configuration = "another-unused.invalid");

        Assert.Same(pool, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer)));
        Assert.Same(lockDescriptor, Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IDistributedLockService)));
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IAtomicCacheService));
    }

    /// <summary>
    /// Repeated memory registration must resolve one process-local store and never create a distributed lock.
    /// </summary>
    [Fact]
    public void MemoryRegistration_RepeatedSelection_HasOneStore()
    {
        var services = new ServiceCollection().AddLogging();

        services.AddInMemoryAtomicCache().AddInMemoryAtomicCache();
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });

        Assert.Single(provider.GetServices<IAtomicCacheService>());
        Assert.IsType<InMemoryAtomicCacheService>(provider.GetRequiredService<IAtomicCacheService>());
        Assert.Null(provider.GetService<IDistributedLockService>());
    }

    /// <summary>
    /// The last explicitly selected atomic mode wins without changing an existing lock registration.
    /// </summary>
    /// <param name="memoryLast">Whether process-local mode is selected last.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AtomicRegistration_LastSelectionWins_WithoutChangingLock(bool memoryLast)
    {
        var services = new ServiceCollection();
        services.AddRedisCoordination();
        var lockDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IDistributedLockService));

        services.AddInMemoryAtomicCache();
        if (!memoryLast)
        {
            services.AddRedisCoordination().AddRedisCoordination();
        }

        // Selecting an implementation must not open Redis, so inspect the composed descriptors before resolution.
        var atomicDescriptor = Assert.Single(services, item => item.ServiceType == typeof(IAtomicCacheService));
        Assert.Equal(memoryLast ? typeof(InMemoryAtomicCacheService) : typeof(AtomicCacheService), atomicDescriptor.ImplementationType);
        Assert.Same(lockDescriptor, Assert.Single(services, item => item.ServiceType == typeof(IDistributedLockService)));
    }
}
