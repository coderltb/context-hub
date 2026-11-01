namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies a real unavailable database cannot stop the HTTP host through its metadata refresh worker.
/// </summary>
public sealed class IntegrationHostAvailabilityTests
{
    private const string ConnectionString =
        "Host=127.0.0.1;Port=1;Database=closed_port_probe;Username=postgres;Password=test-only;Timeout=1;Command Timeout=1;Pooling=false";

    /// <summary>
    /// Records the native EF wrapper shape from an actual refused local database connection.
    /// </summary>
    /// <returns>The precise transient-provider classification assertions.</returns>
    [Fact]
    public async Task NativeUnavailableDatabaseUsesTransientNpgsqlWrapper()
    {
        using var host = CreateHost(new WarningLogger());
        using var scope = host.Services.CreateScope();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<ContextHubDbContext>().ExternalMcps.AsNoTracking().ToListAsync());

        Assert.True(Assert.IsAssignableFrom<NpgsqlException>(failure.InnerException).IsTransient);
    }

    /// <summary>
    /// Leaves native host lifetime running after the actual scheduled database read fails.
    /// </summary>
    /// <returns>The observed safe warning and running-host assertions.</returns>
    [Fact]
    public async Task MetadataRefreshDatabaseOutageDoesNotStopHost()
    {
        var logger = new WarningLogger();
        using var host = CreateHost(logger);
        var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
        await host.StartAsync();
        try
        {
            var warning = await logger.Warning.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(lifetime.ApplicationStopping.IsCancellationRequested);
            Assert.DoesNotContain("test-only", warning, StringComparison.Ordinal);
            Assert.DoesNotContain("127.0.0.1", warning, StringComparison.Ordinal);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    /// <summary>
    /// Propagates unexpected application and SQL failures instead of treating them as recoverable outages.
    /// </summary>
    /// <param name="kind">The unexpected failure kind.</param>
    /// <returns>The original-instance and no-recovery-log assertions.</returns>
    [Theory]
    [InlineData("programming")]
    [InlineData("schema")]
    public async Task MetadataRefreshDoesNotSwallowUnexpectedFailures(string kind)
    {
        Exception original = kind == "schema"
            ? new PostgresException("test-only", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable)
            : new InvalidOperationException("test-only");
        var logger = new WarningLogger();
        using var host = CreateHost(logger, original);
        var worker = host.Services.GetServices<IHostedService>().OfType<ExternalMetadataRefreshService>().Single();
        await worker.StartAsync(CancellationToken.None);

        var actual = await Assert.ThrowsAnyAsync<Exception>(() => worker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Same(original, actual);
        Assert.False(logger.Warning.Task.IsCompleted);
    }

    private static IHost CreateHost(WarningLogger logger, Exception? refreshFailure = null)
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.Logging.ClearProviders();
        builder.Services.AddDbContext<ContextHubDbContext>(options => options.UseNpgsql(ConnectionString, native => native.UseVector()));
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork<ContextHubDbContext>>();
        builder.Services.AddScoped<IExternalConnectionRepository, ExternalConnectionRepository>();
        builder.Services.AddOptions<ExternalMcpOptions>();
        builder.Services.AddCache();
        builder.Services.AddSingleton<IEmbeddingService, NoEmbedding>();
        builder.Services.AddSingleton<IExternalMcpClient, NoClient>();
        builder.Services.AddScoped<IExternalToolRegistry, ExternalToolRegistry>();
        if (refreshFailure is not null)
        {
            builder.Services.AddScoped<IExternalToolRegistry>(_ => new FailedRegistry(refreshFailure));
        }
        builder.Services.AddSingleton<ILogger<ExternalMetadataRefreshService>>(logger);
        builder.Services.AddHostedService<ExternalMetadataRefreshService>();
        return builder.Build();
    }

    private sealed class WarningLogger : ILogger<ExternalMetadataRefreshService>
    {
        public TaskCompletionSource<string> Warning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Warning.TrySetResult(formatter(state, exception));
            }
        }
    }

    private sealed class NoEmbedding : IEmbeddingService
    {
        public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
        public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
    }

    private sealed class NoClient : IExternalMcpClient
    {
        public Task<IReadOnlyList<ExternalToolModel>> DiscoverAsync(string endpoint, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<JsonElement> CallAsync(string endpoint, string toolName, JsonElement arguments, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FailedRegistry(Exception original) : IExternalToolRegistry
    {
        public Task RefreshAsync(CancellationToken cancellationToken) => Task.FromException(original);
        public Task<IReadOnlyList<ExternalToolResponseDto>> PreviewAsync(string name, ExternalMcpConfigDto config, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<ExternalToolCandidateModel>> GetCandidatesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExternalToolCandidateModel?> ResolveAsync(ExternalToolRouteModel route, bool refresh, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExternalMcpDiscoveryDto> GetSummaryAsync(IntegrationExternalMcp connection, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
