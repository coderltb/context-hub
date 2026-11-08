using ContextHub.Application.Dtos.Mcp;
using ContextHub.Application.Interfaces.Mcp;
using ContextHub.Infrastructure.Services.Mcp;
using ContextHub.Infrastructure.Mappings.Mcp;
using ContextHub.Infrastructure.Repositories.Mcp;
using ContextHub.Core.Services.Serialization;
using ContextHub.Infrastructure.Context.Catalog;
using ContextHub.Application.Constants.Mcp;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol;
using ContextHub.Api.Configurations.Mcp;
using Mapster;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

public sealed partial class TelemetryPostgreSqlTests
{
    /// <summary>
    /// Exercises Owner validation, identifier routes, metadata-only pages and optimistic updates on SQL-created tables.
    /// </summary>
    /// <returns>The completed HTTP and database assertions.</returns>
    [Fact]
    public async Task OwnerPromptConfigurationUsesIdsAndRejectsStaleWrites()
    {
        await using var app = await McpTransportTests.CreateHostAsync(new McpTransportTests.IntrospectionHandler(),
            configureServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton(_dataSource));
                services.Replace(ServiceDescriptor.Scoped<IMcpPromptService, McpPromptService>());
            });
        using var http = app.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        const string route = "/api/admin/v1/mcp-prompts";
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(route)).StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "client");
        Assert.Equal(HttpStatusCode.Forbidden, (await http.GetAsync(route)).StatusCode);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "owner");
        var body = new CreateMcpPromptRequestDto
        {
            Name = "owner_flow", Content = "Use only current capabilities.",
            Arguments = [new() { Name = "intent", Required = true }]
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(route, body with
        {
            Arguments = [new() { Name = "intent" }, new() { Name = "intent" }]
        })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync(route, body with
        {
            Arguments = [null]
        })).StatusCode);
        var created = await http.PostAsJsonAsync(route, body);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var document = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var data = document.RootElement.GetProperty("data");
        var id = data.GetProperty("id").GetGuid();
        var token = data.GetProperty("concurrencyToken").GetString()!;
        var argumentId = data.GetProperty("arguments")[0].GetProperty("argumentId").GetGuid();
        Assert.NotEqual(Guid.Empty, argumentId);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PostAsJsonAsync(route, body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(route + "/" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync(route + "/" + Guid.NewGuid())).StatusCode);
        var update = new UpdateMcpPromptRequestDto
        {
            Content = "Updated guidance.", ConcurrencyToken = token, IsEnabled = false
        };
        Assert.Equal(HttpStatusCode.OK, (await http.PutAsJsonAsync(route + "/" + id, update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await http.PutAsJsonAsync(route + "/" + id, update)).StatusCode);
        using var saved = JsonDocument.Parse(await (await http.GetAsync(route + "/" + id)).Content.ReadAsStringAsync());
        var savedData = saved.RootElement.GetProperty("data");
        Assert.Equal(argumentId, Assert.Single(savedData.GetProperty("arguments").EnumerateArray()).GetProperty("argumentId").GetGuid());
        var removal = update with
        {
            IsEnabled = true,
            RemovedArgumentIds = [argumentId],
            Arguments = [new() { Name = "replacement" }],
            ConcurrencyToken = savedData.GetProperty("concurrencyToken").GetString()!
        };
        using var removed = await http.PutAsJsonAsync(route + "/" + id, removal);
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        using var removedJson = JsonDocument.Parse(await removed.Content.ReadAsStringAsync());
        var definitions = removedJson.RootElement.GetProperty("data").GetProperty("arguments");
        Assert.True(definitions.EnumerateArray().Single(value => value.GetProperty("argumentId").GetGuid() == argumentId).GetProperty("isDeleted").GetBoolean());
        await using var scope = app.Services.CreateAsyncScope();
        var runtime = await scope.ServiceProvider.GetRequiredService<IMcpPromptService>().FindEnabledAsync("owner_flow", CancellationToken.None);
        Assert.Equal("replacement", Assert.Single(runtime!.Arguments).Name);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.GetAsync(route + "?page=2147483647&pageSize=100")).StatusCode);
        using var page = JsonDocument.Parse(await (await http.GetAsync(route + "?sort=updatedAt%20desc")).Content.ReadAsStringAsync());
        Assert.All(page.RootElement.GetProperty("data").GetProperty("items").EnumerateArray(), value => Assert.False(value.TryGetProperty("content", out _)));
    }

    /// <summary>
    /// Demonstrates live native discovery, argument validation and disablement without rebuilding or storing caller values.
    /// </summary>
    /// <returns>The real MCP client and SQL assertions.</returns>
    [Fact]
    public async Task DatabasePromptsDriveNativeDiscoveryAndRendering()
    {
        await using var app = await McpTransportTests.CreateHostAsync(new McpTransportTests.IntrospectionHandler(),
            configureServices: services =>
            {
                services.Replace(ServiceDescriptor.Singleton(_dataSource));
                services.Replace(ServiceDescriptor.Scoped<IMcpPromptService, McpPromptService>());
            });
        using var http = app.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "limited");
        await using var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://hub.test/mcp"), TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport, new McpClientOptions
        {
            ProtocolVersion = ContextHub.Api.Constants.HubConstants.PROTOCOL_VERSION
        });
        var prompts = await client.ListPromptsAsync();
        Assert.Equal(6, prompts.Count);
        Assert.Equal(prompts.Select(value => value.Name).Order(StringComparer.Ordinal), prompts.Select(value => value.Name));
        await Assert.ThrowsAsync<McpProtocolException>(() => client.GetPromptAsync("discover_flow").AsTask());
        foreach (var arguments in new[]
        {
            new Dictionary<string, object?> { ["query"] = " " },
            new Dictionary<string, object?> { ["query"] = 42 },
            new Dictionary<string, object?> { ["query"] = new string('x', 513) },
            new Dictionary<string, object?> { ["query"] = "valid", ["unknown"] = "value" }
        })
        {
            await Assert.ThrowsAsync<McpProtocolException>(() => client.GetPromptAsync("discover_flow", arguments).AsTask());
        }
        var result = await client.GetPromptAsync("discover_flow", new Dictionary<string, object?> { ["query"] = "ephemeral-marker" });
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(result.Messages).Content).Text;
        Assert.Contains("ephemeral-marker", text);
        Assert.Contains("Available capabilities: catalog_search.", text);
        await using var connection = await _dataSource.OpenConnectionAsync();
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM access.\"Prompt\" WHERE \"Content\" LIKE '%ephemeral-marker%' OR \"Arguments\"::text LIKE '%ephemeral-marker%'"));
        await connection.ExecuteAsync("UPDATE access.\"Prompt\" SET \"Content\"='Changed without rebuild' WHERE \"Name\"='help_flow'");
        var changed = await client.GetPromptAsync("help_flow");
        Assert.Contains("Changed without rebuild", Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(Assert.Single(changed.Messages).Content).Text);
        await connection.ExecuteAsync("UPDATE access.\"Prompt\" SET \"IsEnabled\"=false WHERE \"Name\"='help_flow'");
        Assert.DoesNotContain(await client.ListPromptsAsync(), value => value.Name == "help_flow");
        await Assert.ThrowsAsync<McpProtocolException>(() => client.GetPromptAsync("help_flow").AsTask());
        await connection.ExecuteAsync("DROP TABLE access.\"Prompt\"");
        await Assert.ThrowsAsync<McpProtocolException>(() => client.ListPromptsAsync().AsTask());
    }

    /// <summary>
    /// Reapplying reviewed deployment scripts preserves Owner edits and native JSONB/Xmin behavior.
    /// </summary>
    /// <returns>The idempotency and mapped schema assertions.</returns>
    [Fact]
    public async Task PromptSeedAndUpgradePreserveOwnerConfiguration()
    {
        await using var connection = await _dataSource.OpenConnectionAsync();
        await connection.ExecuteAsync("UPDATE access.\"Prompt\" SET \"Content\"='Owner edit' WHERE \"Name\"='help_flow'; UPDATE access.\"Tool\" SET \"Title\"='Owner title' WHERE \"Name\"='catalog_search'");
        var originalArguments = await connection.ExecuteScalarAsync<string>("SELECT \"Arguments\"::text FROM access.\"Prompt\" WHERE \"Name\"='discover_flow'");
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (!Directory.Exists(Path.Combine(directory!.FullName, "Documents"))) directory = directory.Parent;
        foreach (var name in new[] { "06-mcp-configuration-upgrade.sql", "02-seed-data.sql" })
        {
            await _container.CopyAsync(File.ReadAllBytes(Path.Combine(directory.FullName, "Documents/database/scripts", name)), "/tmp/" + name);
            var apply = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "context_hub_telemetry_design_review", "-v", "review_only_ack=true", "-f", "/tmp/" + name]);
            Assert.True(apply.ExitCode == 0, apply.Stderr + apply.Stdout);
        }
        Assert.Equal("Owner edit", await connection.ExecuteScalarAsync<string>("SELECT \"Content\" FROM access.\"Prompt\" WHERE \"Name\"='help_flow'"));
        Assert.Equal("Owner title", await connection.ExecuteScalarAsync<string>("SELECT \"Title\" FROM access.\"Tool\" WHERE \"Name\"='catalog_search'"));
        Assert.Equal(6L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM access.\"Prompt\""));
        Assert.Equal(originalArguments, await connection.ExecuteScalarAsync<string>("SELECT \"Arguments\"::text FROM access.\"Prompt\" WHERE \"Name\"='discover_flow'"));
        // Simulate the previous deployment shape after proving repeatable seeds preserve edits.
        await connection.ExecuteAsync("ALTER TABLE access.\"Tool\" DROP COLUMN \"Title\", DROP COLUMN \"Description\"; DROP TABLE access.\"Prompt\"");
        var upgrade = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "context_hub_telemetry_design_review", "-v", "review_only_ack=true", "-f", "/tmp/06-mcp-configuration-upgrade.sql"]);
        Assert.True(upgrade.ExitCode == 0, upgrade.Stderr + upgrade.Stdout);
        Assert.Equal("Catalog Search", await connection.ExecuteScalarAsync<string>("SELECT \"Title\" FROM access.\"Tool\" WHERE \"Name\"='catalog_search'"));
        var rejected = await _container.ExecAsync(["psql", "-X", "-U", "postgres", "-d", "context_hub_telemetry_design_review", "-v", "review_only_ack=false", "-f", "/tmp/06-mcp-configuration-upgrade.sql"]);
        Assert.NotEqual(0, rejected.ExitCode);

        await using var context = CreateContext();
        Assert.Equal(6, await context.Prompts.AsNoTracking().CountAsync());
        Assert.True((await context.Prompts.AsNoTracking().FirstAsync()).ConcurrencyVersion > 0);
        Assert.Equal("jsonb", await connection.ExecuteScalarAsync<string>("SELECT data_type FROM information_schema.columns WHERE table_schema='access' AND table_name='Prompt' AND column_name='Arguments'"));
        Assert.Equal(0L, await connection.ExecuteScalarAsync<long>("SELECT count(*) FROM information_schema.columns WHERE table_schema='access' AND table_name='Prompt' AND column_name IN ('SortOrder','xmin')"));
    }

    /// <summary>
    /// Forces both updates past the initial version check so the database decides the winner.
    /// </summary>
    /// <returns>One committed update and one safe conflict.</returns>
    [Fact]
    public async Task ConcurrentPromptUpdatesTranslateOnlyTheNativeRowConflict()
    {
        await using var initial = CreateContext();
        var entity = await initial.Prompts.AsNoTracking().SingleAsync(value => value.Name == "help_flow");
        var token = Convert.ToBase64String(BitConverter.GetBytes(entity.ConcurrencyVersion));
        var gate = new PromptSaveGate();
        var options = new DbContextOptionsBuilder<ContextHubDbContext>()
            .UseNpgsql(_dataSource, postgres => postgres.UseVector()).AddInterceptors(gate).Options;
        await using var first = new ContextHubDbContext(options);
        await using var second = new ContextHubDbContext(options);
        McpPromptService CreateService(ContextHubDbContext context) => new(
            new McpPromptRepository(context, NullLogger<McpPromptRepository>.Instance),
            new UnitOfWork<ContextHubDbContext>(context, NullLogger<UnitOfWork<ContextHubDbContext>>.Instance),
            new JsonSerializerService(), NullLogger<McpPromptService>.Instance);
        var failures = await Task.WhenAll(new[] { first, second }.Select((context, index) =>
            Record.ExceptionAsync(() => CreateService(context).UpdateAsync(entity.Id,
                new UpdateMcpPromptRequestDto { Content = "Writer " + index, ConcurrencyToken = token }, CancellationToken.None))));
        Assert.Single(failures, failure => failure is null);
        var conflict = Assert.IsType<BusinessException>(Assert.Single(failures, failure => failure is not null));
        Assert.Equal(ContextHub.Shared.Enums.BusinessErrorKind.Conflict, conflict.Kind);
    }

    /// <summary>
    /// Synchronizes only the first save in each participating context.
    /// </summary>
    private sealed class PromptSaveGate : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrived;

        /// <summary>
        /// Releases both writes only after each service has checked the original xmin.
        /// </summary>
        /// <param name="eventData">The native context event.</param>
        /// <param name="result">The unchanged interception result.</param>
        /// <param name="cancellationToken">Cancels the bounded rendezvous.</param>
        /// <returns>The unchanged native save result.</returns>
        public override async ValueTask<Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int>> SavingChangesAsync(
            Microsoft.EntityFrameworkCore.Diagnostics.DbContextEventData eventData,
            Microsoft.EntityFrameworkCore.Diagnostics.InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _arrived) == 2) _ready.TrySetResult();
            await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            return result;
        }
    }

}
