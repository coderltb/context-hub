using ContextHub.Application.Queries.Mcp;
using ContextHub.Application.Interfaces.Integration;
using ContextHub.Application.Interfaces.Telemetry;
using ContextHub.Application.Interfaces.Mcp;
using ContextHub.Core.Interfaces.Authentication;
using ContextHub.Infrastructure.Helpers.Catalog;
using System.Reflection;
using ContextHub.Api.Adapters.Mcp;
using ContextHub.Api.Configurations.Mcp;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using ContextHub.Application.Interfaces.Catalog;
using ContextHub.Application.Policies.Catalog;
using ContextHub.Cache.DependencyInjection;
using ContextHub.Core.Factories.Persistence;
using ContextHub.Core.Interceptors.Auditing;
using ContextHub.Core.Interfaces.Auditing;
using ContextHub.Core.Services.Persistence;
using ContextHub.Infrastructure.Context.Catalog;
using ContextHub.Infrastructure.Mappings.Catalog;
using ContextHub.Infrastructure.Repositories.Catalog;
using ContextHub.Infrastructure.Services.Catalog;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Mapster;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises immutable publication, live lifecycle checks and SQL search against the checked-in DB-first schema.
/// </summary>
[Collection("GlobalMappingCollection")]
public sealed class CatalogPostgreSqlTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("postgres").WithUsername("postgres").WithPassword("postgres").Build();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly FixtureSourceReader _sourceReader = new();
    private NpgsqlDataSource _dataSource = null!;
    private ServiceProvider _cacheProvider = null!;

    /// <summary>
    /// Applies actual design and routine SQL to an isolated PostgreSQL database instead of using EF schema creation.
    /// </summary>
    /// <returns>The schema and native cache initialization.</returns>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "database", "scripts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        foreach (var name in new[] { "01-create-database.sql", "02-seed-data.sql", "03-catalog-search-v1.sql" })
        {
            await _container.CopyAsync(File.ReadAllBytes(Path.Combine(directory.FullName, "Documents", "database", "scripts", name)), "/tmp/" + name);
        }

        const string database = "context_hub_catalog_design_review";
        var create = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "postgres", "-v", "review_only_ack=true",
            "-v", "database_name=" + database, "-f", "/tmp/01-create-database.sql"]);
        Assert.True(create.ExitCode == 0, create.Stderr + create.Stdout);
        foreach (var name in new[] { "02-seed-data.sql", "03-catalog-search-v1.sql" })
        {
            var apply = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", database, "-v", "review_only_ack=true", "-f", "/tmp/" + name]);
            Assert.True(apply.ExitCode == 0, apply.Stderr + apply.Stdout);
        }

        var source = new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = database }.ConnectionString);
        source.UseVector();
        _dataSource = source.Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCache();
        _cacheProvider = services.BuildServiceProvider();
        new CatalogMapping().Register(TypeAdapterConfig.GlobalSettings);
    }

    /// <summary>
    /// Releases only the pool, memory cache and container owned by this test.
    /// </summary>
    /// <returns>The bounded fixture cleanup.</returns>
    public async Task DisposeAsync()
    {
        if (_cacheProvider is not null)
        {
            await _cacheProvider.DisposeAsync();
        }

        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        await _container.DisposeAsync();
    }

    /// <summary>
    /// Preserves immutable bytes through idempotent publication, numeric ordering and terminal revocation.
    /// </summary>
    /// <returns>The saved data and live cached-content assertions.</returns>
    [Fact]
    public async Task PublicationAndRevocationKeepImmutableContentAndRecheckCachedAuthority()
    {
        const string packageId = "contexthub.catalog-review";
        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            await service.CreatePackageAsync(new() { PackageId = packageId, PackageType = "rule", DisplayName = "Review", Summary = "API review guidance." }, CancellationToken.None);
            var first = AddSource(packageId, "1.9.0", '1');
            var preview = await service.PreviewAsync(packageId, first, CancellationToken.None);
            Assert.Equal("ready", preview.Readiness.Status);
            Assert.Equal(0, await context.Versions.CountAsync());
            var original = await service.PublishAsync(packageId, first, CancellationToken.None);
            var repeated = await service.PublishAsync(packageId, first, CancellationToken.None);
            Assert.Equal(original.ContentHash, repeated.ContentHash);
            Assert.Equal(original.PublishedAt, repeated.PublishedAt);
            await service.PublishAsync(packageId, AddSource(packageId, "1.10.0", '2'), CancellationToken.None);
            var page = await service.GetVersionsAsync(packageId, new() { Sort = "-version" }, CancellationToken.None);
            Assert.Equal(new[] { "1.10.0", "1.9.0" }, page.Items.Select(value => value.Version));
            var latest = await service.GetPublicPackageAsync(new() { PackageId = packageId }, CancellationToken.None);
            Assert.Equal("1.10.0", latest["version"]!.GetValue<string>());
            Assert.False(latest["manifest"]!.AsObject().ContainsKey("authoring_source"));
            Assert.True(McpContractFixture.CreateRegistry().IsValidOutput("package_get", latest));
        }

        // A second scope changes authority while the first release's public content remains in the shared cache.
        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            var current = await service.GetVersionAsync(packageId, "1.10.0", CancellationToken.None);
            Assert.Equal(current.PublishedAt, current.Manifest["published_at"]!.GetValue<DateTimeOffset>());
            var revoked = await service.RevokeAsync(packageId, "1.10.0", new() { ConcurrencyToken = current.ConcurrencyToken }, CancellationToken.None);
            Assert.Equal(current.ContentHash, revoked.ContentHash);
            Assert.True(JsonNode.DeepEquals(current.Manifest, revoked.Manifest));
            Assert.True(JsonNode.DeepEquals(current.Bundle, revoked.Bundle));
        }

        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            var failure = await Assert.ThrowsAsync<BusinessException>(() => service.GetPublicPackageAsync(
                new() { PackageId = packageId, Version = "1.10.0" }, CancellationToken.None));
            Assert.Equal("package_revoked", failure.ErrorCode);
            var fallback = await service.GetPublicPackageAsync(new() { PackageId = packageId }, CancellationToken.None);
            Assert.Equal("1.9.0", fallback["version"]!.GetValue<string>());
            var status = await service.GetRevocationsAsync(new() { PackageId = packageId, Version = "1.10.0" }, CancellationToken.None);
            Assert.Equal("revoked", status["status"]!.GetValue<string>());
            Assert.True(McpContractFixture.CreateRegistry().IsValidOutput("revocation_check", status));
            var revocation = Assert.Single((await service.GetRevocationsIndexAsync(CancellationToken.None)).AsArray());
            Assert.Equal(packageId, revocation!["package_id"]!.GetValue<string>());
            Assert.Equal("1.10.0", revocation["version"]!.GetValue<string>());
            Assert.False(revocation.AsObject().ContainsKey("manifest"));
            Assert.Empty(context.ChangeTracker.Entries());
        }
    }

    /// <summary>
    /// Orders dependencies before their consumer and refuses cached roots when a dependency becomes revoked.
    /// </summary>
    /// <returns>The graph, capability and live-state assertions.</returns>
    [Fact]
    public async Task DependencyClosureIsDeterministicAndLiveStateRemainsAuthoritative()
    {
        const string dependency = "contexthub.catalog-dependency";
        const string root = "contexthub.catalog-root";
        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            foreach (var key in new[] { root, dependency })
            {
                await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = key, Summary = "API review dependency." }, CancellationToken.None);
            }

            await service.PublishAsync(dependency, AddSource(dependency, "1.0.0", '3'), CancellationToken.None);
            await service.PublishAsync(root, AddSource(root, "1.0.0", '4', dependency), CancellationToken.None);
            var resolved = await service.ResolveAsync(new() { PackageId = root }, CancellationToken.None);
            Assert.Equal(new[] { dependency, root }, resolved["packages"]!.AsArray().Select(value => value!["package_id"]!.GetValue<string>()));
            var compatibility = await service.CheckCompatibilityAsync(new() { PackageId = root, ClientCapabilities = [] }, CancellationToken.None);
            Assert.False(compatibility["compatible"]!.GetValue<bool>());
            Assert.Single(compatibility["issues"]!.AsArray());
            var contracts = McpContractFixture.CreateRegistry();
            Assert.True(contracts.IsValidOutput("bundle_resolve", resolved));
            Assert.True(contracts.IsValidOutput("compatibility_check", compatibility));
        }

        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            var current = await service.GetVersionAsync(dependency, "1.0.0", CancellationToken.None);
            await service.RevokeAsync(dependency, "1.0.0", new() { ConcurrencyToken = current.ConcurrencyToken }, CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            var failure = await Assert.ThrowsAsync<BusinessException>(() => CreateService(context).GetPublicContentAsync(
                new() { PackageId = root, Version = "1.0.0" }, CancellationToken.None));
            Assert.Equal("package_revoked", failure.ErrorCode);
        }
    }

    /// <summary>
    /// Returns a conflict for immutable replacement and stale metadata while leaving the committed release untouched.
    /// </summary>
    /// <returns>The exact database and optimistic concurrency assertions.</returns>
    [Fact]
    public async Task ConflictingPublicationAndConcurrentMetadataUpdateDoNotOverwriteCommittedData()
    {
        const string key = "contexthub.catalog-conflict";
        await using (var context = CreateContext())
        {
            var service = CreateService(context);
            await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "Original", Summary = "Initial metadata." }, CancellationToken.None);
            await service.PublishAsync(key, AddSource(key, "1.0.0", '5'), CancellationToken.None);
        }

        await using var firstContext = CreateContext();
        await using var secondContext = CreateContext();
        var first = CreateService(firstContext);
        var second = CreateService(secondContext);
        var snapshot = await first.GetPackageDetailAsync(key, CancellationToken.None);
        await second.GetPackageDetailAsync(key, CancellationToken.None);
        await first.UpdatePackageAsync(key, new() { DisplayName = "Saved", Summary = "Saved metadata.", IsActive = true, ConcurrencyToken = snapshot.ConcurrencyToken }, CancellationToken.None);
        var conflict = await Assert.ThrowsAsync<BusinessException>(() => second.UpdatePackageAsync(key,
            new() { DisplayName = "Stale", Summary = "Stale metadata.", IsActive = true, ConcurrencyToken = snapshot.ConcurrencyToken }, CancellationToken.None));
        Assert.Equal(BusinessErrorKind.Conflict, conflict.Kind);

        await using var publishContext = CreateContext();
        var publicationConflict = await Assert.ThrowsAsync<BusinessException>(() => CreateService(publishContext).PublishAsync(
            key, AddSource(key, "1.0.0", '6'), CancellationToken.None));
        Assert.Equal("version_conflict", publicationConflict.ErrorCode);
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Versions.CountAsync());
        Assert.Equal("Saved", await verify.Packages.Select(value => value.Name).SingleAsync());
    }

    /// <summary>
    /// Executes the real lexical routine, chooses the newest eligible release before filtering and protects cursor inputs.
    /// </summary>
    /// <returns>The SQL search and cursor-rebinding assertions.</returns>
    [Fact]
    public async Task SearchUsesDesignRoutineAndRejectsContinuationForDifferentInput()
    {
        await using var context = CreateContext();
        var service = CreateService(context);
        foreach (var (key, digit) in new[] { ("contexthub.catalog-search-one", '7'), ("contexthub.catalog-search-two", '8') })
        {
            await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "API review", Summary = "Review API endpoints." }, CancellationToken.None);
            await service.PublishAsync(key, AddSource(key, "1.0.0", digit), CancellationToken.None);
        }

        var page = await service.SearchAsync(new() { Query = "API review", Limit = 1 }, CancellationToken.None);
        Assert.Single(page.Items);
        Assert.NotNull(page.NextCursor);
        var next = await service.SearchAsync(new() { Query = "API review", Limit = 1, Cursor = page.NextCursor }, CancellationToken.None);
        Assert.Single(next.Items);
        Assert.NotEqual(page.Items[0].PackageId, next.Items[0].PackageId);
        var failure = await Assert.ThrowsAsync<BusinessException>(() => service.SearchAsync(
            new() { Query = "different intent", Limit = 1, Cursor = page.NextCursor }, CancellationToken.None));
        Assert.Equal("cursor_invalid", failure.ErrorCode);
    }

    /// <summary>
    /// Reports a self dependency as a cycle before any immutable release is inserted.
    /// </summary>
    /// <returns>The blocked preview and unchanged release table assertions.</returns>
    [Fact]
    public async Task SelfDependencyIsACycleAndPreviewDoesNotPersistTheCandidate()
    {
        const string key = "contexthub.catalog-cycle";
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "Cycle", Summary = "Cycle review." }, CancellationToken.None);
        var preview = await service.PreviewAsync(key, AddSource(key, "1.0.0", '9', key), CancellationToken.None);
        Assert.Equal("blocked", preview.Readiness.Status);
        Assert.Contains(preview.Readiness.Findings, value => value.Code == "dependency_cycle");
        Assert.Equal(0, await context.Versions.CountAsync());
    }

    /// <summary>
    /// Serializes only at PostgreSQL's unique release constraint when two prepared publications arrive together.
    /// </summary>
    /// <param name="differentContent">Whether the competing immutable manifests intentionally differ.</param>
    /// <returns>The committed release count and rollback/idempotency assertions.</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ParallelPublicationIsIdempotentOrConflictsWithoutPartialRows(bool differentContent)
    {
        const string key = "contexthub.catalog-parallel";
        await using (var context = CreateContext())
        {
            await CreateService(context).CreatePackageAsync(new()
            {
                PackageId = key, PackageType = "rule", DisplayName = "Parallel review", Summary = "Concurrent publication review."
            }, CancellationToken.None);
        }

        var firstRequest = AddSource(key, "1.0.0", 'a');
        var secondRequest = differentContent ? AddSource(key, "1.0.0", 'b') : firstRequest;
        var barrier = new PublicationInsertBarrier();
        await using var firstContext = CreateContext(barrier);
        await using var secondContext = CreateContext(barrier);
        var first = PublishAsync(firstContext, firstRequest);
        var second = PublishAsync(secondContext, secondRequest);
        var failures = await Task.WhenAll(first, second);
        if (differentContent)
        {
            var conflict = Assert.Single(failures, value => value is not null);
            Assert.Equal("version_conflict", Assert.IsType<BusinessException>(conflict).ErrorCode);
        }
        else
        {
            Assert.All(failures, value => Assert.Null(value));
        }

        Assert.Null(firstContext.Database.CurrentTransaction);
        Assert.Null(secondContext.Database.CurrentTransaction);
        await using var verify = CreateContext();
        Assert.Equal(1, await verify.Packages.CountAsync());
        Assert.Equal(1, await verify.Versions.CountAsync());
        var release = await verify.Versions.SingleAsync();
        Assert.Equal("published", release.Status);
        Assert.Contains(release.SourceCommitSha, new[] { firstRequest.SourceCommitSha, secondRequest.SourceCommitSha });

        async Task<Exception?> PublishAsync(ContextHubDbContext context, PublishVersionRequestDto request) =>
            await Record.ExceptionAsync(() => CreateService(context).PublishAsync(key, request, CancellationToken.None));
    }

    /// <summary>
    /// Falls back to PostgreSQL during a real Redis outage and still rejects a release revoked after content was cached.
    /// </summary>
    /// <returns>The cache outage and live-authority assertions.</returns>
    [Fact]
    public async Task RedisOutageDoesNotReplaceLivePostgreSqlAuthority()
    {
        await using var redis = new RedisBuilder("redis:7.4-alpine").Build();
        await redis.StartAsync();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRedisInfrastructure(options => options.Configuration = redis.GetConnectionString() +
            ",abortConnect=false,connectTimeout=1000,connectRetry=0,syncTimeout=1000,asyncTimeout=1000");
        services.AddCache(options => options.DefaultEntryOptions = new HybridCacheEntryOptions
        {
            Flags = HybridCacheEntryFlags.DisableLocalCacheRead
        });
        await using var provider = services.BuildServiceProvider();
        var cacheService = provider.GetRequiredService<ICacheService>();
        const string key = "contexthub.catalog-redis";
        await using (var context = CreateContext())
        {
            var service = CreateService(context, cacheService);
            await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "Cached review", Summary = "Cache outage review." }, CancellationToken.None);
            await service.PublishAsync(key, AddSource(key, "1.0.0", 'c'), CancellationToken.None);
            Assert.Equal("1.0.0", (await service.GetPublicPackageAsync(new() { PackageId = key }, CancellationToken.None))["version"]!.GetValue<string>());
        }

        await redis.StopAsync();
        await using (var context = CreateContext())
        {
            var service = CreateService(context, cacheService);
            var result = await service.GetPublicPackageAsync(new() { PackageId = key }, CancellationToken.None);
            Assert.Equal("1.0.0", result["version"]!.GetValue<string>());
            var current = await service.GetVersionAsync(key, "1.0.0", CancellationToken.None);
            await service.RevokeAsync(key, "1.0.0", new() { ConcurrencyToken = current.ConcurrencyToken }, CancellationToken.None);
        }

        await using (var context = CreateContext())
        {
            var failure = await Assert.ThrowsAsync<BusinessException>(() => CreateService(context, cacheService).GetPublicPackageAsync(
                new() { PackageId = key, Version = "1.0.0" }, CancellationToken.None));
            Assert.Equal("package_revoked", failure.ErrorCode);
        }
    }

    /// <summary>
    /// Updates search projections and reports the actual latest release without modifying immutable publication content.
    /// </summary>
    /// <returns>The metadata, lexical result and derived-vector assertions.</returns>
    [Fact]
    public async Task MetadataChangesRefreshSearchAndRetainTheLatestRelease()
    {
        const string key = "contexthub.catalog-metadata";
        await using var context = CreateContext();
        var service = CreateService(context, embeddingService: new MetadataEmbedding());
        var package = await service.CreatePackageAsync(new()
        {
            PackageId = key, PackageType = "rule", DisplayName = "AlphaUnique", Summary = "AlphaUnique instructions."
        }, CancellationToken.None);
        var published = await service.PublishAsync(key, AddSource(key, "1.0.0", 'd'), CancellationToken.None);
        var updated = await service.UpdatePackageAsync(key, new()
        {
            DisplayName = "BetaUnique", Summary = "BetaUnique instructions.", IsActive = true,
            ConcurrencyToken = package.ConcurrencyToken
        }, CancellationToken.None);
        var search = await service.SearchAsync(new() { Query = "BetaUnique" }, CancellationToken.None);
        Assert.True(updated.LatestVersion == "1.0.0" && search.Items.Count == 1 && search.Items[0].MatchReasons.Contains("lexical_match"),
            $"LatestVersion={updated.LatestVersion ?? "null"}; BetaUnique matches={search.Items.Count}");
        await using var verify = CreateContext();
        var persisted = await verify.Versions.SingleAsync();
        Assert.Equal(published.ContentHash, persisted.ContentHash);
        Assert.Equal(published.SourceCommitSha, persisted.SourceCommitSha);
        Assert.True(JsonNode.DeepEquals(published.Manifest, JsonNode.Parse(persisted.Manifest)));
        Assert.True(JsonNode.DeepEquals(published.Bundle, JsonNode.Parse(persisted.Bundle)));
        Assert.Contains("BetaUnique", persisted.SearchText);
        Assert.DoesNotContain("AlphaUnique", persisted.SearchText);
        Assert.Equal(1f, persisted.Embedding![1]);
        Assert.Equal(0f, persisted.Embedding[0]);
    }

    /// <summary>
    /// Prevents a concurrent publication from retaining old metadata while a rename commits successfully.
    /// </summary>
    /// <returns>The conflict and persisted projection consistency assertions.</returns>
    [Fact]
    public async Task MetadataChangeAndPublicationCannotCommitAnInconsistentDerivedProjection()
    {
        const string key = "contexthub.catalog-metadata-race";
        await using (var setup = CreateContext())
        {
            var service = CreateService(setup);
            await service.CreatePackageAsync(new()
            {
                PackageId = key, PackageType = "rule", DisplayName = "AlphaUnique", Summary = "AlphaUnique instructions."
            }, CancellationToken.None);
            await service.PublishAsync(key, AddSource(key, "1.0.0", 'e'), CancellationToken.None);
        }

        var barrier = new PackageUpdateBarrier();
        await using var metadataContext = CreateContext(barrier);
        var metadataService = CreateService(metadataContext);
        var current = await metadataService.GetPackageDetailAsync(key, CancellationToken.None);
        var rename = Record.ExceptionAsync(() => metadataService.UpdatePackageAsync(key, new()
        {
            DisplayName = "BetaUnique", Summary = "BetaUnique instructions.", IsActive = true,
            ConcurrencyToken = current.ConcurrencyToken
        }, CancellationToken.None));
        await barrier.Arrived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await using var publishContext = CreateContext();
        var publicationFailure = await Record.ExceptionAsync(() => CreateService(publishContext).PublishAsync(
            key, AddSource(key, "1.1.0", 'f'), CancellationToken.None));
        barrier.Release.TrySetResult();
        var renameFailure = await rename;
        Assert.True(renameFailure is BusinessException || publicationFailure is BusinessException,
            "Both operations committed although publication observed the previous package metadata.");
        await using var verify = CreateContext();
        var name = await verify.Packages.Select(value => value.Name).SingleAsync();
        var searchTexts = await verify.Versions.Select(value => value.SearchText).ToArrayAsync();
        Assert.All(searchTexts, text => Assert.Contains(name, text));
    }

    /// <summary>
    /// Converts a real stopped PostgreSQL authority into safe REST and MCP availability failures, even after content was cached.
    /// </summary>
    /// <returns>The native-provider, HTTP envelope and structured MCP error assertions.</returns>
    [Fact]
    public async Task StoppedPostgreSqlProducesSafeDependencyFailuresInsteadOfInternalErrors()
    {
        const string key = "contexthub.catalog-unavailable";
        await using (var setup = CreateContext())
        {
            var service = CreateService(setup);
            await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "Availability", Summary = "Availability review." }, CancellationToken.None);
            await service.PublishAsync(key, AddSource(key, "1.0.0", '1'), CancellationToken.None);
            await service.GetPublicPackageAsync(new() { PackageId = key }, CancellationToken.None);
        }

        await _container.StopAsync();
        Func<ICatalogService, Task>[] operations =
        [
            service => service.GetPackagesAsync(new(), CancellationToken.None),
            service => service.GetPackageDetailAsync(key, CancellationToken.None),
            service => service.GetVersionsAsync(key, new(), CancellationToken.None),
            service => service.SearchAsync(new() { Query = "availability" }, CancellationToken.None),
            service => service.GetPublicPackageAsync(new() { PackageId = key }, CancellationToken.None),
            service => service.GetRevocationsAsync(new() { PackageId = key }, CancellationToken.None),
            service => service.GetCatalogIndexAsync(CancellationToken.None),
            service => service.GetRevocationsIndexAsync(CancellationToken.None)
        ];
        foreach (var operation in operations)
        {
            await using var context = CreateContext();
            var unavailable = await Assert.ThrowsAsync<DependencyUnavailableException>(() => operation(CreateService(context)));
            Assert.NotNull(unavailable.InnerException);
            Assert.Equal(CatalogConstants.DATABASE_UNAVAILABLE, unavailable.ErrorCode);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.False(DatabaseExceptionHelper.IsUnavailable(unavailable.InnerException, canceled.Token));
        }

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiInfrastructure("/api/admin/v1", "1.0");
        await using var app = builder.Build();
        app.UseCorrelation();
        app.UseApiErrorHandling();
        app.MapGet("/api/admin/v1/catalog-availability", async (HttpContext http) =>
        {
            await using var context = CreateContext();
            await CreateService(context).GetPackagesAsync(new(), http.RequestAborted);
            return Results.Ok();
        });
        await app.StartAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/admin/v1/catalog-availability");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("error_dependency_unavailable", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("context_hub_catalog_design_review", body);

        await using var mcpContext = CreateContext();
        var handler = new ExecuteMcpToolQueryHandler(CreateService(mcpContext),
            DispatchProxy.Create<IManagedToolService, AvailabilityFixtureProxy>(),
            DispatchProxy.Create<IMediator, AvailabilityFixtureProxy>(),
            DispatchProxy.Create<IToolStateService, AvailabilityFixtureProxy>(),
            DispatchProxy.Create<IUsageTelemetryService, AvailabilityFixtureProxy>(),
            DispatchProxy.Create<IAuthService, AvailabilityFixtureProxy>(),
            NullLogger<ExecuteMcpToolQueryHandler>.Instance);
        using var arguments = JsonDocument.Parse("""{"query":"availability"}""");
        var result = await handler.Handle(new("catalog_search", arguments.RootElement.Clone()), CancellationToken.None);
        Assert.Equal("dependency_unavailable", result["error"]!["code"]!.GetValue<string>());
        Assert.True(result["error"]!["retryable"]!.GetValue<bool>());
        Assert.True(McpContractFixture.CreateRegistry().IsValidOutput("catalog_search", result));
        Assert.DoesNotContain("Npgsql", result.ToJsonString());
    }

    /// <summary>
    /// Keeps a genuine SQL defect and caller cancellation distinct from dependency availability.
    /// </summary>
    /// <returns>The provider SQL-state and native cancellation assertions.</returns>
    [Fact]
    public async Task DatabaseTranslationDoesNotHideSqlDefectsOrCancellation()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("ALTER TABLE catalog.\"Package\" RENAME COLUMN \"Summary\" TO \"RemovedSummary\"");
        await using var context = CreateContext();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => CreateService(context).GetPackagesAsync(new(), CancellationToken.None));
        Assert.Equal(PostgresErrorCodes.UndefinedColumn, failure.SqlState);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(context).GetPackagesAsync(new(), canceled.Token));
        Assert.False(DatabaseExceptionHelper.IsUnavailable(new InvalidOperationException("fixture programming defect"), CancellationToken.None));
    }

    /// <summary>
    /// Blocks actual credential assignments before preview or publication while retaining explicit placeholder guidance unchanged.
    /// </summary>
    /// <param name="instructions">The schema-valid instruction text.</param>
    /// <param name="permitted">Whether it contains only safe literal guidance.</param>
    /// <returns>The source decision and immutable-content assertions.</returns>
    [Theory]
    [InlineData("Use password=live-secret-value to connect.", false)]
    [InlineData("Configure api_key=live-provider-secret before running.", false)]
    [InlineData("Use password=<password> and api_key=<api-key> from your approved secret store.", true)]
    [InlineData("Read the password from an approved secret store; never print it.", true)]
    [InlineData("{\"password\":\"<password>\", \"safe\":true}", true)]
    [InlineData("Example: {\"password\":\"<password>\", \"safe\":true}", true)]
    public async Task SourceCredentialDetectionPreservesOnlySafeGuidance(string instructions, bool permitted)
    {
        const string key = "contexthub.catalog-secret-review";
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "Credential review", Summary = "Guidance review." }, CancellationToken.None);
        var request = AddSource(key, "1.0.0", '2');
        var source = _sourceReader.Sources[(key, request.SourceCommitSha)];
        var bundle = JsonNode.Parse(source.BundleJson)!.AsObject();
        bundle["instructions"] = instructions;
        _sourceReader.Sources[(key, request.SourceCommitSha)] = source with { BundleJson = bundle.ToJsonString() };
        var preview = await service.PreviewAsync(key, request, CancellationToken.None);
        if (permitted)
        {
            Assert.Equal("ready", preview.Readiness.Status);
            var published = await service.PublishAsync(key, request, CancellationToken.None);
            Assert.Equal(instructions, published.Bundle["instructions"]!.GetValue<string>());
        }
        else
        {
            Assert.Null(preview.PublicPreview);
            Assert.Null(preview.Readiness.CandidateContentHash);
            Assert.Contains(preview.Readiness.Findings, value => value.Code == "credentials_in_source");
            Assert.DoesNotContain("live-secret-value", JsonSerializer.Serialize(preview));
            Assert.DoesNotContain("live-provider-secret", JsonSerializer.Serialize(preview));
            await Assert.ThrowsAsync<BusinessException>(() => service.PublishAsync(key, request, CancellationToken.None));
            Assert.Equal(0, await context.Versions.CountAsync());
        }
    }

    /// <summary>
    /// Uses native PEM labels to block actual private key material without rejecting a public key example.
    /// </summary>
    /// <returns>The private/public PEM publication decisions.</returns>
    [Fact]
    public async Task PrivatePemIsBlockedButPublicPemRemainsGuidance()
    {
        const string key = "contexthub.catalog-pem-review";
        using var rsa = RSA.Create(2048);
        await using var context = CreateContext();
        var service = CreateService(context);
        await service.CreatePackageAsync(new() { PackageId = key, PackageType = "rule", DisplayName = "PEM review", Summary = "PEM guidance review." }, CancellationToken.None);
        var request = AddSource(key, "1.0.0", '3');
        var source = _sourceReader.Sources[(key, request.SourceCommitSha)];
        var bundle = JsonNode.Parse(source.BundleJson)!.AsObject();
        bundle["instructions"] = rsa.ExportPkcs8PrivateKeyPem();
        _sourceReader.Sources[(key, request.SourceCommitSha)] = source with { BundleJson = bundle.ToJsonString() };
        var preview = await service.PreviewAsync(key, request, CancellationToken.None);
        Assert.Null(preview.PublicPreview);
        Assert.Null(preview.Readiness.CandidateContentHash);
        await Assert.ThrowsAsync<BusinessException>(() => service.PublishAsync(key, request, CancellationToken.None));
        bundle["instructions"] = rsa.ExportSubjectPublicKeyInfoPem();
        _sourceReader.Sources[(key, request.SourceCommitSha)] = source with { BundleJson = bundle.ToJsonString() };
        Assert.Equal("ready", (await service.PreviewAsync(key, request, CancellationToken.None)).Readiness.Status);
    }

    /// <summary>
    /// Previews and publishes all four actual documentation bundles using their declared exact dependency closure.
    /// </summary>
    /// <returns>The publication-readiness and transitive Workflow uses assertions.</returns>
    [Fact]
    public async Task DocumentedBundlesPreviewAndPublishInDeclaredDependencyOrder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "reference", "packages", "examples")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var root = Path.Combine(directory.FullName, "Documents", "reference", "packages", "examples");
        var manifests = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "dotnet-api-review.manifests.json")))!.AsArray();
        await using var context = CreateContext();
        var service = CreateService(context);
        foreach (var manifest in manifests.Select(value => value!.AsObject()))
        {
            var key = manifest["package_id"]!.GetValue<string>();
            var kind = manifest["package_type"]!.GetValue<string>();
            var file = kind == "rule" ? "read-only-review-rule.bundle.json" : "dotnet-api-review-" + kind + ".bundle.json";
            var commit = manifest["authoring_source"]!["commit"]!.GetValue<string>();
            await service.CreatePackageAsync(new() { PackageId = key, PackageType = kind, DisplayName = "Documented review package", Summary = "Evidence-based review guidance." }, CancellationToken.None);
            _sourceReader.Sources[(key, commit)] = new(manifest.ToJsonString(), File.ReadAllText(Path.Combine(root, file)), commit);
            var request = new PublishVersionRequestDto
            {
                Version = manifest["version"]!.GetValue<string>(), AuthoringRepository = manifest["authoring_source"]!["repository"]!.GetValue<string>(),
                SourceCommitSha = commit, SourcePath = "packages/documented-review"
            };
            var preview = await service.PreviewAsync(key, request, CancellationToken.None);
            Assert.True(preview.Readiness.Status == "ready", key + ": " + JsonSerializer.Serialize(preview.Readiness));
            await service.PublishAsync(key, request, CancellationToken.None);
        }

        Assert.Equal(4, await context.Versions.CountAsync());
        var workflow = manifests.Single(value => value!["package_type"]!.GetValue<string>() == "workflow")!.AsObject();
        var workflowKey = workflow["package_id"]!.GetValue<string>();
        var candidate = (JsonObject)workflow.DeepClone();
        candidate["version"] = "1.1.0";
        var candidateCommit = new string('4', 40);
        candidate["authoring_source"]!["commit"] = candidateCommit;
        var workflowBundle = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "dotnet-api-review-workflow.bundle.json")))!.AsObject();
        var badUses = (JsonObject)workflowBundle.DeepClone();
        badUses["steps"]![1]!["uses"]!.AsArray().Add("contexthub.undeclared-guidance");
        await AssertBlockedAsync(candidate, badUses);

        var directMissing = (JsonObject)candidate.DeepClone();
        directMissing["dependencies"] = new JsonArray(new JsonObject { ["package_id"] = "contexthub.missing-direct", ["version"] = "1.0.0" });
        await AssertBlockedAsync(directMissing, workflowBundle);

        // The cached public dependency cannot hide a missing authoritative transitive release.
        await using (var connection = await _dataSource.OpenConnectionAsync())
        {
            await connection.ExecuteAsync("DELETE FROM catalog.\"Version\" WHERE \"PackageId\"=(SELECT \"Id\" FROM catalog.\"Package\" WHERE \"Key\"='contexthub.dotnet-api-review-skill')");
        }

        await AssertBlockedAsync(candidate, workflowBundle);
        Assert.Equal(3, await context.Versions.AsNoTracking().CountAsync());

        async Task AssertBlockedAsync(JsonObject manifest, JsonObject bundle)
        {
            _sourceReader.Sources[(workflowKey, candidateCommit)] = new(manifest.ToJsonString(), bundle.ToJsonString(), candidateCommit);
            var request = new PublishVersionRequestDto
            {
                Version = "1.1.0", AuthoringRepository = manifest["authoring_source"]!["repository"]!.GetValue<string>(),
                SourceCommitSha = candidateCommit, SourcePath = "packages/documented-review"
            };
            var preview = await service.PreviewAsync(workflowKey, request, CancellationToken.None);
            Assert.Equal("blocked", preview.Readiness.Status);
            Assert.Contains(preview.Readiness.Findings, finding => finding.Code == "dependency_missing");
            var failure = await Assert.ThrowsAsync<BusinessException>(() => service.PublishAsync(workflowKey, request, CancellationToken.None));
            Assert.Equal("dependency_missing", failure.ErrorCode);
        }
    }

    private ContextHubDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<ContextHubDbContext>()
            .UseNpgsql(_dataSource, options => options.UseVector())
            .AddInterceptors(new AuditSaveChangesInterceptor(TimeProvider.System, new FixtureActor(_actor),
                NullLogger<AuditSaveChangesInterceptor>.Instance)).AddInterceptors(interceptors).Options);

    private CatalogService CreateService(ContextHubDbContext context, ICacheService? cacheService = null, IEmbeddingService? embeddingService = null)
    {
        var packageRepository = new PackageRepository(context, NullLogger<PackageRepository>.Instance);
        var versionRepository = new PackageVersionRepository(context, NullLogger<PackageVersionRepository>.Instance);
        var unitOfWork = new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance);
        var dapper = new DapperService(new DbConnectionFactory(_dataSource), NullLogger<DapperService>.Instance);
        var search = new CatalogSearchService(dapper, embeddingService ?? new UnavailableEmbedding(), ProtectionProvider);
        var resolver = new CatalogResolverService(versionRepository, unitOfWork,
            cacheService ?? _cacheProvider.GetRequiredService<ICacheService>(), NullLogger<CatalogResolverService>.Instance);
        return new(packageRepository, versionRepository, _sourceReader, new CatalogContentValidator(), unitOfWork, search, resolver);
    }

    private static readonly IDataProtectionProvider ProtectionProvider = new EphemeralDataProtectionProvider();

    private PublishVersionRequestDto AddSource(string packageId, string version, char digit, string? dependency = null)
    {
        var commit = new string(digit, 40);
        var manifest = new JsonObject
        {
            ["schema_version"] = "1.0", ["package_id"] = packageId, ["package_type"] = "rule", ["version"] = version,
            ["status"] = "published", ["content_hash"] = "sha256:" + new string('a', 64),
            ["published_at"] = "2026-01-01T00:00:00Z", ["tags"] = new JsonArray("review"),
            ["capabilities"] = new JsonArray("api_review"), ["required_capabilities"] = new JsonArray("workspace_read"),
            ["dependencies"] = dependency is null ? new JsonArray() : new JsonArray(new JsonObject { ["package_id"] = dependency, ["version"] = "1.0.0" }),
            ["conflicts_with"] = new JsonArray(),
            ["authoring_source"] = new JsonObject { ["repository"] = "reviewed-source", ["commit"] = commit },
            ["upstream_references"] = new JsonArray()
        };
        const string bundle = """
            {"schema_version":"1.0","when_to_use":"Review API changes.","when_not_to_use":"Unrelated work.",
             "inputs":[],"outputs":[{"name":"findings","description":"Verified findings."}],
             "instructions":"Inspect affected source.","completion_criteria":["Report evidence."],
             "applies_to":"API changes.","requirements":["Read scope."],"violation_handling":"Report unsupported work."}
            """;
        _sourceReader.Sources[(packageId, commit)] = new(manifest.ToJsonString(), bundle, commit);
        return new() { Version = version, AuthoringRepository = "reviewed-source", SourceCommitSha = commit, SourcePath = "packages/review" };
    }

    /// <summary>
    /// Keeps unrelated MCP ports inert while the real catalog reaches a stopped PostgreSQL authority.
    /// </summary>
    public class AvailabilityFixtureProxy : DispatchProxy
    {
        /// <summary>
        /// Supplies only tool enablement, telemetry completion and a nonsecret fixture actor.
        /// </summary>
        /// <param name="targetMethod">The expected unrelated capability method.</param>
        /// <param name="arguments">Unused fixture arguments.</param>
        /// <returns>The inert value required by the MCP dispatch boundary.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? arguments) =>
            targetMethod!.Name switch
            {
                nameof(IToolStateService.IsEnabledAsync) => Task.FromResult(true),
                nameof(IUsageTelemetryService.RecordAsync) => Task.CompletedTask,
                nameof(IAuthService.Get) => Guid.NewGuid(),
                _ => throw new InvalidOperationException("An unrelated fixture port was invoked.")
            };
    }

    private sealed class MetadataEmbedding : IEmbeddingService
    {
        public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken) =>
            Task.FromResult<EmbeddingResultModel?>(null);

        public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken)
        {
            var vector = new float[384];
            vector[text.Contains("BetaUnique", StringComparison.Ordinal) ? 1 : 0] = 1;
            return Task.FromResult<EmbeddingResultModel?>(new(vector, "metadata-fixture"));
        }
    }

    private sealed class PackageUpdateBarrier : DbCommandInterceptor
    {
        public TaskCompletionSource Arrived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("UPDATE catalog.\"Package\"", StringComparison.Ordinal))
            {
                Arrived.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return result;
        }
    }

    private sealed class PublicationInsertBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO catalog.\"Version\"", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _arrived) == 2)
                {
                    _ready.TrySetResult();
                }

                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }

            return result;
        }
    }

    private sealed record FixtureActor(Guid? ActorId) : IAuditActorAccessor;

    private sealed class FixtureSourceReader : IPackageSourceReader
    {
        public Dictionary<(string PackageId, string Commit), PackageSourceModel> Sources { get; } = [];
        public Task<PackageSourceModel> ReadAsync(PackageSourceRequestModel request, CancellationToken cancellationToken) =>
            Task.FromResult(Sources[(request.PackageId, request.SourceCommitSha)]);
    }

    private sealed class UnavailableEmbedding : IEmbeddingService
    {
        public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
        public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
    }
}
