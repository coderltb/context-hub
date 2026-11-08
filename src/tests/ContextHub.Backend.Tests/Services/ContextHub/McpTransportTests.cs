using System.Reflection;
using System.Threading.RateLimiting;
using ContextHub.Api.Constants;
using ContextHub.Api.DependencyInjection;
using ContextHub.Api.Extensions;
using ContextHub.Application.Interfaces.Integration;
using ContextHub.Application.Constants.Telemetry;
using ContextHub.Application.Constants.Mcp;
using ContextHub.Application.Dtos.Telemetry;
using ContextHub.Application.Dtos.Mcp;
using ContextHub.Application.Interfaces.Telemetry;
using ContextHub.Application.Interfaces.Mcp;
using ContextHub.Application.Models.Telemetry;
using ContextHub.Application.Models.Mcp;
using ContextHub.Infrastructure.Services.Integration;
using Microsoft.Extensions.Http;
using ModelContextProtocol.Client;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises native MCP and introspection middleware; fake business ports deliberately do not claim database coverage.
/// </summary>
[Collection("GlobalMappingCollection")]
public sealed class McpTransportTests
{
    /// <summary>
    /// Rejects exhausted ingress before introspection while keeping anonymous probes and neighboring paths independent.
    /// </summary>
    /// <returns>The throttling, retry-header and anonymous-boundary assertions.</returns>
    [Fact]
    public async Task ExhaustedIngressSkipsIntrospectionAndLeavesHealthAnonymous()
    {
        var backchannel = new IntrospectionHandler();
        await using var app = await CreateHostAsync(backchannel);
        var ingress = app.Services.GetRequiredKeyedService<PartitionedRateLimiter<string>>(HubConstants.INGRESS_LIMITER);
        using var exhausted = ingress.AttemptAcquire("unknown", 1000);
        Assert.True(exhausted.IsAcquired);

        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");

        using var admin = await client.GetAsync("/api/admin/v1/packages");
        using var mcp = await SendAsync(client, "/mcp", "tools/list");
        Assert.Equal(HttpStatusCode.TooManyRequests, admin.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), admin.Headers.RetryAfter?.Delta);
        Assert.Equal(HttpStatusCode.TooManyRequests, mcp.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), mcp.Headers.RetryAfter?.Delta);

        using var health = await client.GetAsync("/health/live");
        using var neighboring = await client.GetAsync("/api/admin/v10/packages");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, neighboring.StatusCode);
        Assert.Equal(0, backchannel.Introspections);
    }

    /// <summary>
    /// Applies the client budget only after authentication and keeps it independent of Owner and health endpoints.
    /// </summary>
    /// <returns>The identity, retry-header and endpoint-boundary assertions.</returns>
    [Fact]
    public async Task ExhaustedClientBudgetPreservesAuthenticationAndAdminBoundaries()
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        var limiter = app.Services.GetRequiredKeyedService<PartitionedRateLimiter<string>>(HubConstants.CLIENT_LIMITER);
        using var exhausted = limiter.AttemptAcquire("129396d2-20e7-4fb5-a428-eaef8158059c", 1000);
        Assert.True(exhausted.IsAcquired);

        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        using var rejected = await SendAsync(client, "/mcp", "tools/list");
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        using var throttled = await SendAsync(client, "/mcp", "tools/list");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.Equal(TimeSpan.FromSeconds(60), throttled.Headers.RetryAfter?.Delta);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        using var admin = await client.GetAsync("/api/admin/v1/packages");
        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, admin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    /// <summary>
    /// Keeps the two keyed limiters independent while enforcing each identity's configured allowance.
    /// </summary>
    /// <returns>The configured permit, partition and singleton-lifetime assertions.</returns>
    [Fact]
    public async Task IngressAndClientLimitsKeepIndependentPartitions()
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        var ingress = app.Services.GetRequiredKeyedService<PartitionedRateLimiter<string>>(HubConstants.INGRESS_LIMITER);
        var client = app.Services.GetRequiredKeyedService<PartitionedRateLimiter<string>>(HubConstants.CLIENT_LIMITER);

        using var ingressAllowance = ingress.AttemptAcquire("same-identity", 1000);
        using var ingressExhausted = ingress.AttemptAcquire("same-identity");
        using var separateAddress = ingress.AttemptAcquire("other-identity", 1000);
        using var clientAllowance = client.AttemptAcquire("same-identity", 1000);
        using var clientExhausted = client.AttemptAcquire("same-identity");

        Assert.True(ingressAllowance.IsAcquired);
        Assert.False(ingressExhausted.IsAcquired);
        Assert.True(separateAddress.IsAcquired);
        Assert.True(clientAllowance.IsAcquired);
        Assert.False(clientExhausted.IsAcquired);
        Assert.Same(ingress, app.Services.GetRequiredKeyedService<PartitionedRateLimiter<string>>(HubConstants.INGRESS_LIMITER));
        Assert.NotSame(ingress, client);
    }

    /// <summary>
    /// Health never introspects irrelevant credentials, while protected APIs distinguish authentication and permission.
    /// </summary>
    /// <returns>The native boundary assertions.</returns>
    [Fact]
    public async Task HealthIsAnonymousAndAdminRequiresOwnerToken()
    {
        var backchannel = new IntrospectionHandler();
        await using var app = await CreateHostAsync(backchannel);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");

        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(0, backchannel.Introspections);

        using var rejected = await client.GetAsync("/api/admin/v1/packages");
        Assert.True(rejected.StatusCode == HttpStatusCode.Unauthorized, (backchannel.LastFailure?.ToString() ?? "") + rejected.ToString() + await rejected.Content.ReadAsStringAsync() + " BackchannelRequests=" + backchannel.Requests);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        using var forbidden = await client.GetAsync("/api/admin/v1/packages");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        using var permitted = await client.GetAsync("/api/admin/v1/packages");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, permitted.StatusCode);
        Assert.Contains("error_dependency_unavailable", await permitted.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Every protected request revalidates its credential and known backchannel outages fail closed.
    /// </summary>
    /// <returns>The uncached revocation and failure assertions.</returns>
    [Fact]
    public async Task IntrospectionIsNotCachedAndOutageDoesNotPermitRequests()
    {
        var backchannel = new IntrospectionHandler();
        await using var app = await CreateHostAsync(backchannel);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        using var first = await SendAsync(client, "/mcp", "tools/list");
        Assert.True(first.StatusCode == HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        backchannel.Active = false;
        using var revoked = await SendAsync(client, "/mcp", "tools/list");
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        Assert.Equal(2, backchannel.Introspections);

        backchannel.Unavailable = true;
        using var unavailable = await SendAsync(client, "/mcp", "tools/list");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, unavailable.StatusCode);
    }

    /// <summary>
    /// Real provider refusal selects safe REST and MCP dependency failures through the actual catalog adapters.
    /// </summary>
    /// <returns>The HTTP, schema and provider-detail privacy assertions.</returns>
    [Fact]
    public async Task NativePostgreSqlOutageHasDeclaredRestAndToolFailures()
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler(), realCatalog: true);
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var unavailable = await Record.ExceptionAsync(async () => await scope.ServiceProvider.GetRequiredService<ICatalogService>()
                .GetPackagesAsync(new PackageListRequestDto(), CancellationToken.None));
            Assert.True(unavailable is ContextHub.Core.Exceptions.DependencyUnavailableException, unavailable?.ToString());
        }
        using var http = app.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        using var response = await http.GetAsync("/api/admin/v1/packages");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("error_dependency_unavailable", body);
        Assert.DoesNotContain("Npgsql", body);
        Assert.DoesNotContain("127.0.0.1", body);

        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://hub.test/mcp"), TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = HubConstants.PROTOCOL_VERSION });
        var result = await client.CallToolAsync("catalog_search", new Dictionary<string, object?> { ["query"] = "public reference" });
        Assert.True(result.IsError);
        Assert.Equal("dependency_unavailable", result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.True(McpContractFixture.CreateRegistry().IsValidOutput("catalog_search", JsonNode.Parse(result.StructuredContent.Value.GetRawText())!));
    }

    /// <summary>
    /// A native 2026 client discovers the explicit surface and receives a schema-valid tool failure without duplicating JSON text.
    /// </summary>
    /// <returns>The genuine SDK transport assertions.</returns>
    [Fact]
    public async Task NativeClientDiscoversAndCallsTheExplicitSurface()
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        using var http = app.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://hub.test/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport, new McpClientOptions { ProtocolVersion = HubConstants.PROTOCOL_VERSION });
        var tools = await client.ListToolsAsync();
        Assert.Equal(ToolCatalogConstants.TOOLS.Select(tool => tool.Name), tools.Select(tool => tool.Name));
        Assert.Equal(6, (await client.ListPromptsAsync()).Count);
        Assert.Equal(2, (await client.ListResourcesAsync()).Count);
        Assert.Equal(3, (await client.ListResourceTemplatesAsync()).Count);

        var result = await client.CallToolAsync("catalog_search", new Dictionary<string, object?> { ["query"] = "public test" });
        Assert.True(result.IsError);
        Assert.Equal("dependency_unavailable", result.StructuredContent!.Value.GetProperty("error").GetProperty("code").GetString());
        Assert.Single(result.Content);
    }

    /// <summary>
    /// Route aliases cannot bypass transport limits, and missing grants are denied before argument validation.
    /// </summary>
    /// <param name="path">A routed spelling of the same MCP endpoint.</param>
    /// <returns>The protocol and authorization assertions.</returns>
    [Theory]
    [InlineData("/mcp")]
    [InlineData("/mcp/")]
    [InlineData("/MCP")]
    public async Task AliasesKeepVersionSizeOriginAndToolGrants(string path)
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "limited");

        using var version = await SendAsync(client, path, "tools/list", version: "2025-11-25");
        Assert.Equal(HttpStatusCode.BadRequest, version.StatusCode);
        using var versionError = JsonDocument.Parse(await version.Content.ReadAsStringAsync());
        Assert.Equal("2.0", versionError.RootElement.GetProperty("jsonrpc").GetString());
        Assert.Equal(JsonValueKind.Null, versionError.RootElement.GetProperty("id").ValueKind);
        Assert.Equal(-32022, versionError.RootElement.GetProperty("error").GetProperty("code").GetInt32());
        Assert.Equal("Unsupported protocol version.", versionError.RootElement.GetProperty("error").GetProperty("message").GetString());

        using var oversized = await SendAsync(client, path, "tools/list", padding: new string('x', 65_536));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversized.StatusCode);
        using var oversizedError = JsonDocument.Parse(await oversized.Content.ReadAsStringAsync());
        Assert.Equal(-32600, oversizedError.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        using var origin = await SendAsync(client, path, "tools/list", origin: "https://untrusted.test");
        Assert.Equal(HttpStatusCode.Forbidden, origin.StatusCode);
        using var originError = JsonDocument.Parse(await origin.Content.ReadAsStringAsync());
        Assert.Equal(-32600, originError.RootElement.GetProperty("error").GetProperty("code").GetInt32());

        using var forbidden = await SendAsync(client, path, "tools/call", "managed_tool_call");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    /// <summary>
    /// Managed input failures retain their declared protocol or tool error instead of becoming implementation failures.
    /// </summary>
    /// <returns>The strict input/output schema assertions.</returns>
    [Fact]
    public async Task ManagedInputErrorsUseTheirDeclaredContracts()
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        using var response = await SendAsync(client, "/mcp", "tools/call", "managed_tool_call");
        var json = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, json);
        Assert.Contains("managed_tool_input_invalid", json);
        Assert.DoesNotContain("internal_error", json);

        using var describe = await SendAsync(client, "/mcp", "tools/call", "managed_tool_describe");
        Assert.Contains("-32602", await describe.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Native JSON binding rejects missing update state and unknown publication input before feature I/O.
    /// </summary>
    /// <param name="method">The documented mutation method.</param>
    /// <param name="path">The concrete resource route.</param>
    /// <param name="body">The deliberately incomplete or extended JSON object.</param>
    /// <returns>The safe validation response.</returns>
    [Theory]
    [InlineData("PUT", "/api/admin/v1/packages/contexthub.review", "{\"displayName\":\"Review\",\"summary\":\"Guidance\",\"concurrencyToken\":\"1\"}")]
    [InlineData("PUT", "/api/admin/v1/repositories/129396d2-20e7-4fb5-a428-eaef8158059c", "{\"repositoryUrl\":\"https://github.com/dotnet/aspnetcore\",\"defaultRef\":\"main\",\"concurrencyToken\":\"1\"}")]
    [InlineData("PUT", "/api/admin/v1/mcp-tools/11111111-1111-1111-1111-111111111111", "{\"concurrencyToken\":\"1\"}")]
    [InlineData("POST", "/api/admin/v1/packages/11111111-1111-1111-1111-111111111111/versions/preview", "{\"version\":\"1.0.0\",\"authoringRepository\":\"main\",\"sourceCommitSha\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"sourcePath\":\"rules\",\"manifest\":{}}")]
    public async Task BodyContractFailuresAreRejectedBeforeUseCases(string method, string path, string body)
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error_validation", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Invalid identifiers are classified by MVC binding instead of being hidden as routing misses.
    /// </summary>
    /// <param name="path">The Guid-bound detail route.</param>
    /// <returns>The native binding response.</returns>
    [Theory]
    [InlineData("/api/admin/v1/repositories/not-a-guid")]
    [InlineData("/api/admin/v1/recommendations/not-a-guid")]
    public async Task InvalidGuidUsesNativeValidation(string path)
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Publication previews are not cacheable and newly created package locations resolve through native routes.
    /// </summary>
    /// <returns>The declared HTTP metadata.</returns>
    [Fact]
    public async Task PreviewIsNoStoreAndCreatedPackageHasLocation()
    {
        await using var app = await CreateHostAsync(new IntrospectionHandler());
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        using var preview = await client.PostAsJsonAsync("/api/admin/v1/packages/11111111-1111-1111-1111-111111111111/versions/preview",
            new PublishVersionRequestDto { Version = "1.0.0", AuthoringRepository = "main", SourceCommitSha = new string('a', 40), SourcePath = "rules" });
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        Assert.True(preview.Headers.CacheControl?.NoStore);

        using var created = await client.PostAsJsonAsync("/api/admin/v1/packages",
            new CreatePackageRequestDto { Key = "contexthub.review", PackageType = "workflow", DisplayName = "Review", Summary = "Public review guidance." });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("/api/admin/v1/packages/11111111-1111-1111-1111-111111111111", created.Headers.Location!.AbsolutePath);
    }

    /// <summary>
    /// Development documentation exposes exactly the Admin operations with native descriptions, field schemas and alternative security.
    /// </summary>
    /// <returns>The actual OpenAPI and anonymous document-generation assertions.</returns>
    [Fact]
    public async Task OpenApiDescribesTheExactAdminSurfaceFromDtoDocumentation()
    {
        var backchannel = new IntrospectionHandler();
        await using var app = await CreateHostAsync(backchannel, environment: Environments.Development);
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://hub.test");
        using var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");
        var count = paths.EnumerateObject().Where(path => path.Name.StartsWith("/api/admin/v1", StringComparison.Ordinal))
            .Sum(path => path.Value.EnumerateObject().Count(operation => operation.Name is "get" or "post" or "put" or "delete"));
        Assert.Equal(33, count);
        var package = paths.GetProperty("/api/admin/v1/packages").GetProperty("post");
        Assert.Contains("package", package.GetProperty("summary").GetString()!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, package.GetProperty("security").GetArrayLength());
        foreach (var status in new[] { "201", "400", "401", "403", "409", "500" })
        {
            Assert.True(package.GetProperty("responses").TryGetProperty(status, out _), "Missing documented status " + status);
        }
        Assert.Contains("server-allowlisted authoring", json);
        Assert.Contains("isActive", json);
        Assert.Equal(0, backchannel.Introspections);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string path, string method, string? name = null,
        string version = HubConstants.PROTOCOL_VERSION, string? origin = null, string? padding = null)
    {
        var parameters = new Dictionary<string, object?> { ["_meta"] = new Dictionary<string, object?> { ["io.modelcontextprotocol/protocolVersion"] = version, ["io.modelcontextprotocol/clientCapabilities"] = new Dictionary<string, object?>() } };
        if (name is not null)
        {
            parameters["name"] = name;
            parameters["arguments"] = new Dictionary<string, object?>();
        }
        if (padding is not null)
        {
            parameters["padding"] = padding;
        }
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters })
        };
        request.Headers.Add("MCP-Protocol-Version", version);
        request.Headers.Add("Mcp-Method", method);
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (name is not null)
        {
            request.Headers.Add("Mcp-Name", name);
        }
        if (origin is not null)
        {
            request.Headers.Add("Origin", origin);
        }
        return client.SendAsync(request);
    }

    internal static async Task<WebApplication> CreateHostAsync(
        HttpMessageHandler backchannel,
        string authority = "https://authority.test/",
        string clientSecret = "test-backchannel-only",
        string environment = "Production",
        bool realCatalog = false,
        Action<IServiceCollection>? configureServices = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ContextHub.Api.DependencyInjection.ApiServiceRegistration).Assembly.GetName().Name,
            EnvironmentName = environment
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ContextHub"] = "Host=127.0.0.1;Port=1;Database=unused_transport_test;Username=unused;Timeout=1;Pooling=false",
            ["CacheSettings:RedisSettings:Host"] = "127.0.0.1",
            ["CacheSettings:RedisSettings:Port"] = "1",
            ["CacheSettings:RedisSettings:AbortOnConnectFail"] = "false",
            ["CacheSettings:RedisSettings:ConnectTimeout"] = "200",
            ["CacheSettings:RedisSettings:ConnectRetry"] = "0",
            ["Authentication:Authority"] = authority,
            ["Authentication:ClientSecret"] = clientSecret,
            ["Observability:Logging:MinimumLevel"] = "Fatal",
            ["RateLimits:IpRequestsPerMinute"] = "1000",
            ["RateLimits:ClientRequestsPerMinute"] = "1000"
        });
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "hub-transport-tests", Environments.Production);
        builder.Services.AddContextHubApi(builder.Configuration);
        foreach (var descriptor in builder.Services.Where(descriptor => descriptor.ImplementationType == typeof(ExternalMetadataRefreshService)).ToArray())
        {
            builder.Services.Remove(descriptor);
        }
        builder.Services.Replace(ServiceDescriptor.Scoped<IToolStateService, ToolStateFixture>());
        builder.Services.Replace(ServiceDescriptor.Scoped<ContextHub.Application.Interfaces.Mcp.IMcpPromptService, PromptFixture>());
        builder.Services.Replace(ServiceDescriptor.Scoped<IUsageTelemetryService, UsageFixture>());
        if (!realCatalog)
        {
            builder.Services.Replace(ServiceDescriptor.Scoped<ICatalogService>(_ => DispatchProxy.Create<ICatalogService, UnavailablePortProxy>()));
        }
        builder.Services.Replace(ServiceDescriptor.Scoped<IManagedToolService>(_ => DispatchProxy.Create<IManagedToolService, UnavailablePortProxy>()));
        builder.Services.Replace(ServiceDescriptor.Scoped<IQualitySignalService>(_ => DispatchProxy.Create<IQualitySignalService, UnavailablePortProxy>()));
        builder.Services.PostConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(handler => handler.PrimaryHandler = backchannel));
        configureServices?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseContextHubApi();
        await app.StartAsync();
        app.GetTestServer().BaseAddress = new Uri("https://hub.test");
        return app;
    }

    /// <summary>
    /// Substitutes only feature I/O; native authentication, policies, dispatch and protocol handlers remain real.
    /// </summary>
    public class UnavailablePortProxy : DispatchProxy
    {
        /// <summary>
        /// Fails a selected feature port without substituting native middleware or authorization.
        /// </summary>
        /// <param name="targetMethod">The requested asynchronous port operation.</param>
        /// <param name="args">The unused approved inputs.</param>
        /// <returns>The correctly typed failed task.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.DeclaringType == typeof(ICatalogService))
            {
                if (targetMethod.Name == nameof(ICatalogService.PreviewAsync))
                {
                    return Task.FromResult(new PackagePreviewResponseDto { Readiness = new PackagePreviewReadinessResponseDto { Status = "blocked" } });
                }
                if (targetMethod.Name == nameof(ICatalogService.CreatePackageAsync))
                {
                    return Task.FromResult(new PackageResponseDto { PackageId = Guid.Parse("11111111-1111-1111-1111-111111111111"), Key = "contexthub.review" });
                }
            }

            var resultType = targetMethod.ReturnType.GetGenericArguments().Single();
            return typeof(Task).GetMethods().Single(method => method.Name == nameof(Task.FromException) && method.IsGenericMethod)
                .MakeGenericMethod(resultType).Invoke(null, [new ContextHub.Core.Exceptions.DependencyUnavailableException("test_provider_unavailable")]);
        }
    }

    private sealed class PromptFixture : ContextHub.Application.Interfaces.Mcp.IMcpPromptService
    {
        public Task<IReadOnlyList<ContextHub.Application.Models.Mcp.McpPromptModel>> GetEnabledAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ContextHub.Application.Models.Mcp.McpPromptModel>>(
                new[] { "check_flow", "discover_flow", "help_flow", "refresh_flow", "status_flow", "use_flow" }
                    .Select(name => new ContextHub.Application.Models.Mcp.McpPromptModel { Name = name }).ToArray());
        public Task<ContextHub.Application.Models.Mcp.McpPromptModel?> FindEnabledAsync(string name, CancellationToken token) => throw new NotSupportedException();
        public Task<PageResponse<ContextHub.Application.Dtos.Mcp.McpPromptSummaryResponseDto>> GetPageAsync(ContextHub.Application.Dtos.Mcp.GetMcpPromptsRequestDto request, CancellationToken token) => throw new NotSupportedException();
        public Task<ContextHub.Application.Dtos.Mcp.McpPromptResponseDto> GetAsync(Guid id, CancellationToken token) => throw new NotSupportedException();
        public Task<ContextHub.Application.Dtos.Mcp.McpPromptResponseDto> CreateAsync(ContextHub.Application.Dtos.Mcp.CreateMcpPromptRequestDto request, CancellationToken token) => throw new NotSupportedException();
        public Task<ContextHub.Application.Dtos.Mcp.McpPromptResponseDto> UpdateAsync(Guid id, ContextHub.Application.Dtos.Mcp.UpdateMcpPromptRequestDto request, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class ToolStateFixture : IToolStateService
    {
        public Task<McpToolDetailResponseDto> GetAsync(Guid toolId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ContextHub.Application.Models.Mcp.McpToolContractModel>> GetContractsAsync(string? name, CancellationToken token) =>
            Task.FromResult<IReadOnlyList<ContextHub.Application.Models.Mcp.McpToolContractModel>>(
                McpContractFixture.ReadContracts().Where(contract => name == null || contract.Name == name).ToArray());
        public Task<IReadOnlyList<string>> GetEnabledNamesAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<string>>(ToolCatalogConstants.TOOLS.Select(tool => tool.Name).ToArray());
        public Task<bool> IsEnabledAsync(string toolName, CancellationToken token) => Task.FromResult(true);
        public Task<PageResponse<McpToolResponseDto>> GetListAsync(GetMcpToolsRequestDto request, CancellationToken token) => throw new NotSupportedException();
        public Task<McpToolResponseDto> UpdateAsync(Guid id, UpdateMcpToolRequestDto request, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class UsageFixture : IUsageTelemetryService
    {
        public Task RecordAsync(ToolOutcomeModel outcome, CancellationToken token) => Task.CompletedTask;
    }

    internal sealed class IntrospectionHandler : HttpMessageHandler
    {
        private int _introspections;
        public Exception? LastFailure { get; set; }
        public int Requests { get; set; }
        public int Introspections => _introspections;
        public bool Active { get; set; } = true;
        public bool Unavailable { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Requests++;
            if (request.RequestUri!.AbsolutePath.Contains("well-known", StringComparison.Ordinal))
            {
                return Json(new
                {
                    issuer = "https://authority.test/", introspection_endpoint = "https://authority.test/connect/introspect",
                    jwks_uri = "https://authority.test/jwks",
                    introspection_endpoint_auth_methods_supported = new[] { "client_secret_basic", "client_secret_post" }
                });
            }
            if (request.RequestUri.AbsolutePath == "/jwks")
            {
                using var rsa = RSA.Create(2048);
                var key = rsa.ExportParameters(false);
                return Json(new { keys = new[] { new { kty = "RSA", use = "sig", kid = "test-signing-key", alg = "RS256",
                    e = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(key.Exponent!),
                    n = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(key.Modulus!) } } });
            }
            Interlocked.Increment(ref _introspections);
            if (Unavailable)
            {
                throw new HttpRequestException("Test backchannel unavailable.");
            }
            var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(token));
            var value = form["token"].ToString();
            if (!Active || value == "invalid")
            {
                return Json(new { active = false });
            }
            var owner = value == "owner";
            return Json(new
            {
                active = true, sub = "129396d2-20e7-4fb5-a428-eaef8158059c", aud = "context-hub-api",
                exp = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds(), token_usage = "access_token",
                actor_type = owner ? "user" : "client", client_id = "129396d2-20e7-4fb5-a428-eaef8158059c",
                role = owner ? new[] { "Owner" } : Array.Empty<string>(),
                scope = owner ? "contexthub.admin" : "contexthub.mcp",
                tool = value == "limited" ? new[] { "catalog_search" } : ToolCatalogConstants.TOOLS.Select(tool => tool.Name).ToArray()
            });
        }

        private static HttpResponseMessage Json<T>(T payload) => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
    }
}
