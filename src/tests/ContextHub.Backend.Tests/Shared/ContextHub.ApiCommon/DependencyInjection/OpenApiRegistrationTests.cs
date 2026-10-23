namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.DependencyInjection;

/// <summary>
/// Verifies type-based enrichment against generated documents and real responses rather than mocked metadata.
/// </summary>
public sealed class OpenApiRegistrationTests
{
    /// <summary>
    /// Requires registered fixtures to enrich a native typed endpoint without any example attributes.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task TypedRegistration_AppliesExamplesAndPreservesRuntime()
    {
        await using var app = await CreateHost();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/typed").GetProperty("post");

        Assert.Equal("fixture", operation.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").GetProperty("example").GetProperty("display_name").GetString());
        Assert.Equal("fixture", operation.GetProperty("responses").GetProperty("200").GetProperty("content")
            .GetProperty("application/json").GetProperty("examples").GetProperty("default").GetProperty("value")
            .GetProperty("display_name").GetString());
        using var runtime = await client.PostAsync("/api/admin/v1/typed",
            new StringContent("{\"display_name\":\"runtime\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, runtime.StatusCode);
        using var payload = JsonDocument.Parse(await runtime.Content.ReadAsStringAsync());
        Assert.Equal("runtime", payload.RootElement.GetProperty("display_name").GetString());
    }

    /// <summary>
    /// Rejects duplicate type mappings immediately at the composition root.
    /// </summary>
    [Fact]
    public void DuplicateMapping_RejectsRegistration()
    {
        var services = new ServiceCollection();
        services.AddApiInfrastructure("/api/admin/v1", "1.0");
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddOpenApiInfrastructure(registration =>
            registration.Request<Payload, PayloadExample>().Request<Payload, PayloadExample>()));

        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING, exception.Message);
    }

    /// <summary>
    /// Verifies pair status registration and the isolated override for a reused DTO pair.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task OperationContract_ChangesInferredSuccessAndKeepsOverrideIsolated()
    {
        await using var app = await CreateHost(registration => registration.Operation<Payload, Payload>(201, 404, 409)
            .Response<Payload, PayloadExample>(201), mvc: true);
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var responses = paths.GetProperty("/api/admin/v1/contracts/create").GetProperty("post").GetProperty("responses");
        Assert.False(responses.TryGetProperty("200", out _));
        Assert.True(responses.GetProperty("201").GetProperty("content").GetProperty("application/json").TryGetProperty("examples", out _));
        Assert.True(responses.TryGetProperty("404", out _));
        Assert.Equal("error_conflict", responses.GetProperty("409").GetProperty("content").GetProperty("application/json")
            .GetProperty("example").GetProperty("error").GetProperty("code").GetString());
        var overridden = paths.GetProperty("/api/admin/v1/contracts/override").GetProperty("post").GetProperty("responses");
        Assert.True(overridden.TryGetProperty("200", out _));
        Assert.False(overridden.TryGetProperty("404", out _));
        Assert.False(overridden.TryGetProperty("409", out _));
        using var runtime = await client.PostAsync("/api/admin/v1/contracts/create",
            new StringContent("{\"displayName\":\"runtime\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Created, runtime.StatusCode);
    }

    /// <summary>
    /// Preserves bodyless success metadata and never resolves a response fixture for 204.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task NativeNoContent_Preserves204WithoutExamples()
    {
        await using var app = await CreateHost(configure: host => host.MapDelete("/api/admin/v1/no-content", () => TypedResults.NoContent()));
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var responses = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/no-content")
            .GetProperty("delete").GetProperty("responses");
        Assert.False(responses.TryGetProperty("200", out _));
        Assert.False(responses.GetProperty("204").TryGetProperty("content", out _));
        using var runtime = await client.DeleteAsync("/api/admin/v1/no-content");
        Assert.Equal(HttpStatusCode.NoContent, runtime.StatusCode);
        Assert.Equal("", await runtime.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Leaves unregistered schemas intact and does not turn query or route values into JSON request bodies.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task MissingProviderAndQueryParameter_DoNotInventExamplesOrBody()
    {
        await using var app = await CreateHost(configure: host =>
        {
            host.MapPost("/api/admin/v1/unmapped", (OtherPayload request) => TypedResults.Ok(request));
            host.MapGet("/api/admin/v1/query/{id}", (int id, string? name) => TypedResults.Ok(new Payload(name ?? id.ToString())));
        });
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var media = paths.GetProperty("/api/admin/v1/unmapped").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json");
        Assert.True(media.TryGetProperty("schema", out _));
        Assert.False(media.TryGetProperty("example", out _));
        Assert.False(paths.GetProperty("/api/admin/v1/query/{id}").GetProperty("get").TryGetProperty("requestBody", out _));
    }

    /// <summary>
    /// Guards protocol and prefix-lookalike routes even when their types have registered fixtures.
    /// </summary>
    /// <param name="path">The non-Admin endpoint.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("/mcp")]
    [InlineData("/connect/token")]
    [InlineData("/health")]
    [InlineData("/api/admin/v10/typed")]
    public async Task OtherBoundaries_DoNotReceiveSharedConventions(string path)
    {
        await using var app = await CreateHost(configure: host => host.MapPost(path, (Payload request) => TypedResults.Ok(request)), boundary: true);
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty(path).GetProperty("post");
        Assert.False(operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").TryGetProperty("example", out _));
        Assert.False(operation.GetProperty("responses").TryGetProperty("500", out _));
    }

    /// <summary>
    /// Verifies that standard examples match runtime adapter status and public error codes.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ErrorBoundary_DocumentsBindingAndExceptionErrorsOnlyWhenDeclared()
    {
        await using var app = await CreateHost(configure: host =>
            host.MapGet("/api/admin/v1/failure", () => ThrowFailure()), boundary: true);
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var responses = paths.GetProperty("/api/admin/v1/typed").GetProperty("post").GetProperty("responses");
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.False(responses.TryGetProperty("404", out _));
        Assert.False(responses.TryGetProperty("409", out _));
        using var failure = await client.GetAsync("/api/admin/v1/failure");
        Assert.Equal(HttpStatusCode.InternalServerError, failure.StatusCode);
        using var runtime = JsonDocument.Parse(await failure.Content.ReadAsStringAsync());
        var example = paths.GetProperty("/api/admin/v1/failure").GetProperty("get").GetProperty("responses")
            .GetProperty("500").GetProperty("content").GetProperty("application/json").GetProperty("example");
        Assert.Equal(example.GetProperty("error").GetProperty("code").GetString(), runtime.RootElement.GetProperty("error").GetProperty("code").GetString());
        using var malformed = await client.PostAsync("/api/admin/v1/typed", new StringContent("invalid-json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
    }

    /// <summary>
    /// Throws a synthetic failure to exercise the real shared exception adapter.
    /// </summary>
    /// <returns>No result because the fixture always fails.</returns>
    private static Payload ThrowFailure() => throw new InvalidOperationException("synthetic-private-error");

    /// <summary>
    /// Confirms effective fallback/named policies and anonymous bypass against actual challenge and forbid responses.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task AuthorizationAndRateLimits_MatchDocumentedRuntimeOutcomes()
    {
        await using var app = await CreateHost(configure: host =>
        {
            host.MapGet("/api/admin/v1/protected", () => TypedResults.Ok(new Payload("protected"))).RequireAuthorization("owner");
            host.MapGet("/api/admin/v1/anonymous", () => TypedResults.Ok(new Payload("anonymous"))).AllowAnonymous();
            host.MapGet("/api/admin/v1/limited", () => TypedResults.Ok(new Payload("limited"))).AllowAnonymous().RequireRateLimiting("fixture");
            host.MapGet("/api/admin/v1/unlimited", () => TypedResults.Ok(new Payload("unlimited"))).AllowAnonymous().DisableRateLimiting();
        }, boundary: true, policies: true, services: services =>
        {
            services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthenticationHandler>("fixture", _ => { });
            services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
                options.AddPolicy("owner", policy => policy.RequireAuthenticatedUser().RequireClaim("role", "owner"));
            });
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = 429;
                options.AddFixedWindowLimiter("fixture", limiter =>
                {
                    limiter.PermitLimit = 1;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                });
            });
        });
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var protectedResponses = paths.GetProperty("/api/admin/v1/protected").GetProperty("get").GetProperty("responses");
        Assert.True(protectedResponses.TryGetProperty("401", out _));
        Assert.True(protectedResponses.TryGetProperty("403", out _));
        Assert.True(paths.GetProperty("/api/admin/v1/typed").GetProperty("post").GetProperty("responses").TryGetProperty("401", out _));
        var anonymous = paths.GetProperty("/api/admin/v1/anonymous").GetProperty("get").GetProperty("responses");
        Assert.False(anonymous.TryGetProperty("401", out _));
        Assert.False(anonymous.TryGetProperty("403", out _));
        Assert.True(paths.GetProperty("/api/admin/v1/limited").GetProperty("get").GetProperty("responses").TryGetProperty("429", out _));
        Assert.False(paths.GetProperty("/api/admin/v1/unlimited").GetProperty("get").GetProperty("responses").TryGetProperty("429", out _));
        using var challenge = await client.GetAsync("/api/admin/v1/protected");
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        client.DefaultRequestHeaders.Add("X-Fixture-Authenticated", "true");
        using var forbid = await client.GetAsync("/api/admin/v1/protected");
        Assert.Equal(HttpStatusCode.Forbidden, forbid.StatusCode);
        using var first = await client.GetAsync("/api/admin/v1/limited");
        using var second = await client.GetAsync("/api/admin/v1/limited");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    /// <summary>
    /// Keeps explicit error payload schemas and typed creation success metadata authoritative.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task ExplicitResponseMetadata_PreservesWireTypeAndStatus()
    {
        await using var app = await CreateHost(registration => registration.Operation<Payload, Payload>(200, 409),
            configure: host =>
            {
                host.MapPost("/api/admin/v1/created", (Payload request) => TypedResults.Created("/fixture", request));
                host.MapPost("/api/admin/v1/custom-conflict", (Payload request) => Results.Conflict(new OtherPayload(3)))
                    .Produces<Payload>(200).Produces<OtherPayload>(409);
            }, boundary: true);
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var created = paths.GetProperty("/api/admin/v1/created").GetProperty("post").GetProperty("responses");
        Assert.True(created.TryGetProperty("201", out _));
        Assert.False(created.TryGetProperty("200", out _));
        var conflict = paths.GetProperty("/api/admin/v1/custom-conflict").GetProperty("post").GetProperty("responses").GetProperty("409");
        Assert.False(conflict.GetProperty("content").GetProperty("application/json").TryGetProperty("example", out _));
        Assert.Contains("OtherPayload", conflict.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetRawText());
        using var runtime = await client.PostAsync("/api/admin/v1/custom-conflict",
            new StringContent("{\"display_name\":\"runtime\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Conflict, runtime.StatusCode);
        Assert.Contains("\"count\":3", await runtime.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Applies response fixtures to the envelope rather than accidentally documenting only its nested data type.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task WireEnvelopeAndNamedVariants_UseExactTypes()
    {
        await using var app = await CreateHost(registration => registration
            .Response<Payload, AlternativeExample>(200, "alternative")
            .Response<ApiResponse<Payload>, EnvelopeExample>(), configure: host =>
                host.MapGet("/api/admin/v1/envelope", () => TypedResults.Ok(new ApiResponse<Payload>(new Payload("runtime")))));
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var variants = paths.GetProperty("/api/admin/v1/typed").GetProperty("post").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("examples");
        Assert.Equal("fixture", variants.GetProperty("default").GetProperty("value").GetProperty("display_name").GetString());
        Assert.Equal("alternative", variants.GetProperty("alternative").GetProperty("value").GetProperty("display_name").GetString());
        var envelope = paths.GetProperty("/api/admin/v1/envelope").GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("content").GetProperty("application/json").GetProperty("examples").GetProperty("default").GetProperty("value");
        Assert.True(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("fixture", envelope.GetProperty("data").GetProperty("display_name").GetString());
    }

    /// <summary>
    /// Rejects an attribute override that supplies a payload incompatible with the native body type.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task WrongTypedAttribute_RejectsDocumentGeneration()
    {
        await using var app = await CreateHost(configure: host =>
            host.MapPost("/api/admin/v1/wrong-type", (Payload request) => TypedResults.Ok(request))
                .WithMetadata(new OpenApiRequestExampleAttribute<WrongExample>()),
            services: services => services.AddTransient<WrongExample>());
        using var client = app.GetTestClient();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStringAsync("/openapi/v1.json"));
        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.INVALID_EXAMPLE_TYPE, exception.Message);
    }

    /// <summary>
    /// Rejects invalid operation ranges, duplicate names and non-transient providers at registration time.
    /// </summary>
    [Fact]
    public void InvalidConfiguration_RejectsBeforeDocumentGeneration()
    {
        // Each case needs its own valid boundary so the intended contract guard, not missing setup, is exercised.
        var operationServices = new ServiceCollection();
        operationServices.AddApiInfrastructure("/api/admin/v1", "1.0");
        var operationError = Assert.Throws<InvalidOperationException>(() => operationServices
            .AddOpenApiInfrastructure(mapping => mapping.Operation<Payload, Payload>(400)));
        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT, operationError.Message);

        var bodylessServices = new ServiceCollection();
        bodylessServices.AddApiInfrastructure("/api/admin/v1", "1.0");
        var bodylessError = Assert.Throws<InvalidOperationException>(() => bodylessServices
            .AddOpenApiInfrastructure(mapping => mapping.Response<Payload, PayloadExample>(204)));
        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT, bodylessError.Message);

        var duplicateServices = new ServiceCollection();
        duplicateServices.AddApiInfrastructure("/api/admin/v1", "1.0");
        var duplicateError = Assert.Throws<InvalidOperationException>(() => duplicateServices
            .AddOpenApiInfrastructure(mapping => mapping.Response<Payload, PayloadExample>().Response<Payload, AlternativeExample>()));
        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.DUPLICATE_MAPPING, duplicateError.Message);

        var services = new ServiceCollection();
        services.AddApiInfrastructure("/api/admin/v1", "1.0");
        services.AddSingleton<PayloadExample>();
        var exception = Assert.Throws<InvalidOperationException>(() => services.AddOpenApiInfrastructure(mapping => mapping.Request<Payload, PayloadExample>()));
        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.INVALID_PROVIDER_LIFETIME, exception.Message);
    }

    /// <summary>
    /// Rejects contradictory native schemas and endpoint overrides instead of silently choosing one contract.
    /// </summary>
    /// <param name="conflictingSchema">Whether to conflict by payload type rather than success status.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContradictoryMetadata_RejectsGeneration(bool conflictingSchema)
    {
        await using var app = await CreateHost(registration => registration.Operation<Payload, Payload>(), configure: host =>
        {
            var endpoint = host.MapPost("/api/admin/v1/contradictory", (Payload request) => TypedResults.Ok(request));
            if (conflictingSchema)
            {
                endpoint.Produces<OtherPayload>(200);
            }
            else
            {
                endpoint.WithMetadata(new OpenApiOperationAttribute(201));
            }
        });
        using var client = app.GetTestClient();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetStringAsync("/openapi/v1.json"));
        Assert.Equal(ApiCommonErrorConstants.OpenApiErrors.INVALID_CONTRACT, exception.Message);
    }

    /// <summary>
    /// Applies a bodyless 204 pair override without adding a schema or fixture from the declared return DTO.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task BodylessPairOverride_DoesNotReuseInferredBody()
    {
        await using var app = await CreateHost(registration => registration.Operation<Payload, Payload>(201, 409), mvc: true);
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var responses = document.RootElement.GetProperty("paths").GetProperty("/api/admin/v1/contracts/no-content")
            .GetProperty("post").GetProperty("responses");
        Assert.False(responses.TryGetProperty("200", out _));
        Assert.False(responses.TryGetProperty("409", out _));
        Assert.False(responses.GetProperty("204").TryGetProperty("content", out _));
        using var response = await client.PostAsync("/api/admin/v1/contracts/no-content",
            new StringContent("{\"displayName\":\"runtime\"}", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Builds a real HTTP host with a naming policy different from the default JSON convention.
    /// </summary>
    /// <param name="registration">Additional pair or example mappings.</param>
    /// <param name="configure">Additional fixture endpoints.</param>
    /// <param name="mvc">Whether to include controller fixtures.</param>
    /// <param name="boundary">Whether the host installs and declares the shared Admin error pipeline.</param>
    /// <param name="services">Additional native host services.</param>
    /// <param name="policies">Whether the fixture installs authentication, authorization and rate-limit middleware.</param>
    /// <returns>The started native OpenAPI test host.</returns>
    private static async Task<WebApplication> CreateHost(Action<OpenApiRegistration>? registration = null,
        Action<WebApplication>? configure = null, bool mvc = false, bool boundary = false, Action<IServiceCollection>? services = null,
        bool policies = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        if (mvc)
        {
            builder.Services.AddControllers().AddApplicationPart(typeof(OpenApiContractController).Assembly);
            builder.Services.AddTransient<OpenApiExamplesTests.FirstExample>();
            builder.Services.AddTransient<OpenApiExamplesTests.SecondExample>();
        }

        builder.Services.AddApiInfrastructure("/api/admin/v1", "1.0");
        services?.Invoke(builder.Services);
        builder.Services.AddOpenApiInfrastructure(mappings =>
        {
            mappings.Request<Payload, PayloadExample>().Response<Payload, PayloadExample>();
            if (boundary)
            {
                mappings.ApiErrorResponses();
            }

            registration?.Invoke(mappings);
        });
        var app = builder.Build();
        if (boundary)
        {
            app.UseCorrelation();
            app.UseExceptionHandler(new Microsoft.AspNetCore.Builder.ExceptionHandlerOptions
            {
                ExceptionHandler = _ => Task.CompletedTask
            });
            app.UseApiStatusCodes();
        }

        if (policies)
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseRateLimiter();
        }

        app.MapOpenApi().AllowAnonymous();
        if (mvc)
        {
            app.MapControllers();
        }
        app.MapPost("/api/admin/v1/typed", (Payload request) => TypedResults.Ok(request));
        configure?.Invoke(app);
        await app.StartAsync();
        return app;
    }

    /// <summary>
    /// Models a synthetic request and response independent of any application database.
    /// </summary>
    /// <param name="DisplayName">The fixture's safe name.</param>
    public sealed record Payload(string DisplayName);

    /// <summary>
    /// Models a payload that has no fixture registration.
    /// </summary>
    /// <param name="Count">A synthetic count.</param>
    public sealed record OtherPayload(int Count);

    /// <summary>
    /// Creates a typed fixture once per document-generation activation.
    /// </summary>
    public sealed class PayloadExample : IOpenApiExampleProvider<Payload>
    {
        /// <summary>
        /// Returns a deterministic payload without reading request data.
        /// </summary>
        /// <returns>The safe documentation fixture.</returns>
        public Payload CreateExample() => new("fixture");
    }

    /// <summary>
    /// Supplies a distinct named fixture of the same wire type.
    /// </summary>
    public sealed class AlternativeExample : IOpenApiExampleProvider<Payload>
    {
        /// <summary>
        /// Creates the alternative payload without merging it into another example.
        /// </summary>
        /// <returns>The alternative fixture.</returns>
        public Payload CreateExample() => new("alternative");
    }

    /// <summary>
    /// Supplies an intentionally different DTO for the type-mismatch negative test.
    /// </summary>
    public sealed class WrongExample : IOpenApiExampleProvider<OtherPayload>
    {
        /// <summary>
        /// Creates the DTO that must not be attached to a Payload schema.
        /// </summary>
        /// <returns>The incompatible fixture.</returns>
        public OtherPayload CreateExample() => new(3);
    }

    /// <summary>
    /// Supplies the complete wire envelope rather than only its data member.
    /// </summary>
    public sealed class EnvelopeExample : IOpenApiExampleProvider<ApiResponse<Payload>>
    {
        /// <summary>
        /// Creates a successful synthetic envelope.
        /// </summary>
        /// <returns>The full wire fixture.</returns>
        public ApiResponse<Payload> CreateExample() => new(new Payload("fixture"));
    }

    /// <summary>
    /// Authenticates only a fixture header so challenge and forbid can be exercised without a real issuer.
    /// </summary>
    /// <param name="options">Native authentication options.</param>
    /// <param name="logger">Native authentication diagnostics.</param>
    /// <param name="encoder">Native header-safe encoder.</param>
    public sealed class FixtureAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        /// <summary>
        /// Returns either no identity or a synthetic identity lacking the Owner requirement.
        /// </summary>
        /// <returns>The fixture authentication result.</returns>
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.ContainsKey("X-Fixture-Authenticated")
                ? AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim("role", "reader")], Scheme.Name)), Scheme.Name))
                : AuthenticateResult.NoResult());
    }
}

