namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Transformers.OpenApi;

/// <summary>
/// Verifies value examples against real MVC binding and native OpenAPI without changing runtime contracts.
/// </summary>
public sealed class OpenApiValueExamplesTests
{
    /// <summary>
    /// Keeps binding targets in the API-owned enum namespace without changing attribute values.
    /// </summary>
    [Fact]
    public void ValueTargets_BelongToApiEnumsAndKeepTheirBindingValues()
    {
        Assert.Equal("ContextHub.ApiCommon.Enums", typeof(OpenApiValueTarget).Namespace);
        Assert.Equal(0, (int)OpenApiValueTarget.Query);
        Assert.Equal(1, (int)OpenApiValueTarget.Path);
        Assert.Equal(2, (int)OpenApiValueTarget.Header);
        Assert.Equal(3, (int)OpenApiValueTarget.Form);
    }

    /// <summary>
    /// Uses native expanded Minimal API property metadata and leaves unmapped scalars without fake examples.
    /// </summary>
    /// <returns>The Minimal API fixture and absent-provider assertions.</returns>
    [Fact]
    public async Task MinimalQueryValues_UseExpandedPropertyMetadata()
    {
        await using var app = await CreateHost(application =>
        {
            application.MapGet("/api/test/v1/minimal-values", ([AsParameters] ValueQuery request) => request.SearchTerm);
            application.MapGet("/api/test/v1/unmapped-values", ([FromQuery] string? query) => query);
        });
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty("/api/test/v1/minimal-values").GetProperty("get");

        Assert.True(Parameter(operation, "search-term").TryGetProperty("example", out var example), operation.GetRawText());
        Assert.Equal("fixture", example.GetString());
        Assert.False(Parameter(paths.GetProperty("/api/test/v1/unmapped-values").GetProperty("get"), "query")
            .TryGetProperty("example", out _));
    }

