using ContextHub.Application.Validators.Telemetry;
using ContextHub.Cache.Services;
using ContextHub.ApiCommon.Options;
using AuthAccessor = ContextHub.ApiCommon.Services.Authentication.AuthService;
using Mapster;
using Microsoft.Extensions.Logging.Abstractions;
using ContextHub.Application.Dtos.Telemetry;
using ContextHub.Application.Dtos.Mcp;
using ContextHub.Application.Models.Telemetry;
using ContextHub.Application.Exceptions.Mcp;
using ContextHub.Infrastructure.Context.Catalog;
using ContextHub.Infrastructure.Mappings.Telemetry;
using ContextHub.Infrastructure.Repositories.Telemetry;
using ContextHub.Infrastructure.Repositories.Mcp;
using ContextHub.Infrastructure.Services.Telemetry;
using ContextHub.Infrastructure.Services.Mcp;
using ContextHub.Core.Factories.Persistence;
using HubPackage = ContextHub.Domain.Entities.Package;
using HubVersion = ContextHub.Domain.Entities.PackageVersion;
using ContextHub.Api.Configurations.Mcp;
using ContextHub.Application.Interfaces.Telemetry;
using ContextHub.Application.Interfaces.Mcp;
using ContextHub.Application.Models.Mcp;
using ModelContextProtocol;
using ModelContextProtocol.Client;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises atomic telemetry, review projection and concurrency against the actual DB-first design scripts.
/// </summary>
[Collection("GlobalMappingCollection")]
public sealed partial class TelemetryPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("postgres").WithUsername("postgres").WithPassword("postgres").Build();
    private NpgsqlDataSource _dataSource = null!;
    private string _connectionString = "";
    private readonly Guid _actor = Guid.NewGuid();

    /// <summary>
    /// Applies the checked-in guarded design scripts to a new isolated review database.
    /// </summary>
    /// <returns>The disposable database initialization.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "database", "scripts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var scriptRoot = Path.Combine(directory.FullName, "Documents", "database", "scripts");
        foreach (var name in new[] { "01-create-database.sql", "02-seed-data.sql" })
        {
            await _container.CopyAsync(File.ReadAllBytes(Path.Combine(scriptRoot, name)), "/tmp/" + name);
        }

        const string database = "context_hub_telemetry_design_review";
        var create = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "postgres", "-v", "review_only_ack=true",
            "-v", "database_name=" + database, "-f", "/tmp/01-create-database.sql"]);
        Assert.True(create.ExitCode == 0, create.Stderr + create.Stdout);
        var seed = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", database, "-v", "review_only_ack=true", "-f", "/tmp/02-seed-data.sql"]);
        Assert.True(seed.ExitCode == 0, seed.Stderr + seed.Stdout);
        _connectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString;
        var source = new NpgsqlDataSourceBuilder(_connectionString);
        source.UseVector();
        _dataSource = source.Build();
        new TelemetryMapping().Register(TypeAdapterConfig.GlobalSettings);
        new ContextHub.Infrastructure.Mappings.Mcp.McpMapping().Register(TypeAdapterConfig.GlobalSettings);
    }

    /// <summary>
    /// Disposes only this fixture's pooled connections and container.
    /// </summary>
    /// <returns>The completed isolated resource cleanup.</returns>
    public async Task DisposeAsync()
    {
        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Concurrent nullable-key increments retain every actual tool outcome and open one daily reliability item.
    /// </summary>
    /// <returns>The independent database count assertions.</returns>
    [Fact]
    public async Task ConcurrentActualOutcomesUseAtomicNullableDimensionsAndOneReliabilityItem()
    {
        var client = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(async index =>
        {
            await using var context = CreateContext();
            await new UsageTelemetryRepository(context).RecordAsync(new ToolOutcomeModel
            {
                ClientId = client, ToolName = "catalog_search", Succeeded = index >= 4
            }, CancellationToken.None);
        }));

        await using var connection = await _dataSource.OpenConnectionAsync();
        var counts = await connection.QuerySingleAsync<(long TotalCount, long SuccessCount, long ErrorCount)>(
            "SELECT \"TotalCount\",\"SuccessCount\",\"ErrorCount\" FROM telemetry.\"DailyUsage\"");
        Assert.Equal((20L, 16L, 4L), counts);
        Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\" WHERE \"Origin\"='system_reliability' AND \"DedupeKey\" IS NULL"));

        await using var subsequent = CreateContext();
        await new UsageTelemetryRepository(subsequent).RecordAsync(new() { ClientId = client, ToolName = "catalog_search", Succeeded = false }, CancellationToken.None);
        Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\" WHERE \"Origin\"='system_reliability'"));
    }

    /// <summary>
    /// Reliability requires both the minimum observation count and the configured failure ratio.
    /// </summary>
    /// <returns>The below-boundary and threshold-crossing assertions.</returns>
    [Fact]
    public async Task ReliabilityDoesNotOpenBelowTheMinimumOrFailureRatio()
    {
        await using var context = CreateContext();
        var repository = new UsageTelemetryRepository(context);
        var client = Guid.NewGuid();
        for (var index = 0; index < 19; index++)
        {
            await repository.RecordAsync(new() { ClientId = client, ToolName = "catalog_search", Succeeded = false }, CancellationToken.None);
        }

        await using var connection = await _dataSource.OpenConnectionAsync();
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\""));
        await repository.RecordAsync(new() { ClientId = client, ToolName = "catalog_search", Succeeded = true }, CancellationToken.None);
        Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\""));

        // Only this disposable test database is reset so the independent ratio boundary starts from an empty aggregate.
        await connection.ExecuteAsync("DELETE FROM recommendation.\"Recommendation\"; DELETE FROM telemetry.\"DailyUsage\"");
        for (var index = 0; index < 20; index++)
        {
            await repository.RecordAsync(new() { ClientId = client, ToolName = "catalog_search", Succeeded = index >= 3 }, CancellationToken.None);
        }

        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\""));
        await repository.RecordAsync(new() { ClientId = client, ToolName = "catalog_search", Succeeded = false }, CancellationToken.None);
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\""));
        await repository.RecordAsync(new() { ClientId = client, ToolName = "catalog_search", Succeeded = false }, CancellationToken.None);
        Assert.Equal(1L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\""));
    }

    /// <summary>
    /// Merged client feedback retains an Owner decision and terminal feedback opens a distinct future item.
    /// </summary>
    /// <returns>The atomic dedupe and lifecycle assertions.</returns>
    [Fact]
    public async Task ClientSignalDedupePreservesOwnerStateAndDoesNotReuseTerminalItems()
    {
        await using var context = CreateContext();
        var repository = new QualitySignalRepository(context, CreateDapper());
        var request = new QualitySignalRequestDto { SignalType = "coverage_gap", IssueCode = "no_matching_package",
            ArtifactType = "code", Operation = "review", DesiredPackageType = "workflow" };
        var client = Guid.NewGuid();
        Assert.True(await repository.UpsertAsync(request, client, null, CancellationToken.None));
        var merged = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => repository.UpsertAsync(request, client, null, CancellationToken.None)));
        Assert.All(merged, value => Assert.False(value));
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("UPDATE recommendation.\"Recommendation\" SET \"Status\"='accepted'");
        Assert.False(await repository.UpsertAsync(request, client, null, CancellationToken.None));
        Assert.Equal("accepted", await connection.ExecuteScalarAsync<string>("SELECT \"Status\" FROM recommendation.\"Recommendation\""));
        Assert.Equal(10L, await connection.ExecuteScalarAsync<long>("SELECT \"SignalCount\" FROM recommendation.\"Recommendation\""));
        var unitOfWork = new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance);
        var reviews = new RecommendationRepository(context, unitOfWork, NullLogger<RecommendationRepository>.Instance);
        var codedContext = Assert.Single((await reviews.GetReviewPageAsync(new(), CancellationToken.None)).Items).Context;
        Assert.Equal("coverage_gap", codedContext!.Value.GetProperty("signalType").GetString());

        await connection.ExecuteAsync("UPDATE recommendation.\"Recommendation\" SET \"Status\"='completed'");
        Assert.True(await repository.UpsertAsync(request, client, null, CancellationToken.None));
        Assert.Equal(2L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM recommendation.\"Recommendation\""));
    }

    /// <summary>
    /// Missing authoritative state fails closed and stale native versions cannot overwrite an Owner change.
    /// </summary>
    /// <returns>The tool-state concurrency assertions.</returns>
    [Fact]
    public async Task ToolStateRejectsStaleVersionAndMissingRows()
    {
        await using var context = CreateContext();
        var unitOfWork = new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance);
        var service = new ToolStateService(new ToolRepository(context, unitOfWork, NullLogger<ToolRepository>.Instance));
        var initial = (await service.GetListAsync(CancellationToken.None)).Items.Single(value => value.Name == "bundle_resolve");
        var request = new UpdateMcpToolRequestDto { IsEnabled = false, ConcurrencyToken = initial.ConcurrencyToken,
            Title = "Owner title", Description = "Owner description" };
        Assert.False((await service.UpdateAsync(initial.Name, request, CancellationToken.None)).IsEnabled);
        var saved = (await service.GetListAsync(CancellationToken.None)).Items.Single(value => value.Name == initial.Name);
        Assert.Equal("Owner title", saved.Title);
        Assert.True(saved.UpdatedAt > DateTimeOffset.MinValue);
        var definition = new McpContractRegistry(await service.GetContractsAsync(initial.Name, CancellationToken.None)).GetDefinition(initial.Name);
        Assert.Equal("Owner title", definition.Title);
        Assert.Equal("Owner description", definition.Description);
        Assert.True(definition.Annotations!.ReadOnlyHint);
        Assert.DoesNotContain(initial.Name, await service.GetEnabledNamesAsync(CancellationToken.None));
        await Assert.ThrowsAsync<BusinessException>(() => service.UpdateAsync(initial.Name, request, CancellationToken.None));
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("DELETE FROM access.\"Tool\" WHERE \"Name\"='package_get'");
        await Assert.ThrowsAsync<ToolStateUnavailableException>(() => service.IsEnabledAsync("package_get", CancellationToken.None));
        await Assert.ThrowsAsync<ToolStateUnavailableException>(() => service.GetListAsync(CancellationToken.None));
        await Assert.ThrowsAsync<ToolStateUnavailableException>(() => service.GetEnabledNamesAsync(CancellationToken.None));
    }

    /// <summary>
    /// Uses actual JSONB updates for subsequent SDK discovery and validation without restarting the host.
    /// </summary>
    /// <returns>The database, protocol and request-snapshot assertions.</returns>
    [Fact]
    public async Task DatabaseSchemasDriveDiscoveryAndValidationOnTheNextRequest()
    {
        await using var app = await McpTransportTests.CreateHostAsync(
            new McpTransportTests.IntrospectionHandler(),
            configureServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton(_dataSource));
                services.Replace(ServiceDescriptor.Scoped<IToolStateService, ToolStateService>());
            });
        using var http = app.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://hub.test/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport,
            new McpClientOptions { ProtocolVersion = ContextHub.Api.Constants.HubConstants.PROTOCOL_VERSION });
        var original = Assert.Single(await client.ListToolsAsync(), tool => tool.Name == "catalog_search");
        Assert.False(original.JsonSchema.TryGetProperty("description", out _));

        await using var context = CreateContext();
        var service = new ToolStateService(new ToolRepository(context,
            new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance),
            NullLogger<ToolRepository>.Instance));
        var before = new McpContractRegistry(await service.GetContractsAsync("catalog_search", CancellationToken.None));
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            UPDATE access."Tool"
            SET "InputSchema" = '{"type":"object","description":"Database contract revision","required":["marker"],"properties":{"marker":{"type":"string"}}}'::jsonb
            WHERE "Name" = 'catalog_search'
            """);

        var changed = Assert.Single(await client.ListToolsAsync(), tool => tool.Name == "catalog_search");
        Assert.Equal("Database contract revision", changed.JsonSchema.GetProperty("description").GetString());
        var rejected = await client.CallToolAsync("catalog_search", new Dictionary<string, object?> { ["query"] = "review" });
        Assert.True(rejected.IsError);
        Assert.Equal("validation_error", rejected.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.True(before.IsValidInput("catalog_search", JsonNode.Parse("{\"query\":\"review\"}")!));

        // A malformed contract cannot fall back to the previously embedded definitions.
        await connection.ExecuteAsync("""UPDATE access."Tool" SET "InputSchema" = '{"type":42}'::jsonb WHERE "Name" = 'catalog_search'""");
        var failure = await Assert.ThrowsAsync<McpProtocolException>(() => client.ListToolsAsync().AsTask());
        Assert.DoesNotContain("42", failure.Message);

        await connection.ExecuteAsync("""UPDATE access."Tool" SET "InputSchema" = '{"type":"object"}'::jsonb WHERE "Name" = 'managed_tool_search'""");
        var missing = await client.CallToolAsync("managed_tool_search", new Dictionary<string, object?>());
        Assert.True(missing.IsError);
        Assert.Equal("validation_error", missing.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());

        await connection.ExecuteAsync("""
            UPDATE access."Tool" SET "InputSchema" = '{"type":"object"}'::jsonb
            WHERE "Name" IN ('catalog_search', 'compatibility_check', 'library_documentation_search')
            """);
        foreach (var (name, arguments) in new[]
        {
            ("catalog_search", new Dictionary<string, object?> { ["query"] = null }),
            ("compatibility_check", new Dictionary<string, object?> { ["client_capabilities"] = null }),
            ("library_documentation_search", new Dictionary<string, object?>())
        })
        {
            var invalid = await client.CallToolAsync(name, arguments);
            Assert.True(invalid.IsError);
            Assert.Equal("validation_error", invalid.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        }

        await connection.ExecuteAsync("""
            UPDATE access."Tool" SET "InputSchema" = '{"type":"object"}'::jsonb,
                "OutputSchema" = '{"not":{}}'::jsonb WHERE "Name" = 'catalog_search'
            """);
        await Assert.ThrowsAsync<McpProtocolException>(() => client.CallToolAsync("catalog_search",
            new Dictionary<string, object?> { ["query"] = "review" }).AsTask());
    }

    /// <summary>
    /// Keeps each compiled pair stable while later reads observe database contract updates.
    /// </summary>
    /// <returns>The paired schema and narrow SQL projection assertions.</returns>
    [Fact]
    public async Task ContractPairsAreAtomicAndStateReadsDoNotLoadSchemas()
    {
        await using var context = CreateContext();
        var configuration = new TypeAdapterConfig();
        new TelemetryMapping().Register(configuration);
        var stateSql = context.Tools.AsNoTracking()
            .ProjectToType<ContextHub.Infrastructure.Models.Mcp.ToolStateModel>(configuration).ToQueryString();
        Assert.DoesNotContain("InputSchema", stateSql);
        Assert.DoesNotContain("OutputSchema", stateSql);

        var service = new ToolStateService(new ToolRepository(context,
            new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance),
            NullLogger<ToolRepository>.Instance));
        var original = Assert.Single(await service.GetContractsAsync("package_get", CancellationToken.None));
        var snapshot = new McpContractRegistry([original]);
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("""
            UPDATE access."Tool"
            SET "InputSchema" = '{"type":"object","required":["new_input"]}'::jsonb,
                "OutputSchema" = '{"type":"object","required":["new_output"]}'::jsonb
            WHERE "Name" = 'package_get'
            """);
        var current = Assert.Single(await service.GetContractsAsync("package_get", CancellationToken.None));
        Assert.NotEqual(original.InputSchema, current.InputSchema);
        Assert.NotEqual(original.OutputSchema, current.OutputSchema);
        Assert.True(snapshot.IsValidInput("package_get", JsonNode.Parse("{\"package_id\":\"contexthub.test\"}")!));
        Assert.False(new McpContractRegistry([current]).IsValidInput("package_get", JsonNode.Parse("{\"package_id\":\"contexthub.test\"}")!));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(original.OutputSchema),
            JsonNode.Parse(snapshot.GetDefinition("package_get").OutputSchema!.Value.GetRawText())));
        await connection.ExecuteAsync("DELETE FROM access.\"Tool\" WHERE \"Name\"='package_get'");
        await Assert.ThrowsAsync<ToolStateUnavailableException>(() => service.GetContractsAsync("package_get", CancellationToken.None));
    }

    /// <summary>
    /// Database-backed review projection returns public package identifiers and never broadens a missing package filter.
    /// </summary>
    /// <returns>The provider projection and filtering assertions.</returns>
    [Fact]
    public async Task ReviewProjectionUsesPublicKeysAndUnknownPackageFilterIsEmpty()
    {
        await using var context = CreateContext();
        var package = new HubPackage { Id = Guid.NewGuid(), Key = "demo.telemetry", Type = "skill", Name = "Telemetry Demo",
            Summary = "A public test package.", IsActive = true, CreatedBy = _actor, UpdatedBy = _actor,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        context.Packages.Add(package);
        await context.SaveChangesAsync();
        var unitOfWork = new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance);
        var repository = new RecommendationRepository(context, unitOfWork, NullLogger<RecommendationRepository>.Instance);
        var created = await repository.CreateReviewAsync(new() { PackageId = package.Key, RecommendationType = "package_improvement",
            Title = "Improve the example", Description = "Add a verified example." }, CancellationToken.None);
        Assert.Equal(package.Key, created.PackageId);
        var page = await repository.GetReviewPageAsync(new(), CancellationToken.None);
        Assert.Equal(package.Key, Assert.Single(page.Items).PackageId);
        Assert.Empty((await repository.GetReviewPageAsync(new() { PackageId = "missing.package" }, CancellationToken.None)).Items);
    }

    /// <summary>
    /// Redis enforces one application budget across distinct browser principals and blocks writes when coordination is unavailable.
    /// </summary>
    /// <returns>The shared budget and outage assertions.</returns>
    [Fact]
    public async Task QualityBudgetUsesRedisAcrossBrowsersAndFailsClosedOnOutage()
    {
        await using var redis = new RedisBuilder("redis:7.4-alpine").Build();
        await redis.StartAsync();
        var options = ConfigurationOptions.Parse(redis.GetConnectionString());
        options.AsyncTimeout = 1000;
        options.ConnectTimeout = 1000;
        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        var atomic = new AtomicCacheService(connection, NullLogger<AtomicCacheService>.Instance);
        var client = Guid.NewGuid();
        var request = new QualitySignalRequestDto { SignalType = "coverage_gap", IssueCode = "no_matching_package",
            ArtifactType = "code", Operation = "review", DesiredPackageType = "workflow" };
        for (var attempt = 0; attempt < 10; attempt++)
        {
            await using var context = CreateContext();
            using var services = CreateQualityServices(context, atomic, client);
            var result = await services.GetRequiredService<IMediator>().Send(new SubmitQualitySignalCommand(request), CancellationToken.None);
            Assert.Equal(attempt == 0 ? "accepted" : "deduplicated", result.Status);
        }

        await using var rejectedContext = CreateContext();
        using var rejectedServices = CreateQualityServices(rejectedContext, atomic, client);
        await Assert.ThrowsAsync<QualitySignalRateLimitException>(() => rejectedServices.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(request), CancellationToken.None).AsTask());
        await using var database = await _dataSource.OpenConnectionAsync();
        Assert.Equal(10L, await database.ExecuteScalarAsync<long>("SELECT \"SignalCount\" FROM recommendation.\"Recommendation\""));

        await redis.StopAsync();
        await using var outageContext = CreateContext();
        using var outageServices = CreateQualityServices(outageContext, atomic, Guid.NewGuid());
        var failure = await Record.ExceptionAsync(() => outageServices.GetRequiredService<IMediator>()
            .Send(new SubmitQualitySignalCommand(request), CancellationToken.None).AsTask());
        var unavailable = Assert.IsType<DependencyUnavailableException>(failure);
        Assert.True(unavailable.InnerException is RedisException or RedisTimeoutException);
        Assert.Equal(10L, await database.ExecuteScalarAsync<long>("SELECT \"SignalCount\" FROM recommendation.\"Recommendation\""));
    }

    /// <summary>
    /// Creates a separate browser's actual authorization, validation and submission pipeline.
    /// </summary>
    /// <param name="context">The browser operation's context.</param>
    /// <param name="atomic">The fixture-owned native Redis coordination service.</param>
    /// <param name="client">The already authenticated application identity.</param>
    /// <returns>The caller-owned container over real Redis and PostgreSQL adapters.</returns>
    private ServiceProvider CreateQualityServices(ContextHubDbContext context, IAtomicCacheService atomic, Guid client)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("actor_type", "client"), new Claim("client_id", client.ToString()),
            new Claim("scope", "contexthub.mcp"), new Claim("tool", "quality_signal_submit")
        }, "fixture"));
        var accessor = new AuthAccessor(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } },
            Options.Create(new CurrentUserOptions()), NullLogger<AuthAccessor>.Instance);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddContextHubApplication();
        services.AddAuthorizationBehavior().AddValidationBehavior();
        services.AddSingleton<IAuthService>(accessor);
        services.AddScoped<IQualitySignalService>(_ => new QualitySignalService(
            new QualitySignalRepository(context, CreateDapper()), atomic, NullLogger<QualitySignalService>.Instance));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a native vector-aware context over the SQL-created schema without migrations.
    /// </summary>
    /// <returns>A caller-owned scoped context.</returns>
    private ContextHubDbContext CreateContext() => new(new DbContextOptionsBuilder<ContextHubDbContext>()
        .UseNpgsql(_dataSource, options => options.UseVector()).Options);

    /// <summary>
    /// Reuses the real shared Dapper facade with the fixture-owned connection pool.
    /// </summary>
    /// <returns>The native command service.</returns>
    private DapperService CreateDapper() => new(new DbConnectionFactory(_dataSource), NullLogger<DapperService>.Instance);
}

