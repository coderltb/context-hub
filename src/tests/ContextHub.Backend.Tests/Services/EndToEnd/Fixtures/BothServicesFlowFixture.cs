namespace ContextHub.Backend.Tests.Services.EndToEnd.Fixtures;

/// <summary>
/// Runs both product hosts against separate disposable databases and shared Redis, replacing only external email and GitHub IO.
/// </summary>
public abstract class BothServicesFlowFixture : IAsyncLifetime
{
    private const string AuthenticationDatabase = "context_hub_flow_auth_design_review";
    private const string HubDatabase = "context_hub_flow_catalog_design_review";

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("pgvector/pgvector:pg17")
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder("redis:7.4-alpine").Build();

    /// <summary>
    /// Gets the real Identity/OpenIddict host.
    /// </summary>
    protected WebApplication Authentication { get; private set; } = null!;

    /// <summary>
    /// Gets the real Context Hub HTTP/MCP host.
    /// </summary>
    protected WebApplication Hub { get; private set; } = null!;

    /// <summary>
    /// Gets the provider-boundary capture; native Identity still creates and checks each code.
    /// </summary>
    protected CapturingEmailService EmailService { get; } = new();

    /// <summary>
    /// Gets deterministic repository content without replacing source allowlists or publication logic.
    /// </summary>
    protected SyntheticGitHubClient GitHubClient { get; } = new();

    /// <summary>
    /// Applies the reviewed SQL and starts native hosts with test-owned credentials and pools.
    /// </summary>
    /// <returns>The completed fixture initialization.</returns>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        await _redis.StartAsync();
        await ApplyDesignSqlAsync();