/// <summary>
/// Exercises native ActionResult inference and isolated pair overrides without per-action fixture attributes.
/// </summary>
[ApiController]
[Route("api/admin/v1/contracts")]
public sealed class OpenApiContractController : ControllerBase
{
    /// <summary>
    /// Returns 201 while leaving the response DTO available for native schema inference.
    /// </summary>
    /// <param name="request">The synthetic body.</param>
    /// <returns>The created fixture.</returns>
    [HttpPost("create")]
    public ActionResult<OpenApiRegistrationTests.Payload> Create(OpenApiRegistrationTests.Payload request) => StatusCode(201, request);

    /// <summary>
    /// Reuses the same types with a distinct endpoint contract and native success-schema override.
    /// </summary>
    /// <param name="request">The synthetic body.</param>
    /// <returns>The echoed value.</returns>
    [HttpPost("override")]
    [OpenApiOperation(200)]
    [ProducesResponseType(typeof(OpenApiRegistrationTests.Payload), 200)]
    public ActionResult<OpenApiRegistrationTests.Payload> Override(OpenApiRegistrationTests.Payload request) => Ok(request);

    /// <summary>
    /// Keeps a typed action signature while explicitly declaring its bodyless runtime success.
    /// </summary>
    /// <param name="request">The synthetic body.</param>
    /// <returns>No content after accepting the fixture.</returns>
    [HttpPost("no-content")]
    [OpenApiOperation(204)]
    public ActionResult<OpenApiRegistrationTests.Payload> NoContentOverride(OpenApiRegistrationTests.Payload request) => NoContent();
}
