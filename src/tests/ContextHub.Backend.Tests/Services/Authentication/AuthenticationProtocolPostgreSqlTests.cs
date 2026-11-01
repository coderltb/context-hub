namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises native SSO and credential protocols over HTTP against disposable PostgreSQL.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class AuthenticationProtocolPostgreSqlTests : IAsyncLifetime
{
    /// <summary>
    /// Owns only the disposable protocol-test database.
    /// </summary>
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("authentication_protocol_tests").WithUsername("postgres").WithPassword("postgres").Build();

    /// <summary>
    /// Advances native token validation without sleeping through refresh reuse windows.
    /// </summary>
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.UtcNow);

    /// <summary>
    /// Holds the native host and managers owned by this test.
    /// </summary>
    private WebApplication _app = null!;

    /// <summary>
    /// Creates schema only in the fresh Testcontainer and starts the actual Authentication pipeline.
    /// </summary>
    /// <returns>The isolated host initialization.</returns>
    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientsController).Assembly.GetName().Name,
            ContentRootPath = AuthenticationTestHostPaths.GetContentRoot(),
            EnvironmentName = Environments.Development
        });
        builder.WebHost.UseTestServer();
        using var signingKey = RSA.Create(2048);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = _database.GetConnectionString(),
            ["Authentication:Issuer"] = "https://localhost/",
            ["Authentication:SigningKey"] = signingKey.ExportPkcs8PrivateKeyPem(),
            ["Authentication:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Authentication:RedirectUris:0"] = "https://localhost/auth/callback",
            ["Authentication:PostLogoutRedirectUris:0"] = "https://localhost/signed-out",
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32),
            ["Observability:Logging:MinimumLevel"] = "Fatal"
        });
        builder.Services.AddLoggingInfrastructure(builder.Configuration, "authentication-protocol-tests", Environments.Development);
        builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);
        builder.Services.AddPostgreSqlFixture<AuthenticationDbContext>(_database.GetConnectionString());
        builder.Services.Configure<OpenIddictServerOptions>(options => options.TimeProvider = _clock);
        builder.Services.Configure<OpenIddictValidationOptions>(options => options.TimeProvider = _clock);
        _app = builder.Build();
        using (var scope = _app.Services.CreateScope())
        {
            // Testcontainers already created the database; the product host remains DB-first.
            await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>()
                .GetService<IRelationalDatabaseCreator>().CreateTablesAsync();
        }

        _app.UseAuthenticationApi();
        await _app.StartAsync();
        _app.GetTestServer().BaseAddress = new Uri("https://localhost");
    }

    /// <summary>
    /// Context Hub validates real Owner and opaque client credentials through the running Authentication protocol.
    /// </summary>
    /// <returns>The separate-host actor, tool-grant and immediate revocation assertions.</returns>
    [Fact]
    public async Task ContextHubUsesNativeIntrospectionAndRechecksRevokedClient()
    {
        using var authentication = _app.GetTestClient();
        await RegisterAsync(authentication, "hub-owner");
        var cookie = await LoginAsync(authentication, "hub-owner");
        var grant = await AuthorizeAsync(authentication, cookie, swagger: true);
        var credentials = await ExchangeAsync(authentication, grant.Code, grant.Verifier, swagger: true);
        var ownerToken = credentials.GetProperty("access_token").GetString()!;
        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/clients")
        {
            Content = JsonContent.Create(new CreateClientRequestDto("hub-client") { AllowedTools = ["catalog_search"] })
        };
        create.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var issued = await authentication.SendAsync(create);
        var data = (await ReadJsonAsync(issued, HttpStatusCode.Created)).GetProperty("data");
        var clientToken = data.GetProperty("token").GetString()!;
        var clientId = data.GetProperty("clientId").GetGuid();
        var tokenId = data.GetProperty("tokenId").GetGuid();

        await using var hub = await ContextHubV1.McpTransportTests.CreateHostAsync(_app.GetTestServer().CreateHandler(), "https://localhost/", new string('x', 32));
        using var http = hub.GetTestClient();
        http.BaseAddress = new Uri("https://hub.test");
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clientToken);
        await using var transport = new ModelContextProtocol.Client.HttpClientTransport(new ModelContextProtocol.Client.HttpClientTransportOptions
        {
            Endpoint = new Uri("https://hub.test/mcp"),
            TransportMode = ModelContextProtocol.Client.HttpTransportMode.StreamableHttp
        }, http, ownsHttpClient: false);
        await using var mcp = await ModelContextProtocol.Client.McpClient.CreateAsync(transport,
            new ModelContextProtocol.Client.McpClientOptions { ProtocolVersion = ContextHub.Api.Constants.HubConstants.PROTOCOL_VERSION });
        Assert.Equal("catalog_search", Assert.Single(await mcp.ListToolsAsync()).Name);

        using var clientAdmin = await http.GetAsync("/api/admin/v1/packages");
        Assert.Equal(HttpStatusCode.Forbidden, clientAdmin.StatusCode);
        using var ownerAdmin = await SendBearerAsync(http, HttpMethod.Get, "/api/admin/v1/packages", ownerToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ownerAdmin.StatusCode);

        using var revoke = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/v1/clients/{clientId}/tokens/{tokenId}/revoke");
        revoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ownerToken);
        using var revoked = await authentication.SendAsync(revoke);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        var rejected = await Assert.ThrowsAsync<HttpRequestException>(async () => await mcp.ListToolsAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
    }

    /// <summary>
    /// Publishes only the public portion of the supplied signing key through native discovery.
    /// </summary>
    /// <returns>The JWKS privacy and key-identity assertions.</returns>
    [Fact]
    public async Task DiscoveryExposesOnlyPublicSigningMaterial()
    {
        using var client = _app.GetTestClient();
        using var discovery = await client.GetAsync("/.well-known/openid-configuration");
        discovery.EnsureSuccessStatusCode();
        using var configuration = JsonDocument.Parse(await discovery.Content.ReadAsStringAsync());
        using var response = await client.GetAsync(configuration.RootElement.GetProperty("jwks_uri").GetString());
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var key = Assert.Single(document.RootElement.GetProperty("keys").EnumerateArray());
        var signing = Assert.IsType<RsaSecurityKey>(_app.Services.GetRequiredService<IOptions<OpenIddictServerOptions>>()
            .Value.SigningCredentials[0].Key);

        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal(Base64UrlEncoder.Encode(signing.Parameters.Modulus!), key.GetProperty("n").GetString());
        foreach (var privateField in new[] { "d", "p", "q", "dp", "dq", "qi", "oth", "k" })
        {
            Assert.False(key.TryGetProperty(privateField, out _));
        }
    }

    /// <summary>
    /// Rejects missing and malformed Bearer tokens on every protected Admin route before model binding or business execution.
    /// </summary>
    /// <param name="method">The protected operation's HTTP method.</param>
    /// <param name="path">The concrete route with valid identifier segments.</param>
    /// <returns>The native challenge and safe-envelope assertions.</returns>
    [Theory]
    [InlineData("GET", "/api/admin/v1/users")]
    [InlineData("GET", "/api/admin/v1/users/me")]
    [InlineData("POST", "/api/admin/v1/users/me/password")]
    [InlineData("GET", "/api/admin/v1/users/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/admin/v1/users/invitations")]
    [InlineData("PUT", "/api/admin/v1/users/00000000-0000-0000-0000-000000000001/status")]
    [InlineData("POST", "/api/admin/v1/users/00000000-0000-0000-0000-000000000001/invitation")]
    [InlineData("GET", "/api/admin/v1/clients")]
    [InlineData("GET", "/api/admin/v1/clients/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/admin/v1/clients")]
    [InlineData("PUT", "/api/admin/v1/clients/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/admin/v1/clients/00000000-0000-0000-0000-000000000001/tokens")]
    [InlineData("POST", "/api/admin/v1/clients/00000000-0000-0000-0000-000000000001/tokens/00000000-0000-0000-0000-000000000002/revoke")]
    [InlineData("GET", "/api/admin/v1/roles")]
    [InlineData("PUT", "/api/admin/v1/users/00000000-0000-0000-0000-000000000001/roles")]
    public async Task ProtectedAdminRoutesRequireNativeBearerValidation(string method, string path)
    {
        using var client = _app.GetTestClient();
        foreach (var malformed in new[] { false, true })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            if (malformed)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-access-token");
            }

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Contains("error_unauthorized", await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// Rechecks live Owner state even while the stored access token remains otherwise valid.
    /// </summary>
    /// <returns>The native forbidden response after current account access is removed.</returns>
    [Fact]
    public async Task ValidAccessTokenCannotAuthorizeAnInactiveOwner()
    {
        using var client = _app.GetTestClient();
        var owner = await RegisterAsync(client, "owner");
        var cookie = await LoginAsync(client, "owner");
        var grant = await AuthorizeAsync(client, cookie, swagger: true);
        var issued = await ExchangeAsync(client, grant.Code, grant.Verifier, swagger: true);
        var access = issued.GetProperty("access_token").GetString()!;

        // Preserve token entries to exercise the current-state policy independently of grant revocation.
        using (var scope = _app.Services.CreateScope())
        {
            var userId = owner.GetProperty("id").GetGuid();
            await scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>().Users
                .Where(user => user.Id == userId).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.IsActive, false));
        }

        using var response = await SendBearerAsync(client, HttpMethod.Get, "/api/admin/v1/users/me", access);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("error_forbidden", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Creates multiple Owners anonymously, then signs in through Razor and Swagger's native PKCE flow.
    /// </summary>
    /// <returns>The registration, PKCE, metadata-preservation and removed-route assertions.</returns>
    [Fact]
    public async Task AnonymousRegistrationAndSwaggerPkceUseNativePipeline()
    {
        using var client = _app.GetTestClient();
        var first = await RegisterAsync(client, "owner");
        var second = await RegisterAsync(client, "second-owner");
        Assert.NotEqual(first.GetProperty("id").GetGuid(), second.GetProperty("id").GetGuid());
        Assert.False(first.TryGetProperty("twoFactorEnabled", out _));

        using var duplicate = await client.PostAsJsonAsync("/api/admin/v1/users",
            new CreateUserRequestDto("other", "owner@example.test", "Native-test-Password!123"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        // The removed action stays absent; unsupported requests retain native routing behavior.
        using var anonymousRemovedEndpoint = await client.PostAsync("/api/admin/v1/configuration/oidc/synchronize", null);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, anonymousRemovedEndpoint.StatusCode);
        using var openApi = await client.GetAsync("/openapi/v1.json");
        var document = await ReadJsonAsync(openApi);
        Assert.False(document.GetProperty("paths").TryGetProperty("/api/admin/v1/configuration/oidc/synchronize", out _));
        var routes = _app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
        Assert.NotEmpty(routes);
        Assert.DoesNotContain(routes, endpoint => endpoint.RoutePattern.RawText?.TrimStart('/') ==
            "api/admin/v1/configuration/oidc/synchronize");

        var cookie = await LoginAsync(client, "owner");
        var grant = await AuthorizeAsync(client, cookie, swagger: true);
        var token = await ExchangeAsync(client, grant.Code, grant.Verifier, swagger: true);
        Assert.False(token.TryGetProperty("id_token", out _));
        Assert.False(token.TryGetProperty("refresh_token", out _));
        var access = token.GetProperty("access_token").GetString()!;

        using var current = await SendBearerAsync(client, HttpMethod.Get, "/api/admin/v1/users/me", access);
        var profile = await ReadJsonAsync(current);
        Assert.Equal(first.GetProperty("id").GetGuid(), profile.GetProperty("data").GetProperty("id").GetGuid());
        using (var scope = _app.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = (await manager.FindByClientIdAsync(AuthenticationConstants.ADMIN_CLIENT))!;
            var changed = new OpenIddictApplicationDescriptor();
            await manager.PopulateAsync(changed, application);
            changed.RedirectUris.Clear();
            changed.RedirectUris.Add(new Uri("https://localhost/obsolete-callback"));
            await manager.UpdateAsync(application, changed);
        }

        // Account registration leaves existing client metadata untouched.
        await RegisterAsync(client, "third-owner");
        using (var scope = _app.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = (await manager.FindByClientIdAsync(AuthenticationConstants.ADMIN_CLIENT))!;
            Assert.Equal("https://localhost/obsolete-callback", Assert.Single(await manager.GetRedirectUrisAsync(application)));
        }

        using var removedEndpoint = await SendBearerAsync(client, HttpMethod.Post,
            "/api/admin/v1/configuration/oidc/synchronize", access);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, removedEndpoint.StatusCode);

        using (var scope = _app.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
            var application = (await manager.FindByClientIdAsync(AuthenticationConstants.ADMIN_CLIENT))!;
            Assert.Equal("https://localhost/obsolete-callback", Assert.Single(await manager.GetRedirectUrisAsync(application)));
            var resource = (await manager.FindByClientIdAsync(AuthenticationConstants.RESOURCE))!;
            Assert.True(await manager.ValidateClientSecretAsync(resource, new string('x', 32)));
            Assert.Equal(3, await manager.CountAsync());
        }

        var rejectedGrant = await AuthorizeAsync(client, cookie, swagger: true);
        using var invalidProof = await client.PostAsync("/connect/token",
            CodeForm(rejectedGrant.Code, WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32)), swagger: true));
        Assert.Equal("invalid_grant", (await ReadJsonAsync(invalidProof, HttpStatusCode.BadRequest))
            .GetProperty("error").GetString());

        using var replay = await client.PostAsync("/connect/token",
            CodeForm(grant.Code, grant.Verifier, swagger: true));
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
        Assert.Equal("invalid_grant", (await ReadJsonAsync(replay, HttpStatusCode.BadRequest)).GetProperty("error").GetString());
    }

    /// <summary>
    /// Rotates refresh tokens natively and revokes only the browser session confirmed at logout.
    /// </summary>
    /// <returns>The native replay, token-family and independent-session assertions.</returns>
    [Fact]
    public async Task NativeRefreshReplayAndLogoutPreserveOtherSession()
    {
        using var client = _app.GetTestClient();
        await RegisterAsync(client, "owner");
        var firstCookie = await LoginAsync(client, "owner");
        var originalGrant = await AuthorizeAsync(client, firstCookie, swagger: false);
        var original = await ExchangeAsync(client, originalGrant.Code, originalGrant.Verifier, swagger: false);
        var refresh = original.GetProperty("refresh_token").GetString()!;

        using var rotatedResponse = await client.PostAsync("/connect/token", RefreshForm(refresh));
        var rotated = await ReadJsonAsync(rotatedResponse);
        Assert.NotEqual(refresh, rotated.GetProperty("refresh_token").GetString());

        // Advance beyond the native reuse allowance rather than assuming immediate rejection.
        _clock.Advance(TimeSpan.FromMinutes(2));
        using var replay = await client.PostAsync("/connect/token", RefreshForm(refresh));
        Assert.Equal("invalid_grant", (await ReadJsonAsync(replay, HttpStatusCode.BadRequest)).GetProperty("error").GetString());

        var firstGrant = await AuthorizeAsync(client, firstCookie, swagger: false);
        var firstToken = await ExchangeAsync(client, firstGrant.Code, firstGrant.Verifier, swagger: false);
        using var createClient = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/clients");
        createClient.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firstToken.GetProperty("access_token").GetString());
        createClient.Content = JsonContent.Create(new CreateClientRequestDto("independent-client") { AllowedTools = ["catalog_search"] });
        using var issuedClient = await client.SendAsync(createClient);
        var clientToken = (await ReadJsonAsync(issuedClient, HttpStatusCode.Created)).GetProperty("data").GetProperty("token").GetString()!;
        var secondCookie = await LoginAsync(client, "owner");
        var secondGrant = await AuthorizeAsync(client, secondCookie, swagger: false);
        var secondToken = await ExchangeAsync(client, secondGrant.Code, secondGrant.Verifier, swagger: false);
        await LogoutAsync(client, firstCookie);

        using var rejected = await SendBearerAsync(client, HttpMethod.Get, "/api/admin/v1/users/me",
            firstToken.GetProperty("access_token").GetString()!);
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        using var retained = await SendBearerAsync(client, HttpMethod.Get, "/api/admin/v1/users/me",
            secondToken.GetProperty("access_token").GetString()!);
        Assert.Equal(HttpStatusCode.OK, retained.StatusCode);
        using var otherRefresh = await client.PostAsync("/connect/token", RefreshForm(secondToken.GetProperty("refresh_token").GetString()!));
        Assert.Equal(HttpStatusCode.OK, otherRefresh.StatusCode);
        Assert.True((await IntrospectAsync(client, clientToken)).GetProperty("active").GetBoolean());
    }

    /// <summary>
    /// Rejects copied logout cookies even without earlier grants, while another browser and a fresh login remain usable.
    /// </summary>
    /// <param name="issueGrant">Whether this cookie previously obtained an authorization family.</param>
    /// <param name="swagger">Whether to replay against the documentation client.</param>
    /// <returns>The native cookie rejection and independent-session assertions.</returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task CopiedCookieCannotCreateGrantAfterLogout(bool issueGrant, bool swagger)
    {
        using var client = _app.GetTestClient();
        await RegisterAsync(client, "owner");
        var copiedCookie = await LoginAsync(client, "owner");
        if (issueGrant)
        {
            await AuthorizeAsync(client, copiedCookie, swagger);
        }

        var independentCookie = await LoginAsync(client, "owner");
        await LogoutAsync(client, copiedCookie);

        // Reuse the exact protected cookie rather than the browser's cleared cookie jar.
        using var replay = new HttpRequestMessage(HttpMethod.Get, AuthorizationUri("replayed-pkce-proof-012345678901234567890123456789", swagger));
        replay.Headers.Add("Cookie", copiedCookie);
        using var response = await client.SendAsync(replay);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(AuthenticationConstants.Endpoints.LOGIN_PAGE, response.Headers.Location!.AbsolutePath);

        await AuthorizeAsync(client, independentCookie, swagger);
        await AuthorizeAsync(client, await LoginAsync(client, "owner"), swagger);
    }

    /// <summary>
    /// Uses the actual introspection endpoint for issued, expired, revoked and disabled application credentials.
    /// </summary>
    /// <returns>The live token-state and identity-separation assertions.</returns>
    [Fact]
    public async Task OpaqueClientLifecycleIsCheckedByNativeIntrospection()
    {
        using var client = _app.GetTestClient();
        await RegisterAsync(client, "owner");
        var cookie = await LoginAsync(client, "owner");
        var grant = await AuthorizeAsync(client, cookie, swagger: true);
        var owner = await ExchangeAsync(client, grant.Code, grant.Verifier, swagger: true);
        var access = owner.GetProperty("access_token").GetString()!;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/clients");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        request.Content = JsonContent.Create(new CreateClientRequestDto("protocol-client")
        {
            AllowedTools = ["catalog_search"],
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
        using var issuedResponse = await client.SendAsync(request);
        var issued = (await ReadJsonAsync(issuedResponse, HttpStatusCode.Created)).GetProperty("data");
        var raw = issued.GetProperty("token").GetString()!;
        var clientId = issued.GetProperty("clientId").GetGuid();
        var tokenId = issued.GetProperty("tokenId").GetGuid();

        Assert.True((await IntrospectAsync(client, raw)).GetProperty("active").GetBoolean());
        using var forbidden = await SendBearerAsync(client, HttpMethod.Get, "/api/admin/v1/users/me", raw);
        Assert.Equal(HttpStatusCode.Unauthorized, forbidden.StatusCode);
        using var revoke = await SendBearerAsync(client, HttpMethod.Post,
            $"/api/admin/v1/clients/{clientId}/tokens/{tokenId}/revoke", access);
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        Assert.False((await IntrospectAsync(client, raw)).GetProperty("active").GetBoolean());

        using var issueRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/admin/v1/clients/{clientId}/tokens");
        issueRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        issueRequest.Content = JsonContent.Create(new IssueClientTokenRequestDto { AllowedTools = ["catalog_search"] });
        using var unlimitedResponse = await client.SendAsync(issueRequest);
        var unlimited = (await ReadJsonAsync(unlimitedResponse, HttpStatusCode.Created)).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, unlimited.GetProperty("expiresAt").ValueKind);
        var unlimitedRaw = unlimited.GetProperty("token").GetString()!;
        Assert.True((await IntrospectAsync(client, unlimitedRaw)).GetProperty("active").GetBoolean());

        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
            // Arrange elapsed time in the isolated store without waiting or bypassing production validation.
            await db.ClientTokens.Where(token => token.Id == unlimited.GetProperty("tokenId").GetGuid())
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        }
        Assert.False((await IntrospectAsync(client, unlimitedRaw)).GetProperty("active").GetBoolean());

        using (var scope = _app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
            await db.ClientTokens.Where(token => token.Id == unlimited.GetProperty("tokenId").GetGuid())
                .ExecuteUpdateAsync(update => update.SetProperty(token => token.ExpiresAt, (DateTimeOffset?)null));
            await db.Clients.Where(value => value.Id == clientId)
                .ExecuteUpdateAsync(update => update.SetProperty(value => value.IsActive, false));
        }
        Assert.False((await IntrospectAsync(client, unlimitedRaw)).GetProperty("active").GetBoolean());
    }

    /// <summary>
    /// Rejects malformed role elements through the real Bearer, Mediator and validation pipeline before mutation.
    /// </summary>
    /// <returns>The safe HTTP validation and unchanged account-membership assertions.</returns>
    [Fact]
    public async Task MalformedRoleElementsReturnValidationWithoutMutation()
    {
        using var client = _app.GetTestClient();
        var owner = await RegisterAsync(client, "owner");
        var cookie = await LoginAsync(client, "owner");
        var (code, verifier) = await AuthorizeAsync(client, cookie, swagger: true);
        var grant = await ExchangeAsync(client, code, verifier, swagger: true);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", grant.GetProperty("access_token").GetString());
        var id = owner.GetProperty("id").GetGuid();

        foreach (var role in new string?[] { null, "", " ", "\t" })
        {
            using var invitation = await client.PostAsJsonAsync("/api/admin/v1/users/invitations", new
            {
                userName = "invited", email = "invited@example.test", roleNames = new[] { "Owner", role }
            });
            using var replacement = await client.PutAsJsonAsync($"/api/admin/v1/users/{id}/roles", new
            {
                roleNames = new[] { "Owner", role }, concurrencyToken = owner.GetProperty("concurrencyToken").GetString()
            });
            Assert.Equal("error_validation", (await ReadJsonAsync(invitation, HttpStatusCode.BadRequest))
                .GetProperty("error").GetProperty("code").GetString());
            Assert.Equal("error_validation", (await ReadJsonAsync(replacement, HttpStatusCode.BadRequest))
                .GetProperty("error").GetProperty("code").GetString());
        }

        using var observer = _app.Services.CreateScope();
        var db = observer.ServiceProvider.GetRequiredService<AuthenticationDbContext>();
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.UserRoles.CountAsync());
        Assert.Equal(owner.GetProperty("concurrencyToken").GetString(),
            (await db.Users.AsNoTracking().SingleAsync()).ConcurrencyStamp);
    }

    /// <summary>
    /// Registers a real account through the public transport without pre-seeding a product user.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="name">The unique test account name.</param>
    /// <returns>The safe profile from the created response.</returns>
    private static async Task<JsonElement> RegisterAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/v1/users",
            new CreateUserRequestDto(name, name + "@example.test", "Native-test-Password!123"));

        return (await ReadJsonAsync(response, HttpStatusCode.Created)).GetProperty("data");
    }

    /// <summary>
    /// Submits the real Razor password form with its native antiforgery proof.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="name">The account created for this test.</param>
    /// <returns>The protected browser cookies, never written to diagnostic output.</returns>
    private static async Task<string> LoginAsync(HttpClient client, string name)
    {
        using var page = await client.GetAsync("/login");
        var proof = Antiforgery(await page.Content.ReadAsStringAsync());
        using var request = new HttpRequestMessage(HttpMethod.Post, "/login");
        request.Headers.Add("Cookie", Cookies(page));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.UserName"] = name,
            ["Input.Password"] = "Native-test-Password!123",
            ["ReturnUrl"] = "/",
            ["__RequestVerificationToken"] = proof
        });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return Cookies(response);
    }

    /// <summary>
    /// Creates one native authorization request with exact callbacks and per-request PKCE proof.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="cookie">The password-authenticated browser session.</param>
    /// <param name="swagger">Whether to exercise the OAuth-only documentation client.</param>
    /// <returns>The code and private verifier used only by this test.</returns>
    private static async Task<(string Code, string Verifier)> AuthorizeAsync(HttpClient client, string cookie, bool swagger)
    {
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthorizationUri(verifier, swagger));
        request.Headers.Add("Cookie", cookie);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var parameters = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        Assert.True(parameters.ContainsKey("code"));

        return (parameters["code"].ToString(), verifier);
    }

    /// <summary>
    /// Builds the same valid native request for successful issuance and copied-cookie rejection.
    /// </summary>
    /// <param name="verifier">The PKCE proof whose challenge is sent to the server.</param>
    /// <param name="swagger">Selects the registered protocol client and callback.</param>
    /// <returns>The authorization endpoint with its required protocol parameters.</returns>
    private static string AuthorizationUri(string verifier, bool swagger)
    {
        var query = new Dictionary<string, string?>
        {
            ["client_id"] = swagger ? AuthenticationConstants.SWAGGER_CLIENT : AuthenticationConstants.ADMIN_CLIENT,
            ["response_type"] = "code",
            ["redirect_uri"] = swagger ? "https://localhost/swagger/oauth2-redirect.html" : "https://localhost/auth/callback",
            ["scope"] = swagger ? AuthenticationConstants.ADMIN_SCOPE : "openid profile offline_access " + AuthenticationConstants.ADMIN_SCOPE,
            ["state"] = Guid.NewGuid().ToString("N"),
            ["code_challenge"] = WebEncoders.Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))),
            ["code_challenge_method"] = "S256"
        };
        if (!swagger)
        {
            query["nonce"] = Guid.NewGuid().ToString("N");
        }

        return QueryHelpers.AddQueryString("/connect/authorize", query);
    }

    /// <summary>
    /// Redeems a native authorization code without an implicit or password grant.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="code">The native one-use authorization code.</param>
    /// <param name="verifier">The corresponding private PKCE proof.</param>
    /// <param name="swagger">Selects the same client and callback used during authorization.</param>
    /// <returns>The native token response without wrapping it in an Admin envelope.</returns>
    private static async Task<JsonElement> ExchangeAsync(HttpClient client, string code, string verifier, bool swagger)
    {
        using var response = await client.PostAsync("/connect/token", CodeForm(code, verifier, swagger));
        Assert.True(response.Headers.CacheControl!.NoStore);

        return await ReadJsonAsync(response);
    }

    /// <summary>
    /// Builds the native form matching an authorization request.
    /// </summary>
    /// <param name="code">The issued code.</param>
    /// <param name="verifier">The private PKCE proof.</param>
    /// <param name="swagger">Selects the registered client and callback.</param>
    /// <returns>The form owned by its HTTP request.</returns>
    private static FormUrlEncodedContent CodeForm(string code, string verifier, bool swagger) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "authorization_code",
        ["client_id"] = swagger ? AuthenticationConstants.SWAGGER_CLIENT : AuthenticationConstants.ADMIN_CLIENT,
        ["redirect_uri"] = swagger ? "https://localhost/swagger/oauth2-redirect.html" : "https://localhost/auth/callback",
        ["code"] = code,
        ["code_verifier"] = verifier
    });

    /// <summary>
    /// Builds an Owner-only native refresh request.
    /// </summary>
    /// <param name="refresh">The rotating native refresh proof.</param>
    /// <returns>The form owned by its HTTP request.</returns>
    private static FormUrlEncodedContent RefreshForm(string refresh) => new(new Dictionary<string, string>
    {
        ["grant_type"] = "refresh_token",
        ["client_id"] = AuthenticationConstants.ADMIN_CLIENT,
        ["refresh_token"] = refresh
    });

    /// <summary>
    /// Confirms logout through the native antiforgery-protected POST.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="cookie">Only the browser session being ended.</param>
    /// <returns>The logout assertions.</returns>
    private static async Task LogoutAsync(HttpClient client, string cookie)
    {
        using var pageRequest = new HttpRequestMessage(HttpMethod.Get, "/logout");
        pageRequest.Headers.Add("Cookie", cookie);
        using var page = await client.SendAsync(pageRequest);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/logout");
        request.Headers.Add("Cookie", cookie + "; " + Cookies(page));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = Antiforgery(await page.Content.ReadAsStringAsync())
        });
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    /// <summary>
    /// Uses a backend credential only for native introspection, never as an end-client token.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="token">The opaque credential whose live state must be checked.</param>
    /// <returns>The native introspection JSON.</returns>
    private static async Task<JsonElement> IntrospectAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/connect/introspect");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(AuthenticationConstants.RESOURCE + ":" + new string('x', 32))));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token });
        using var response = await client.SendAsync(request);

        return await ReadJsonAsync(response);
    }

    /// <summary>
    /// Sends one explicit Bearer request without changing another request's ambient identity.
    /// </summary>
    /// <param name="client">The isolated host client.</param>
    /// <param name="method">The requested method.</param>
    /// <param name="path">The local resource.</param>
    /// <param name="access">The credential used only for this request.</param>
    /// <returns>The response owned by the caller.</returns>
    private static async Task<HttpResponseMessage> SendBearerAsync(HttpClient client, HttpMethod method, string path, string access)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);

        return await client.SendAsync(request);
    }

    /// <summary>
    /// Reads protocol JSON once without logging raw credential payloads.
    /// </summary>
    /// <param name="response">The completed HTTP response.</param>
    /// <param name="expected">The expected native status.</param>
    /// <returns>A detached JSON value valid after its document is disposed.</returns>
    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        return document.RootElement.Clone();
    }

    /// <summary>
    /// Extracts only the native hidden form proof without executing or rewriting the page.
    /// </summary>
    /// <param name="html">The rendered native form.</param>
    /// <returns>The decoded antiforgery value.</returns>
    private static string Antiforgery(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    /// <summary>
    /// Transfers browser cookies within this test without writing their values to logs.
    /// </summary>
    /// <param name="response">The response issuing native cookie headers.</param>
    /// <returns>The Cookie header value for the next browser request.</returns>
    private static string Cookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? string.Join("; ", values.Select(value => value.Split(';')[0]))
            : "";

    /// <summary>
    /// Stops the host and removes only this test's database container.
    /// </summary>
    /// <returns>The owned resource cleanup.</returns>
    public async Task DisposeAsync()
    {
        await _app.DisposeAsync();
        await _database.DisposeAsync();
    }
}
