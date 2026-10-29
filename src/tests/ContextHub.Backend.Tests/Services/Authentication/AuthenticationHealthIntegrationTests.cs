namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises the native EF/Redis probes against disposable dependencies, including outage isolation and pool ownership.
/// </summary>
public sealed class AuthenticationHealthIntegrationTests
{
    /// <summary>
    /// Keeps recovery failure separate from readiness and preserves an externally owned multiplexer after health-service disposal.
    /// </summary>
    /// <returns>The healthy, outage, safe-output and ownership assertions.</returns>
    [Fact]
    public async Task NativeProbesUseExistingPoolAndIsolateDependencyOutages()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
        await using var redis = new RedisBuilder("redis:7.4-alpine").Build();
        await postgres.StartAsync();
        await redis.StartAsync();
        using var connection = await ConnectionMultiplexer.ConnectAsync(
            redis.GetConnectionString() + ",abortConnect=false,connectTimeout=1000,asyncTimeout=1000,syncTimeout=1000");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(AuthenticationTestKeys.Create())
            .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = postgres.GetConnectionString(),
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32)
        }).Build();
        var services = new ServiceCollection().AddLogging().AddHttpAudit();
        services.AddSingleton<IConnectionMultiplexer>(connection);
        services.AddHealthInfrastructure();
        services.AddAuthenticationInfrastructure(configuration, new HealthEnvironment());
        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IConnectionMultiplexer));

        using (var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }))
        {
            var healthService = provider.GetRequiredService<HealthCheckService>();
            Assert.Same(connection, provider.GetRequiredService<IConnectionMultiplexer>());
            var ready = await healthService.CheckHealthAsync(check => check.Tags.Contains("ready"));
            var recovery = await healthService.CheckHealthAsync(check => check.Tags.Contains("recovery"));
            Assert.Equal(HealthStatus.Healthy, ready.Status);
            Assert.Equal(HealthStatus.Healthy, recovery.Status);
            Assert.Equal(["postgresql"], ready.Entries.Keys);
            Assert.Equal(["redis"], recovery.Entries.Keys);

        }

        // Disposal of the health host cannot close a caller-owned pool; prove it with a real command before the outage.
        await connection.GetDatabase().PingAsync();
        using (var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true }))
        {
            var healthService = provider.GetRequiredService<HealthCheckService>();

            // Connectivity succeeds without provisioning any product schema; EF health is deliberately not a schema check.
            await redis.StopAsync();
            var recovery = await healthService.CheckHealthAsync(check => check.Tags.Contains("recovery"));
            Assert.Equal(HealthStatus.Unhealthy, recovery.Status);
            Assert.Equal(HealthStatus.Healthy,
                (await healthService.CheckHealthAsync(check => check.Tags.Contains("ready"))).Status);
            await AssertSafeFailureAsync(recovery);

            await postgres.StopAsync();
            var ready = await healthService.CheckHealthAsync(check => check.Tags.Contains("ready"));
            Assert.Equal(HealthStatus.Unhealthy, ready.Status);
            Assert.Equal(HealthStatus.Healthy,
                (await healthService.CheckHealthAsync(check => check.Tags.Contains("live"))).Status);
            await AssertSafeFailureAsync(ready);
        }
    }

    /// <summary>
    /// Checks the shared writer against actual provider failures instead of exposing health exception details.
    /// </summary>
    /// <param name="report">The failed native dependency report.</param>
    /// <returns>The safe health wire-contract assertions.</returns>
    private static async Task AssertSafeFailureAsync(HealthReport report)
    {
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;
        await HealthResponseWriter.WriteReadinessAsync(context, report);
        using var json = JsonDocument.Parse(body.ToArray());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(["status", "checks"], json.RootElement.EnumerateObject().Select(property => property.Name));
        foreach (var check in json.RootElement.GetProperty("checks").EnumerateArray())
        {
            Assert.Equal(["name", "status", "durationMs"], check.EnumerateObject().Select(property => property.Name));
        }
    }

    /// <summary>
    /// Selects development registration while keeping hosted schedulers stopped in this dependency-only test.
    /// </summary>
    private sealed class HealthEnvironment : IHostEnvironment
    {
        /// <summary>
        /// Gets or sets the native credential-registration environment.
        /// </summary>
        public string EnvironmentName { get; set; } = Environments.Development;

        /// <summary>
        /// Gets or sets the test composition name.
        /// </summary>
        public string ApplicationName { get; set; } = nameof(AuthenticationHealthIntegrationTests);

        /// <summary>
        /// Gets or sets the unused content root.
        /// </summary>
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        /// <summary>
        /// Gets or sets the unused file provider.
        /// </summary>
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