        Authentication = await StartAuthenticationAsync();
        Hub = await StartHubAsync();
    }

    /// <summary>
    /// Releases hosts and their pools before releasing container endpoints.
    /// </summary>
    /// <returns>The completed fixture cleanup.</returns>
    public async Task DisposeAsync()
    {
        if (Hub is not null)
        {
            await Hub.DisposeAsync();
        }

        if (Authentication is not null)
        {
            await Authentication.DisposeAsync();
        }

        await _redis.DisposeAsync();
        await _database.DisposeAsync();
    }

    /// <summary>
    /// Registers and signs in through Razor and authorization-code PKCE.
    /// </summary>
    /// <param name="name">The synthetic Owner username.</param>
    /// <returns>The Owner ID, password-only cookie and native token response.</returns>
    protected async Task<(Guid Id, string Cookie, JsonElement Credentials)> CreateOwnerAsync(string name)
    {
        using var client = Authentication.GetTestClient();
        var user = await AuthenticationProtocolFixture.RegisterAsync(client, name);
        var cookie = await AuthenticationProtocolFixture.LoginAsync(client, name);
        var grant = await AuthenticationProtocolFixture.AuthorizeAsync(client, cookie, swagger: false);
        var credentials = await AuthenticationProtocolFixture.ExchangeAsync(client, grant.Code, grant.Verifier, swagger: false);

        return (user.GetProperty("id").GetGuid(), cookie, credentials);
    }

    /// <summary>
    /// Dispatches a real Admin request without changing shared client authorization headers.
    /// </summary>
    /// <param name="host">The target product host.</param>
    /// <param name="method">The HTTP operation.</param>
    /// <param name="path">The endpoint path.</param>
    /// <param name="token">The native access token.</param>
    /// <param name="body">The optional input DTO.</param>
    /// <param name="expected">The expected HTTP status.</param>
    /// <returns>The detached REST envelope.</returns>
    protected static async Task<JsonElement> SendJsonAsync(
        WebApplication host,
        HttpMethod method,
        string path,
        string token,
        object? body = null,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await client.SendAsync(request);

        return await AuthenticationProtocolFixture.ReadJsonAsync(response, expected);
    }

    /// <summary>
    /// Creates a real application identity and checks the one-time secret response headers.
    /// </summary>
    /// <param name="ownerToken">The authorized Owner access token.</param>
    /// <param name="tools">The tool grants for the new client.</param>
    /// <returns>The first token and its persisted identity metadata.</returns>
    protected async Task<JsonElement> CreateClientAsync(string ownerToken, params string[] tools)
    {
        using var client = Authentication.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/clients")
        {
            Content = JsonContent.Create(new CreateClientRequestDto("Synthetic flow client") { AllowedTools = tools })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var response = await client.SendAsync(request);
        Assert.True(response.Headers.CacheControl?.NoStore);

        return (await AuthenticationProtocolFixture.ReadJsonAsync(response, HttpStatusCode.Created)).GetProperty("data");
    }

    /// <summary>
    /// Connects the native MCP client through actual HTTP authentication and introspection.
    /// </summary>
    /// <param name="token">The client-issued opaque Bearer token.</param>
    /// <returns>The client owning its transport and HTTP connection.</returns>
    protected async Task<McpClient> ConnectMcpAsync(string token)
    {
        var http = Hub.GetTestClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri("https://hub.test/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: true);

        return await McpClient.CreateAsync(transport, new McpClientOptions
        {
            ProtocolVersion = ContextHub.Api.Constants.HubConstants.PROTOCOL_VERSION
        });
    }

    /// <summary>
    /// Locates checked-in test inputs without depending on the runner's working directory.
    /// </summary>
    /// <param name="segments">The path beneath the workspace root.</param>
    /// <returns>The absolute checked-in artifact path.</returns>
    protected static string WorkspacePath(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "database", "scripts")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return Path.Combine([directory.FullName, .. segments]);
    }

    private async Task ApplyDesignSqlAsync()
    {
        string[] scripts = ["01-create-database.sql", "02-seed-data.sql", "03-catalog-search-v1.sql"];
        foreach (var script in scripts)
        {
            await _database.CopyAsync(File.ReadAllBytes(WorkspacePath("Documents", "database", "scripts", script)), "/tmp/" + script);
        }

        // Each service owns a different database; the fixture applies reviewed DDL instead of EF schema creation.
        foreach (var name in new[] { AuthenticationDatabase, HubDatabase })
        {
            var create = await _database.ExecAsync([
                "psql", "-X", "-U", "postgres", "-d", "postgres", "-v", "review_only_ack=true",
                "-v", "database_name=" + name, "-f", "/tmp/01-create-database.sql"
            ]);
            Assert.True(create.ExitCode == 0, create.Stderr + create.Stdout);
        }

        foreach (var script in scripts.Skip(1))
        {
            var apply = await _database.ExecAsync([
                "psql", "-X", "-U", "postgres", "-d", HubDatabase,
                "-v", "review_only_ack=true", "-f", "/tmp/" + script
            ]);
            Assert.True(apply.ExitCode == 0, apply.Stderr + apply.Stdout);
        }
    }

    private Dictionary<string, string?> RedisConfiguration() => new()
    {
        ["CacheSettings:RedisSettings:Host"] = _redis.Hostname,
        ["CacheSettings:RedisSettings:Port"] = _redis.GetMappedPublicPort(6379).ToString(CultureInfo.InvariantCulture),
        ["CacheSettings:RedisSettings:Password"] = "",
        ["CacheSettings:RedisSettings:Ssl"] = "false",
        ["CacheSettings:RedisSettings:DbNumber"] = "0",
        ["Observability:Logging:MinimumLevel"] = "Fatal"
    };

    private async Task<WebApplication> StartAuthenticationAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientsController).Assembly.GetName().Name,
            ContentRootPath = AuthenticationTestHostPaths.GetContentRoot(),
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        var settings = RedisConfiguration();
        foreach (var item in AuthenticationTestKeys.Create())
        {
            settings[item.Key] = item.Value;
        }
        var connectionString = new NpgsqlConnectionStringBuilder(_database.GetConnectionString())
        {
            Database = AuthenticationDatabase
        }.ConnectionString;
        settings["ConnectionStrings:Authentication"] = connectionString;
        settings["Authentication:Issuer"] = "https://localhost/";
        settings["Authentication:RedirectUris:0"] = "https://localhost/auth/callback";
        settings["Authentication:PostLogoutRedirectUris:0"] = "https://localhost/signed-out";
        settings["Authentication:ClientTokenPepper"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        settings["Authentication:IntrospectionClientSecret"] = new string('x', 32);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "authentication-flow-tests", Environments.Development);
        builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);
        builder.Services.AddPostgreSqlFixture<AuthenticationDbContext>(connectionString);
        builder.Services.Replace(ServiceDescriptor.Singleton<IEmailService>(EmailService));

        var app = builder.Build();
        app.UseAuthenticationApi();
        await app.StartAsync();
        app.GetTestServer().BaseAddress = new Uri("https://localhost");

        return app;
    }

    private async Task<WebApplication> StartHubAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(HubApiRegistration).Assembly.GetName().Name,
            ContentRootPath = WorkspacePath("context-hub", "src", "services", "ContextHub", "ContextHub.Api"),
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        var settings = RedisConfiguration();
        settings["ConnectionStrings:ContextHub"] = new NpgsqlConnectionStringBuilder(_database.GetConnectionString())
        {
            Database = HubDatabase
        }.ConnectionString;
        settings["Authentication:Authority"] = "https://localhost/";
        settings["Authentication:ClientSecret"] = new string('x', 32);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "context-hub-flow-tests", Environments.Development);
        HubApiRegistration.AddContextHubApi(builder.Services, builder.Configuration);
        builder.Services.Replace(ServiceDescriptor.Singleton<IGitHubSourceClient>(GitHubClient));

        // Only the native OAuth backchannel is redirected to the real in-process Authentication host.
        builder.Services.PostConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(http => http.PrimaryHandler = Authentication.GetTestServer().CreateHandler()));

        var app = builder.Build();
        app.UseContextHubApi();
        await app.StartAsync();
        app.GetTestServer().BaseAddress = new Uri("https://hub.test");

        return app;
    }

    /// <summary>
    /// Captures provider requests without sending real mail or replacing native recovery behavior.
    /// </summary>
    protected sealed class CapturingEmailService : IEmailService
    {
        /// <summary>
        /// Gets the provider requests for assertions; their contents are never written to diagnostics.
        /// </summary>
        public List<EmailRequestDto> Messages { get; } = [];

        /// <summary>
        /// Records a synthetic provider acceptance.
        /// </summary>
        /// <param name="request">The native recovery email.</param>
        /// <param name="cancellationToken">Cancels provider dispatch.</param>
        /// <returns>Whether the synthetic provider accepted the request.</returns>
        public Task<bool> SendAsync(EmailRequestDto request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(request);

            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Supplies exact checked-in GitHub content while the real reader checks stored source ownership.
    /// </summary>
    protected sealed class SyntheticGitHubClient : IGitHubSourceClient
    {
        /// <summary>
        /// Gets sources keyed by the exact configured package path.
        /// </summary>
        public Dictionary<string, PackageSourceModel> Sources { get; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Inspects the controlled repository without contacting GitHub.
        /// </summary>
        /// <param name="repositoryUrl">The stored allowlisted source.</param>
        /// <param name="reference">The requested branch.</param>
        /// <param name="cancellationToken">Cancels inspection.</param>
        /// <returns>The deterministic head and number of source files.</returns>
        public Task<GitHubInspectionModel> InspectAsync(string repositoryUrl, string reference, CancellationToken cancellationToken)
        {
            Assert.Equal("https://github.com/context-hub-tests/flow-source", repositoryUrl);
            Assert.Equal("main", reference);

            return Task.FromResult(new GitHubInspectionModel(new string('1', 40), Sources.Count * 2));
        }

        /// <summary>
        /// Returns only the requested exact source revision.
        /// </summary>
        /// <param name="repositoryUrl">The stored allowlisted source.</param>
        /// <param name="commitSha">The requested immutable commit.</param>
        /// <param name="sourcePath">The canonical package path.</param>
        /// <param name="cancellationToken">Cancels source reading.</param>
        /// <returns>The synthetic manifest and bundle.</returns>
        public Task<PackageSourceModel> ReadAsync(string repositoryUrl, string commitSha, string sourcePath, CancellationToken cancellationToken)
        {
            Assert.Equal("https://github.com/context-hub-tests/flow-source", repositoryUrl);
            var source = Sources[sourcePath];
            Assert.Equal(source.CommitSha, commitSha);

            return Task.FromResult(source);
        }
    }
}
