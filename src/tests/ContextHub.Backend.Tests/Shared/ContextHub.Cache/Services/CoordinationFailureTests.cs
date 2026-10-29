namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.Services;

/// <summary>
/// Injects failures at the native Redis command seam without replacing the lock algorithm.
/// </summary>
public sealed class CoordinationFailureTests
{
    /// <summary>
    /// Translates only Redis outage families and preserves unrelated failures and cancellation.
    /// </summary>
    /// <param name="kind">The independently chosen command failure family.</param>
    /// <returns>The exception identity and safe diagnostic assertions.</returns>
    [Theory]
    [InlineData("provider")]
    [InlineData("timeout")]
    [InlineData("programming")]
    [InlineData("command")]
    [InlineData("cancellation")]
    public async Task Lock_TranslatesOnlyProviderFailures(string kind)
    {
        Exception failure = kind switch
        {
            "provider" => new RedisException("private-provider-message"),
            "timeout" => new RedisTimeoutException("private-provider-message", CommandStatus.Unknown),
            "command" => new RedisCommandException("private-command-message"),
            "cancellation" => new OperationCanceledException("private-cancellation-message"),
            _ => new InvalidOperationException("private-programming-message")
        };
        var logger = new TestLogger<DistributedLockService>();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton(CreateConnection(failure));
        services.AddSingleton<ILogger<DistributedLockService>>(logger);
        services.AddTransient<IDistributedLockService, DistributedLockService>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var actual = await Record.ExceptionAsync(() => provider.GetRequiredService<IDistributedLockService>()
            .TryAcquireAsync("fixture-key", TimeSpan.FromSeconds(20)));

        if (kind is "provider" or "timeout")
        {
            Assert.Same(failure, Assert.IsType<DistributedLockUnavailableException>(actual).InnerException);
            var warning = Assert.Single(logger.Entries, entry => entry.Level >= LogLevel.Warning);
            Assert.Contains(failure.GetType().Name, warning.Message);
            Assert.DoesNotContain("private-", warning.Message);
            Assert.Null(warning.Exception);
        }
        else
        {
            Assert.Same(failure, actual);
            Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
        }
    }

    /// <summary>
    /// Leaves atomic mutation failures unchanged without emitting a duplicate failure diagnostic.
    /// </summary>
    /// <returns>The original exception and absence of failure logs.</returns>
    [Fact]
    public async Task AtomicCommand_PropagatesWithoutFailureLogOrRetry()
    {
        var failure = new RedisException("private-provider-message");
        var logger = new TestLogger<AtomicCacheService>();
        var service = new AtomicCacheService(CreateConnection(failure), logger);

        Assert.Same(failure, await Record.ExceptionAsync(() => service.IncrementAsync("fixture-key", TimeSpan.FromMinutes(1))));
        Assert.DoesNotContain(logger.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    /// <summary>
    /// Supplies native interface proxies that fail only the asynchronous Redis command boundary.
    /// </summary>
    /// <param name="failure">The exact provider-side failure to propagate.</param>
    /// <returns>The multiplexer used by the real coordination services.</returns>
    private static IConnectionMultiplexer CreateConnection(Exception failure)
    {
        var connection = DispatchProxy.Create<IConnectionMultiplexer, CommandFailureProxy>();
        var database = DispatchProxy.Create<RedisDatabase, CommandFailureProxy>();
        Assert.IsAssignableFrom<CommandFailureProxy>(connection).Handler = (method, _) => method.Name switch
        {
            "GetDatabase" => database,
            "Dispose" => null,
            _ => throw new NotSupportedException(method.Name)
        };
        Assert.IsAssignableFrom<CommandFailureProxy>(database).Handler = (method, _) => method.Name switch
        {
            "get_Multiplexer" => connection,
            "get_Database" => 0,
            _ when method.ReturnType == typeof(Task<bool>) => Task.FromException<bool>(failure),
            _ when method.ReturnType == typeof(Task<RedisResult>) => Task.FromException<RedisResult>(failure),
            _ => throw new NotSupportedException(method.Name)
        };

        return connection;
    }

    /// <summary>
    /// Supports DispatchProxy's generated subclass while keeping command fault injection in this fixture.
    /// </summary>
    public class CommandFailureProxy : DispatchProxy
    {
        /// <summary>
        /// Defines the native member boundary supported by this fixture.
        /// </summary>
        public Func<MethodInfo, object?[]?, object?> Handler { get; set; } = null!;

        /// <summary>
        /// Dispatches one native interface invocation to the explicitly configured fixture behavior.
        /// </summary>
        /// <param name="targetMethod">The native Redis interface member.</param>
        /// <param name="args">The member arguments, never logged.</param>
        /// <returns>The member result or failed task.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Handler(targetMethod!, args);
    }
}
