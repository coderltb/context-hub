namespace ContextHub.Backend.Tests.Services.Authentication;

/// <summary>
/// Exercises the executable host composition without substituting authentication handlers or touching a database.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class AuthenticationHttpTests
{
    /// <summary>
    /// Protects current-account reads and password changes before persistence or credential handling can execute.
    /// </summary>
    /// <param name="method">The operation's HTTP method.</param>
    /// <param name="path">The current-user route without a caller-selected account identifier.</param>
    /// <returns>The native unauthorized envelope assertion.</returns>
    [Theory]
    [InlineData("GET", "/api/admin/v1/users/me")]
    [InlineData("POST", "/api/admin/v1/users/me/password")]
    public async Task CurrentUserOperationsRequireOwnerAuthentication(string method, string path)
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("error_unauthorized", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Keeps every native health probe anonymous even when a caller sends an invalid Bearer token.
    /// </summary>
    /// <param name="path">The native health route outside the Admin boundary.</param>
    /// <returns>The anonymous native health response assertions.</returns>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health/recovery")]
    public async Task HealthRoutesDoNotRequireBearerAuthentication(string path)
    {
        await using var app = await CreateHostAsync(configure: services =>
        {
            // Dependency health is tested separately; this fixture isolates endpoint authorization.
            services.Configure<HealthCheckServiceOptions>(options => options.Registrations.Clear());
            services.AddHealthChecks().AddCheck("authorization-fixture", () => HealthCheckResult.Healthy(),
                tags: ["live", "ready", "recovery"]);
        });
        using var client = app.GetTestClient();
        foreach (var malformed in new[] { false, true })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            if (malformed)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-access-token");
            }

            using var response = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Healthy", await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>
    /// Uses the real native process check without opening the Authentication database or applying an Admin envelope.
    /// </summary>
    /// <returns>The native liveness contract assertions.</returns>
    [Fact]
    public async Task LivenessUsesSharedNativeContract()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.False(body.RootElement.TryGetProperty("success", out _));
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    /// <summary>
    /// Renders native Razor forms and issues an antiforgery token instead of leaving the SSO routes unmapped.
    /// </summary>
    /// <param name="path">The implemented anonymous SSO form.</param>
    /// <returns>The HTTP rendering assertions.</returns>
    [Theory]
    [InlineData("/forgot-password")]
    [InlineData("/verify-password-reset-otp")]
    [InlineData("/login")]
    [InlineData("/logout")]
    public async Task SsoFormsHaveAntiforgeryAndNoStore(string path)
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.DoesNotContain("validation-summary-errors", html);
        if (!path.EndsWith("/logout", StringComparison.Ordinal))
        {
            Assert.Contains("name=\"ReturnUrl\"", html);
            Assert.Contains("value=\"/\"", html);
        }

        if (path.EndsWith("/logout", StringComparison.Ordinal))
        {
            Assert.Contains("action=\"/connect/logout\"", html);
        }
    }

    /// <summary>
    /// Serves the layout's versioned local icon anonymously through the native static-asset endpoint.
    /// </summary>
    /// <returns>The rendered link and actual SVG response assertions.</returns>
    [Fact]
    public async Task SsoFaviconIsVersionedAndAnonymous()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        var html = await client.GetStringAsync("/login");
        var icon = Regex.Match(html, "<link[^>]*rel=\"icon\"[^>]*href=\"([^\"]+)\"");
        Assert.True(icon.Success, "The shared layout must declare its local favicon.");
        var path = WebUtility.HtmlDecode(icon.Groups[1].Value);
        Assert.StartsWith("/favicon.svg?v=", path, StringComparison.Ordinal);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-access-token");

        using var response = await client.SendAsync(request);
        var svg = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("viewBox=\"0 0 24 24\"", svg);
        Assert.DoesNotContain("<script", svg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("foreignObject", svg, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rejects an external continuation before an account query or sign-in can run.
    /// </summary>
    /// <returns>The local-redirect boundary assertions.</returns>
    [Fact]
    public async Task ExternalReturnUrlIsRejected()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/login?returnUrl=https%3A%2F%2Fevil.example");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    /// <summary>
    /// Rejects a forged form before credential handling; this deliberately supplies no antiforgery cookie or token.
    /// </summary>
    /// <returns>The native antiforgery response assertions.</returns>
    [Fact]
    public async Task LoginPostRequiresAntiforgery()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.UserName"] = "untrusted",
            ["Input.Password"] = "never-log-this"
        });
        using var response = await client.PostAsync("/login", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("never-log-this", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Keeps Admin JSON unauthorized responses separate from the HTML login form used for interactive sign-in.
    /// </summary>
    /// <returns>The native scheme and envelope assertions.</returns>
    [Fact]
    public async Task AdminAndRazorUseTheirOwnAuthenticationResponses()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var api = await client.GetAsync("/api/admin/v1/clients");
        using var page = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
        Assert.Contains("error_unauthorized", await api.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("Input.Password", await page.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Serves Swagger from the native OpenAPI document without sample or protocol-controller operations.
    /// </summary>
    /// <returns>The published development surface assertions.</returns>
    [Fact]
    public async Task SwaggerUsesNativeDocumentAndOmitsScaffold()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var ui = await client.GetAsync("/swagger/index.html");
        using var schema = await client.GetAsync("/openapi/v1.json");
        using var sample = await client.GetAsync("/weatherforecast");
        var document = await schema.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        Assert.Equal(HttpStatusCode.OK, schema.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sample.StatusCode);
        Assert.Contains("/api/admin/v1/clients", document);

        using var json = JsonDocument.Parse(document);
        var schemas = json.RootElement.GetProperty("components").GetProperty("schemas");
        var ownerProperties = schemas.GetProperty(nameof(CreateUserRequestDto)).GetProperty("properties");
        var passwordSchema = ownerProperties.GetProperty("password");
        Assert.False(passwordSchema.TryGetProperty("readOnly", out var readOnly) && readOnly.GetBoolean());
        Assert.False(ownerProperties.TryGetProperty("roleNames", out _));
        var paths = json.RootElement.GetProperty("paths");
        Assert.False(paths.TryGetProperty("/connect/token", out _));
        Assert.False(paths.TryGetProperty("/connect/authorize", out _));
        Assert.False(
            paths.GetProperty("/api/admin/v1/users").GetProperty("post").TryGetProperty("security", out var security) &&
            security.GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/admin/v1/clients").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
    }

    /// <summary>
    /// Documents actual business failures without adding them to collection reads or native protocols.
    /// </summary>
    /// <returns>The endpoint-specific status and shared error-example assertions.</returns>
    [Fact]
    public async Task SwaggerIncludesEndpointBusinessErrors()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var cases = new (string Path, string Method, int[] Statuses)[]
        {
            ("/api/admin/v1/clients/{clientId}", "get", [404]),
            ("/api/admin/v1/clients/{clientId}", "put", [404, 409]),
            ("/api/admin/v1/clients/{clientId}/tokens", "post", [404, 409]),
            ("/api/admin/v1/clients/{clientId}/tokens/{tokenId}/revoke", "post", [404]),
            ("/api/admin/v1/users/{userId}", "get", [404]),
            ("/api/admin/v1/users/{userId}/status", "put", [404, 409]),
            ("/api/admin/v1/users/{userId}/roles", "put", [404, 409]),
            ("/api/admin/v1/users/{userId}/invitation", "post", [404, 409])
        };

        foreach (var item in cases)
        {
            var responses = paths.GetProperty(item.Path).GetProperty(item.Method).GetProperty("responses");
            foreach (var status in item.Statuses)
            {
                var media = responses.GetProperty(status.ToString(CultureInfo.InvariantCulture))
                    .GetProperty("content").GetProperty("application/json");
                Assert.Equal(status == 404 ? "error_not_found" : "error_conflict",
                    media.GetProperty("example").GetProperty("error").GetProperty("code").GetString());
            }
        }

        foreach (var path in new[] { "/api/admin/v1/users", "/api/admin/v1/clients", "/api/admin/v1/roles" })
        {
            var responses = paths.GetProperty(path).GetProperty("get").GetProperty("responses");
            Assert.False(responses.TryGetProperty("404", out _));
            Assert.False(responses.TryGetProperty("409", out _));
        }
    }

    /// <summary>
    /// Publishes shared error bodies once while retaining each action's typed success and effective authorization metadata.
    /// </summary>
    /// <returns>The actual Authentication OpenAPI response-schema and conditional-status assertions.</returns>
    [Fact]
    public async Task SwaggerUsesSharedErrorsAndActionSpecificSuccessTypes()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");

        foreach (var path in new[] { "/api/admin/v1/users" })
        {
            var responses = paths.GetProperty(path).GetProperty("post").GetProperty("responses");
            foreach (var (status, code) in new[] { ("400", "error_validation"), ("409", "error_conflict"), ("500", "error_internal") })
            {
                var media = responses.GetProperty(status).GetProperty("content").GetProperty("application/json");
                var schema = media.GetProperty("schema");
                if (schema.TryGetProperty("$ref", out var reference))
                {
                    schema = document.RootElement.GetProperty("components").GetProperty("schemas")
                        .GetProperty(reference.GetString()!.Split('/')[^1]);
                }

                var properties = schema.GetProperty("properties");
                Assert.True(properties.TryGetProperty("success", out _));
                Assert.True(properties.TryGetProperty("data", out _));
                Assert.True(properties.TryGetProperty("error", out _));
                Assert.Equal(code, media.GetProperty("example").GetProperty("error").GetProperty("code").GetString());
            }

            Assert.Contains(nameof(UserResponseDto), responses.GetProperty("201").GetProperty("content")
                .GetProperty("application/json").GetProperty("schema").GetRawText());
        }

        // Anonymous setup has no challenge/forbid contract; protected operations inherit those statuses from authorization.
        var setup = paths.GetProperty("/api/admin/v1/users").GetProperty("post").GetProperty("responses");
        Assert.False(setup.TryGetProperty("401", out _));
        Assert.False(setup.TryGetProperty("403", out _));
        var roles = paths.GetProperty("/api/admin/v1/roles").GetProperty("get").GetProperty("responses");
        Assert.True(roles.TryGetProperty("401", out _));
        Assert.True(roles.TryGetProperty("403", out _));
        Assert.True(roles.TryGetProperty("500", out _));
        Assert.False(roles.TryGetProperty("400", out _));
        Assert.False(roles.TryGetProperty("409", out _));
    }

    /// <summary>
    /// Removes the redundant setup route rather than keeping a second registration contract.
    /// </summary>
    /// <returns>The removed endpoint assertion without persistence access.</returns>
    [Fact]
    public async Task LegacySetupEndpointIsRemoved()
    {
        await using var app = await CreateHostAsync(disableStaticFallback: true);
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/admin/v1/setup/owner", new { userName = "" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Leaves an already protocol-validated post-logout URI to OpenIddict rather than overriding it with a local path.
    /// </summary>
    /// <param name="redirect">The protocol redirect already checked before controller dispatch.</param>
    /// <param name="fallback">The local fallback only when no protocol redirect was requested.</param>
    /// <returns>The thin protocol adapter assertions, not an allowlist validation substitute.</returns>
    [Theory]
    [InlineData("https://admin.example/signed-out", null)]
    [InlineData(null, "/")]
    public async Task LogoutPreservesNativeRedirectSelection(string? redirect, string? fallback)
    {
        await using var app = await CreateHostAsync();
        using var scope = app.Services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        http.Request.Scheme = "https";
        http.Features.Set(new OpenIddictServerAspNetCoreFeature
        {
            Transaction = new OpenIddictServerTransaction
            {
                Request = new OpenIddictRequest { PostLogoutRedirectUri = redirect }
            }
        });
        var controller = ActivatorUtilities.CreateInstance<AuthorizationController>(scope.ServiceProvider);
        controller.ControllerContext = new ControllerContext { HttpContext = http };
        var result = Assert.IsType<SignOutResult>(await controller.LogoutConfirmed(CancellationToken.None));

        Assert.Equal(fallback, result.Properties!.RedirectUri);
    }

    /// <summary>
    /// Removes authenticator and recovery-code entry points while keeping password recovery separate.
    /// </summary>
    /// <param name="path">The retired recovery endpoint.</param>
    /// <returns>The GET and POST routing assertions.</returns>
    [Theory]
    [InlineData("/Identity/Account/LoginWith2fa")]
    [InlineData("/Identity/Account/Manage/EnableAuthenticator")]
    [InlineData("/Identity/Account/LoginWithRecoveryCode")]
    [InlineData("/Identity/Account/Manage/GenerateRecoveryCodes")]
    public async Task RemovedAuthenticatorAndRecoveryCodeRoutesReturnNotFound(string path)
    {
        await using var app = await CreateHostAsync(disableStaticFallback: true);
        using var client = app.GetTestClient();
        using var get = await client.GetAsync(path);
        using var post = await client.PostAsync(path, new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, post.StatusCode);
    }

    /// <summary>
    /// Reaches input validation anonymously without opening a database for invalid registration input.
    /// </summary>
    /// <returns>The public registration boundary assertions.</returns>
    [Fact]
    public async Task AnonymousRegistrationReachesValidation()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var response = await client.PostAsJsonAsync("/api/admin/v1/users", new { userName = "", email = "invalid", password = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("error_validation", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Offers OAuth code flow alongside manual Bearer entry and keeps registration anonymous.
    /// </summary>
    /// <returns>The generated security contract assertions.</returns>
    [Fact]
    public async Task SwaggerOffersPkceAndPublicRegistration()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var schemes = root.GetProperty("components").GetProperty("securitySchemes");

        Assert.Contains(schemes.EnumerateObject(), item => item.Value.GetProperty("type").GetString() == "oauth2");
        var registration = root.GetProperty("paths").GetProperty("/api/admin/v1/users").GetProperty("post");
        Assert.False(registration.TryGetProperty("security", out var security) && security.GetArrayLength() > 0);
        Assert.DoesNotContain("twoFactorEnabled", root.GetRawText());
    }

    /// <summary>
    /// Accepts forwarded scheme/address only from the explicitly trusted immediate proxy.
    /// </summary>
    /// <param name="trusted">Whether the socket peer matches the configured proxy.</param>
    /// <returns>The native trust-boundary assertions.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForwardedHeadersRespectConfiguredProxy(bool trusted)
    {
        await using var app = await CreateHostAsync(configure: services =>
            services.Configure<AuthenticationServiceOptions>(options => options.KnownProxies = ["10.20.30.40"]));
        var context = await app.GetTestServer().SendAsync(http =>
        {
            http.Connection.RemoteIpAddress = IPAddress.Parse(trusted ? "10.20.30.40" : "10.20.30.41");
            http.Request.Scheme = "http";
            http.Request.Host = new HostString("localhost");
            http.Request.Method = "GET";
            http.Request.Path = "/health/live";
            http.Request.Headers["X-Forwarded-Proto"] = "https";
            http.Request.Headers["X-Forwarded-For"] = "198.51.100.10";
        });

        Assert.Equal(trusted ? "https" : "http", context.Request.Scheme);
        Assert.Equal(trusted ? "198.51.100.10" : "10.20.30.41", context.Connection.RemoteIpAddress!.ToString());
    }

    /// <summary>
    /// Publishes XML descriptions from the host and referenced DTO projects alongside typed examples.
    /// </summary>
    /// <returns>The real document metadata assertions.</returns>
    [Fact]
    public async Task SwaggerIncludesDescriptionsAndAuthenticationExamples()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var users = paths.GetProperty("/api/admin/v1/users");

        Assert.False(string.IsNullOrWhiteSpace(users.GetProperty("post").GetProperty("summary").GetString()));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var email = schemas.GetProperty(nameof(CreateUserRequestDto)).GetProperty("properties").GetProperty("email");
        Assert.Contains("email", email.GetProperty("description").GetString(), StringComparison.OrdinalIgnoreCase);

        // Ordinary examples come from DTO property metadata, without constructing duplicate fixture objects.
        Assert.Equal("owner@example.test", email.GetProperty("examples")[0].GetString());
        var envelope = schemas.GetProperty("ApiResponseOfUserResponseDto").GetProperty("properties");
        Assert.Equal(JsonValueKind.Null, envelope.GetProperty("error").GetProperty("examples")[0].ValueKind);
        Assert.Equal(1, envelope.GetProperty("error").GetProperty("oneOf").EnumerateArray()
            .Count(branch => branch.TryGetProperty("type", out var type) && type.GetString() == "null"));
        Assert.Equal(7, envelope.GetProperty("meta").GetProperty("examples")[0].EnumerateObject().Count());
        var status = paths.GetProperty("/api/admin/v1/users/{userId}/status").GetProperty("put")
            .GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json")
            .GetProperty("examples").GetProperty("default").GetProperty("value");
        Assert.False(status.GetProperty("data").GetProperty("isActive").GetBoolean());
        Assert.Equal("documentation-example", status.GetProperty("meta").GetProperty("requestId").GetString());
        Assert.Equal(DateTimeOffset.UnixEpoch, status.GetProperty("meta").GetProperty("requestTimestamp").GetDateTimeOffset());

        var issued = paths.GetProperty("/api/admin/v1/clients/{clientId}/tokens").GetProperty("post")
            .GetProperty("responses").GetProperty("201").GetProperty("content").GetProperty("application/json")
            .GetProperty("examples").GetProperty("default").GetProperty("value");
        Assert.Equal(DateTimeOffset.UnixEpoch, issued.GetProperty("meta").GetProperty("requestTimestamp").GetDateTimeOffset());
        Assert.True(issued.GetProperty("data").GetProperty("expiresAt").GetDateTimeOffset() > DateTimeOffset.UnixEpoch);


    }

    /// <summary>
    /// Serves the shared operation-search asset with the interactive development UI.
    /// </summary>
    /// <returns>The anonymous local asset and UI configuration assertions.</returns>
    [Fact]
    public async Task SwaggerServesSharedSearchAsset()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        using var script = await client.GetAsync("/swagger-assets/operation-search.js");
        var html = await client.GetStringAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal("application/javascript", script.Content.Headers.ContentType!.MediaType);
        Assert.Contains("/swagger-assets/operation-search.js", html);
        var initializer = await client.GetStringAsync("/swagger/index.js");
        Assert.Contains("\"displayRequestDuration\":true", initializer);
    }

    /// <summary>
    /// Keeps interactive documentation and its embedded asset outside the Production surface.
    /// </summary>
    /// <returns>The Production absence and anonymous liveness assertions.</returns>
    [Fact]
    public async Task ProductionDoesNotExposeSwaggerAssets()
    {
        await using var app = await CreateHostAsync(environment: Environments.Production);
        using var client = app.GetTestClient();

        foreach (var path in new[] { "/swagger/index.html", "/openapi/v1.json", "/swagger-assets/operation-search.js" })
        {
            using var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        using var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    /// <summary>
    /// Rejects a discovered provider whose lifetime could retain request state between documents.
    /// </summary>
    [Fact]
    public void ExampleDiscoveryRejectsSingletonProviders()
    {
        var services = new ServiceCollection();
        services.AddApiInfrastructure("/api/admin/v1", "1.0");
        var assembly = typeof(ClientsController).Assembly;
        var provider = assembly.ExportedTypes.First(type => type.IsClass &&
            typeof(IOpenApiExampleProvider).IsAssignableFrom(type));
        services.AddSingleton(provider);

        Assert.Throws<InvalidOperationException>(() => services.AddOpenApiInfrastructure(registration =>
            registration.ScanExamples(assembly)));
    }

    /// <summary>
    /// Serves SSO forms at clean public URLs and stops exposing their physical page paths.
    /// </summary>
    /// <param name="route">The canonical public page URL.</param>
    /// <param name="oldPath">The former file-based public route.</param>
    /// <returns>The native HTML and route absence assertions.</returns>
    [Theory]
    [InlineData("/login", "/Identity/Account/Login")]
    [InlineData("/forgot-password", "/Identity/Account/ForgotPassword")]
    [InlineData("/verify-password-reset-otp", "/Identity/Account/VerifyPasswordResetOtp")]
    [InlineData("/logout", "/Identity/Account/Logout")]
    public async Task SsoUsesCleanPublicRoutes(string route, string oldPath)
    {
        await using var app = await CreateHostAsync(disableStaticFallback: true);
        using var client = app.GetTestClient();
        using var page = await client.GetAsync(route);
        using var old = await client.GetAsync(oldPath);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("text/html", page.Content.Headers.ContentType!.MediaType);
        Assert.Equal(HttpStatusCode.NotFound, old.StatusCode);
    }

    /// <summary>
    /// Generates navigation from internal page names while publishing only clean routes.
    /// </summary>
    /// <returns>The native link-generation and recovery challenge assertions.</returns>
    [Fact]
    public async Task SsoLinksAndRecoveryChallengeUsePublicRoutes()
    {
        await using var app = await CreateHostAsync();
        using var client = app.GetTestClient();
        var home = await client.GetStringAsync("/");
        var login = await client.GetStringAsync("/login");
        using var reset = await client.GetAsync("/reset-password");

        Assert.Contains("href=\"/login\"", home);
        Assert.Contains("href=\"/logout\"", home);
        Assert.Contains("href=\"/forgot-password", login);
        Assert.DoesNotContain("/Identity/Account/", login);
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.Equal("/forgot-password", reset.Headers.Location!.AbsolutePath);
        Assert.Contains("ReturnUrl=", reset.Headers.Location.Query);
    }

    /// <summary>
    /// Keeps page failures in the safe HTML boundary for native trailing-slash route variants too.
    /// </summary>
    /// <param name="path">The canonical page route or its native slash variant.</param>
    /// <returns>The safe HTML error assertions.</returns>
    [Theory]
    [InlineData("/login")]
    [InlineData("/login/")]
    public async Task SsoRouteFailuresRemainSafeHtml(string path)
    {
        await using var app = await CreateHostAsync(configure: services =>
            services.Configure<MvcOptions>(options => options.Filters.Add(new ThrowingLoginPageFilter())));
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.DoesNotContain("private-page-failure", html);
    }

    /// <summary>
    /// Injects a page failure without invoking credentials or persistence.
    /// </summary>
    private sealed class ThrowingLoginPageFilter : IAsyncPageFilter
    {
        /// <summary>
        /// Leaves handler selection to native Razor.
        /// </summary>
        /// <param name="context">The native selection context.</param>
        /// <returns>The completed selection hook.</returns>
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        /// <summary>
        /// Fails only the login page so the native error page can re-execute normally.
        /// </summary>
        /// <param name="context">The page selected by native routing.</param>
        /// <param name="next">The remaining native handler pipeline.</param>
        /// <returns>The remaining handler task for other pages.</returns>
        public Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next) =>
            context.ActionDescriptor.ViewEnginePath == "/Identity/Account/Login"
                ? throw new InvalidOperationException("private-page-failure")
                : next();
    }

    /// <summary>
    /// Starts the actual API pipeline on TestServer; the unused connection is never presented as persistence evidence.
    /// </summary>
    /// <param name="disableStaticFallback">Whether to test route absence without the development-only static GET catch-all.</param>
    /// <param name="configure">Optional boundary doubles; native authentication and middleware remain unchanged.</param>
    /// <param name="environment">The actual host environment controlling public documentation.</param>
    /// <returns>The started host owned by the test.</returns>
    private static async Task<WebApplication> CreateHostAsync(
        bool disableStaticFallback = false,
        Action<IServiceCollection>? configure = null,
        string environment = "Development")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ClientsController).Assembly.GetName().Name,
            ContentRootPath = AuthenticationTestHostPaths.GetContentRoot(),
            EnvironmentName = environment
        });
        builder.WebHost.UseTestServer();
        if (disableStaticFallback)
        {
            // The native development GET/HEAD catch-all otherwise makes any unknown POST return 405.
            builder.Configuration["DisableStaticAssetNotFoundRuntimeFallback"] = "true";
        }

        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Authentication"] = "Host=localhost;Database=unused_http_test;Username=unused",
            ["Authentication:ClientTokenPepper"] = Convert.ToBase64String(new byte[32]),
            ["Authentication:IntrospectionClientSecret"] = new string('x', 32),
            ["Observability:Logging:MinimumLevel"] = "Fatal"
        });
        if (environment == Environments.Production)
        {
            // Test-only keys let the real production pipeline start without a deployment secret.
            using var signingKey = RSA.Create(2048);
            builder.Configuration["Authentication:SigningKey"] = signingKey.ExportRSAPrivateKeyPem();
            builder.Configuration["Authentication:EncryptionKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            builder.Configuration["Authentication:Issuer"] = "https://localhost/";
        }

        builder.Services.AddLoggingInfrastructure(builder.Configuration, "authentication-http-tests", Environments.Development);
        builder.Services.AddAuthenticationApi(builder.Configuration, builder.Environment);

        configure?.Invoke(builder.Services);

        var app = builder.Build();
        app.UseAuthenticationApi();
        await app.StartAsync();

        // HTTPS is also required by native OpenIddict and the __Host cookie contract under TestServer.
        app.GetTestServer().BaseAddress = new Uri("https://localhost");

        return app;
    }
}
