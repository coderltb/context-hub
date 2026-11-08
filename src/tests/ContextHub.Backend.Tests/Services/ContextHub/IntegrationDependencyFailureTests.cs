namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies native dependency failures are translated only at their owning integration IO boundaries.
/// </summary>
public sealed class IntegrationDependencyFailureTests
{
    /// <summary>
    /// Classifies real refused PostgreSQL reads without reporting a missing row or publishing provider data.
    /// </summary>
    /// <returns>The read-boundary and preserved-native-cause assertions.</returns>
    [Fact]
    public async Task ClosedPostgreSqlReadsBecomeDependencyFailures()
    {
        await using var context = CreateClosedContext();
        var unitOfWork = new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance);
        var sources = new AuthoringRepository(context, unitOfWork, NullLogger<AuthoringRepository>.Instance);
        var external = new ExternalConnectionRepository(context, unitOfWork, NullLogger<ExternalConnectionRepository>.Instance);

        AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => sources.GetEntityAsync(Guid.CreateVersion7(), CancellationToken.None)));
        AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => sources.GetPageAsync(new(), CancellationToken.None)));
        AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => external.GetConnectionsAsync(CancellationToken.None)));
        AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => external.GetByIdAsync(Guid.CreateVersion7(), CancellationToken.None)));
        AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => external.GetByNameAsync("reviewed-provider", CancellationToken.None)));
    }

    /// <summary>
    /// Preserves the exact native failure for staged inserts and deletes when PostgreSQL cannot be reached.
    /// </summary>
    /// <returns>The write-boundary assertions.</returns>
    [Fact]
    public async Task ClosedPostgreSqlWritesBecomeDependencyFailures()
    {
        await using (var context = CreateClosedContext())
        {
            var repository = new AuthoringRepository(context,
                new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance), NullLogger<AuthoringRepository>.Instance);
            var entity = new IntegrationRepository { Id = Guid.CreateVersion7(), Url = "https://github.com/owner/source", DefaultRef = "main" };
            await repository.AddAsync(entity);
            AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => repository.SaveAsync(entity, null, CancellationToken.None)));
        }

        await using (var context = CreateClosedContext())
        {
            var repository = CreateExternal(context);
            var entity = new IntegrationExternalMcp { Id = Guid.CreateVersion7(), Name = "reviewed-provider", Config = "{}" };
            AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => repository.SaveAsync(entity, null, CancellationToken.None)));
        }

        await using (var context = CreateClosedContext())
        {
            var repository = CreateExternal(context);
            var entity = new IntegrationExternalMcp { Id = Guid.CreateVersion7(), Name = "reviewed-provider", Config = "{}", ConcurrencyVersion = 1 };
            context.Attach(entity);
            AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => repository.DeleteAsync(entity, 1, CancellationToken.None)));
        }
    }

    /// <summary>
    /// Reports the declared managed index failure when its current database authority cannot be read.
    /// </summary>
    /// <returns>The registry and managed tool-error assertions.</returns>
    [Fact]
    public async Task ClosedPostgreSqlRegistryProducesDeclaredManagedUnavailableResult()
    {
        await using var context = CreateClosedContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCache();
        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var client = new NoClient();
        var embedding = new NoEmbedding();
        var registry = new ExternalToolRegistry(CreateExternal(context), client, cache, Options.Create(new ExternalMcpOptions()), embedding,
            NullLogger<ExternalToolRegistry>.Instance);
        AssertNativeCause(await Assert.ThrowsAsync<DependencyUnavailableException>(() => registry.GetCandidatesAsync(CancellationToken.None)));
        var facade = new ManagedToolService(registry, client, cache, embedding, Options.Create(new ExternalMcpOptions()));

        var result = await facade.SearchAsync("public documentation", CancellationToken.None);

        Assert.Equal("managed_tool_index_unavailable", result.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, client.Calls);
    }

    /// <summary>
    /// Keeps programming errors and native SQL contention distinct from dependency unavailability.
    /// </summary>
    /// <param name="kind">The non-availability failure raised at the query boundary.</param>
    /// <returns>The original-instance preservation assertion.</returns>
    [Theory]
    [InlineData("programming")]
    [InlineData("schema")]
    [InlineData("serialization")]
    [InlineData("deadlock")]
    public async Task NonAvailabilityFailuresAreNotReclassified(string kind)
    {
        await using var context = CreateClosedContext();
        Exception original = kind switch
        {
            "schema" => new PostgresException("test-only", "ERROR", "ERROR", PostgresErrorCodes.UndefinedTable),
            "serialization" => new PostgresException("test-only", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure),
            "deadlock" => new PostgresException("test-only", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected),
            _ => new InvalidOperationException("test-only")
        };
        var repository = new FailedQueryRepository(context, original);

        var actual = await Assert.ThrowsAnyAsync<Exception>(() => repository.GetEntityAsync(Guid.CreateVersion7(), CancellationToken.None));

        Assert.Same(original, actual);
    }

    /// <summary>
    /// Leaves caller cancellation unchanged before any closed-port database read can start.
    /// </summary>
    /// <returns>The cancellation-type assertion.</returns>
    [Fact]
    public async Task CancelledDatabaseReadIsNotReclassified()
    {
        await using var context = CreateClosedContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateExternal(context).GetConnectionsAsync(cancellation.Token));
    }

    /// <summary>
    /// Classifies a real failed resolver before persistence or hosted MCP calls are attempted.
    /// </summary>
    /// <returns>The DNS failure and zero-IO-side-effect assertions.</returns>
    [Fact]
    public async Task UnresolvableExternalEndpointIsDependencyFailureBeforePersistence()
    {
        var repository = new CountingConnectionRepository();
        var registry = new NoRegistry();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCache();
        using var provider = services.BuildServiceProvider();
        var service = new ExternalMcpService(repository, registry, provider.GetRequiredService<ICacheService>());
        var request = new SaveExternalMcpRequestDto
        {
            Config = JsonSerializer.SerializeToElement(new
            {
                schemaVersion = 1, url = "https://contexthub-probe-does-not-exist.invalid/mcp", enabled = true, toolPolicy = new { mode = "all" }
            })
        };

        var failure = await Assert.ThrowsAsync<DependencyUnavailableException>(() => service.CreateAsync(new CreateExternalMcpRequestDto { Name = "provider", Config = request.Config }, CancellationToken.None));

        Assert.IsAssignableFrom<SocketException>(failure.InnerException);
        Assert.Equal(0, repository.Calls);
        Assert.Equal(0, registry.Calls);
    }

    private static ContextHubDbContext CreateClosedContext() => new(new DbContextOptionsBuilder<ContextHubDbContext>()
        .UseNpgsql("Host=127.0.0.1;Port=1;Database=closed_port_probe;Username=postgres;Password=test-only;Timeout=1;Command Timeout=1;Pooling=false",
            options => options.UseVector()).Options);

    private static ExternalConnectionRepository CreateExternal(ContextHubDbContext context) => new(context,
        new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance), NullLogger<ExternalConnectionRepository>.Instance);

    private static void AssertNativeCause(DependencyUnavailableException failure)
    {
        Assert.NotNull(failure.InnerException);
        var provider = failure.InnerException as NpgsqlException ?? failure.InnerException.InnerException as NpgsqlException;
        Assert.NotNull(provider);
        Assert.True(provider.IsTransient);
        Assert.DoesNotContain("test-only", failure.Message, StringComparison.Ordinal);
    }

    private sealed class FailedQueryRepository(ContextHubDbContext context, Exception original)
        : AuthoringRepository(context, new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance), NullLogger<AuthoringRepository>.Instance)
    {
        protected override IQueryable<IntegrationRepository> CreateQuery(Expression<Func<IntegrationRepository, bool>>? predicate = null,
            SearchOptions<IntegrationRepository>? search = null, QueryOrder<IntegrationRepository>? order = null) => throw original;
    }

    private sealed class NoEmbedding : IEmbeddingService
    {
        public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
        public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
    }

    private sealed class NoClient : IExternalMcpClient
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<ExternalToolModel>> DiscoverAsync(string endpoint, CancellationToken cancellationToken)
        {
            Calls++;
            throw new NotSupportedException();
        }
        public Task<JsonElement> CallAsync(string endpoint, string toolName, JsonElement arguments, CancellationToken cancellationToken)
        {
            Calls++;
            throw new NotSupportedException();
        }
    }

    private sealed class CountingConnectionRepository : IExternalConnectionRepository
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<IntegrationExternalMcp>> GetConnectionsAsync(CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task<IntegrationExternalMcp?> GetByIdAsync(Guid id, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task<IntegrationExternalMcp?> GetByNameAsync(string name, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task<IntegrationExternalMcp> SaveAsync(IntegrationExternalMcp entity, uint? expectedVersion, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task DeleteAsync(IntegrationExternalMcp entity, uint expectedVersion, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
    }

    private sealed class NoRegistry : IExternalToolRegistry
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<ExternalToolResponseDto>> PreviewAsync(string name, ExternalMcpConfigDto config, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task<IReadOnlyList<ExternalToolCandidateModel>> GetCandidatesAsync(CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task<ExternalToolCandidateModel?> ResolveAsync(ExternalToolRouteModel route, bool refresh, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task<ExternalMcpDiscoveryDto> GetSummaryAsync(IntegrationExternalMcp connection, CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
        public Task RefreshAsync(CancellationToken cancellationToken) { Calls++; throw new NotSupportedException(); }
    }
}