    /// <summary>
    /// Uses binding aliases rather than JSON names and keeps field overrides local to one action.
    /// </summary>
    /// <returns>The generated-document and runtime binding assertions.</returns>
    [Fact]
    public async Task QueryValues_RespectAliasesAndLocalOverrides()
    {
        await using var app = await CreateHost();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var normal = paths.GetProperty("/api/test/v1/values/query").GetProperty("get");
        var special = paths.GetProperty("/api/test/v1/values/override").GetProperty("get");

        Assert.Equal("fixture", Parameter(normal, "search-term").GetProperty("example").GetString());
        Assert.Equal("override", Parameter(special, "search-term").GetProperty("example").GetString());
        Assert.Equal(7, Parameter(normal, "page-size").GetProperty("example").GetInt32());
        Assert.False(normal.TryGetProperty("requestBody", out _));

        // Example registration documents values only; model binding continues to use the declared query aliases.
        using var response = await client.GetAsync("/api/test/v1/values/query?search-term=runtime&page-size=3");
        Assert.Equal("runtime:3", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Places form examples on operation-local properties and never adds examples for uploads.
    /// </summary>
    /// <returns>The form schema and component-isolation assertions.</returns>
    [Fact]
    public async Task FormValues_KeepAliasesAndDoNotMutateOtherOperations()
    {
        await using var app = await CreateHost();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var form = paths.GetProperty("/api/test/v1/values/form").GetProperty("post");
        var special = paths.GetProperty("/api/test/v1/values/form-override").GetProperty("post");
        Assert.True(form.GetProperty("requestBody").GetProperty("content").TryGetProperty("multipart/form-data", out _), form.GetRawText());
        var properties = FormProperties(form);

        Assert.Equal("fixture", properties.GetProperty("search-term").GetProperty("examples")[0].GetString());
        Assert.Equal("override", FormProperties(special).GetProperty("search-term").GetProperty("examples")[0].GetString());
        Assert.False(properties.GetProperty("upload").TryGetProperty("examples", out _));
        Assert.Equal("fixture", FormProperties(form).GetProperty("search-term").GetProperty("examples")[0].GetString());

        using var body = new MultipartFormDataContent();
        body.Add(new StringContent("runtime"), "search-term");
        using var response = await client.PostAsync("/api/test/v1/values/form", body);
        Assert.Equal("runtime", await response.Content.ReadAsStringAsync());
    }

    /// <summary>
    /// Applies explicit scalar examples to the actual path/header target without inventing a body.
    /// </summary>
    /// <returns>The explicit target assertions.</returns>
    [Fact]
    public async Task ScalarOverrides_MatchPathAndHeaderBinding()
    {
        await using var app = await CreateHost();
        using var client = app.GetTestClient();
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/test/v1/values/scalar/{key}").GetProperty("get");

        Assert.Equal("override", Parameter(operation, "key").GetProperty("example").GetString());
        Assert.Equal("override", Parameter(operation, "X-Label").GetProperty("example").GetString());
        Assert.False(operation.TryGetProperty("requestBody", out _));
    }

    /// <summary>
    /// Rejects ambiguous value-provider registration rather than selecting the last fixture silently.
    /// </summary>
    [Fact]
    public void DuplicateValues_AreRejected()
    {
        var services = new ServiceCollection();
        services.AddApiInfrastructure("/api/test/v1", "1.0");

        Assert.Throws<InvalidOperationException>(() => services.AddOpenApiInfrastructure(registration =>
            registration.Values<ValueQuery, QueryValues>().Values<ValueQuery, QueryValues>()));
    }

    /// <summary>
    /// Creates an isolated MVC host with explicitly registered synthetic providers.
    /// </summary>
    /// <param name="configure">Optional additional native endpoints owned by the test.</param>
    /// <returns>The started native OpenAPI test host.</returns>
    private static async Task<WebApplication> CreateHost(Action<WebApplication>? configure = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiInfrastructure("/api/test/v1", "1.0");
        builder.Services.AddControllers().AddApplicationPart(typeof(ValueExamplesController).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new ValueControllerFeatureProvider());
            })
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
        builder.Services.AddTransient<OverrideValue>();
        builder.Services.AddOpenApi();
        builder.Services.AddOpenApiInfrastructure(registration => registration.Values<ValueQuery, QueryValues>().Values<ValueForm, FormValues>());
        var app = builder.Build();
        app.MapOpenApi();
        app.MapControllers();
        configure?.Invoke(app);
        await app.StartAsync();

        return app;
    }

    /// <summary>
    /// Finds one explicitly named native parameter without modifying punctuation or casing.
    /// </summary>
    /// <param name="operation">The generated operation.</param>
    /// <param name="name">The binding name on the wire.</param>
    /// <returns>The matching parameter.</returns>
    private static JsonElement Parameter(JsonElement operation, string name) =>
        operation.GetProperty("parameters").EnumerateArray().Single(value => value.GetProperty("name").GetString() == name);

    /// <summary>
    /// Reads native multipart properties without hiding reference or body-generation errors.
    /// </summary>
    /// <param name="operation">The form operation.</param>
    /// <returns>The operation's documented multipart properties.</returns>
    private static JsonElement FormProperties(JsonElement operation) => operation.GetProperty("requestBody")
        .GetProperty("content").GetProperty("multipart/form-data").GetProperty("schema").GetProperty("properties");

    /// <summary>
    /// Isolates controller discovery from unrelated fixtures in the same test assembly.
    /// </summary>
    private sealed class ValueControllerFeatureProvider : ControllerFeatureProvider
    {
        /// <summary>
        /// Selects only the controller owned by these value-example tests.
        /// </summary>
        /// <param name="typeInfo">The candidate controller type.</param>
        /// <returns>Whether this test host owns the controller.</returns>
        protected override bool IsController(TypeInfo typeInfo) => typeInfo.AsType() == typeof(ValueExamplesController);
    }
}

/// <summary>
/// Exposes binding fixtures only inside selected HTTP test hosts.
/// </summary>
[ApiController]
[Route("api/test/v1/values")]
public sealed class ValueExamplesController : ControllerBase
{
    /// <summary>
    /// Echoes query-bound values independently of their JSON fixture names.
    /// </summary>
    /// <param name="request">The query fixture.</param>
    /// <returns>The actually bound values.</returns>
    [HttpGet("query")]
    public string Query([FromQuery] ValueQuery request) => $"{request.SearchTerm}:{request.PageSize}";

    /// <summary>
    /// Overrides one query field without changing the reusable DTO's default example.
    /// </summary>
    /// <param name="request">The query fixture.</param>
    /// <returns>The actually bound search term.</returns>
    [HttpGet("override")]
    [OpenApiValueExample<OverrideValue>(OpenApiValueTarget.Query, "search-term")]
    public string Override([FromQuery] ValueQuery request) => request.SearchTerm;

    /// <summary>
    /// Echoes a form field while leaving optional uploads without synthetic data.
    /// </summary>
    /// <param name="request">The multipart fixture.</param>
    /// <returns>The actually bound field.</returns>
    [HttpPost("form")]
    [Consumes("multipart/form-data")]
    public string Form([FromForm] ValueForm request) => request.SearchTerm;

    /// <summary>
    /// Overrides one form operation, not shared component schemas.
    /// </summary>
    /// <param name="request">The multipart fixture.</param>
    /// <returns>The actually bound field.</returns>
    [HttpPost("form-override")]
    [Consumes("multipart/form-data")]
    [OpenApiValueExample<OverrideValue>(OpenApiValueTarget.Form, "search-term")]
    public string FormOverride([FromForm] ValueForm request) => request.SearchTerm;

    /// <summary>
    /// Declares unambiguous scalar path and header examples.
    /// </summary>
    /// <param name="key">The route key.</param>
    /// <param name="label">The optional bound header.</param>
    /// <returns>The route key.</returns>
    [HttpGet("scalar/{key}")]
    [OpenApiValueExample<OverrideValue>(OpenApiValueTarget.Path, "key")]
    [OpenApiValueExample<OverrideValue>(OpenApiValueTarget.Header, "X-Label")]
    public string Scalar([FromRoute] string key, [FromHeader(Name = "X-Label")] string? label) => key;
}

/// <summary>
/// Carries query aliases distinct from CLR and JSON names.
/// </summary>
public sealed class ValueQuery
{
    /// <summary>
    /// Gets or sets the search field using its explicit query alias.
    /// </summary>
    [FromQuery(Name = "search-term")]
    public string SearchTerm { get; set; } = "";

    /// <summary>
    /// Gets or sets the native numeric query field.
    /// </summary>
    [FromQuery(Name = "page-size")]
    public int PageSize { get; set; }
}

/// <summary>
/// Carries a multipart alias and an upload that must never receive an example.
/// </summary>
public sealed class ValueForm
{
    /// <summary>
    /// Gets or sets the form field's explicit binding alias.
    /// </summary>
    [FromForm(Name = "search-term")]
    public string SearchTerm { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional upload without synthetic content.
    /// </summary>
    [FromForm(Name = "upload")]
    public IFormFile? Upload { get; set; }
}

/// <summary>
/// Supplies synthetic query values through the existing typed provider contract.
/// </summary>
public sealed class QueryValues : IOpenApiExampleProvider<ValueQuery>
{
    /// <summary>
    /// Creates values whose serialized names differ from binding aliases.
    /// </summary>
    /// <returns>The synthetic query fixture.</returns>
    public ValueQuery CreateExample() => new() { SearchTerm = "fixture", PageSize = 7 };
}

/// <summary>
/// Supplies form values without creating a fake file.
/// </summary>
public sealed class FormValues : IOpenApiExampleProvider<ValueForm>
{
    /// <summary>
    /// Creates the synthetic form fixture.
    /// </summary>
    /// <returns>The synthetic form values.</returns>
    public ValueForm CreateExample() => new() { SearchTerm = "fixture" };
}

/// <summary>
/// Supplies a scalar override through the existing non-generic provider contract.
/// </summary>
public sealed class OverrideValue : IOpenApiExampleProvider
{
    /// <summary>
    /// Creates a fixed documentation-only field value.
    /// </summary>
    /// <returns>The synthetic field value.</returns>
    public object GetExample() => "override";
}
