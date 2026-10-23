namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.DependencyInjection;

/// <summary>
/// Checks MVC matching, link generation, binding and generated OpenAPI against real isolated HTTP test hosts.
/// </summary>
public sealed class RoutingRegistrationTests
{
    /// <summary>
    /// Transforms controller/action tokens only in hosts that explicitly register the convention.
    /// </summary>
    /// <param name="enabled">Whether this host opts into kebab-case tokens.</param>
    /// <param name="path">The expected concrete route prefix.</param>
    /// <returns>A task completing after HTTP and OpenAPI assertions.</returns>
    [Theory]
    [InlineData(true, "/api/package-review/review-package")]
    [InlineData(false, "/api/PackageReview/ReviewPackage")]
    public async Task Routing_OptInMatchesBindingLinksAndOpenApi(bool enabled, string path)
    {
        await using var app = await CreateHost(enabled);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync(path + "/APIKey42");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("APIKey42", payload.RootElement.GetProperty("packageId").GetString());
        Assert.False(payload.RootElement.GetProperty("hasUnexpectedAlias").GetBoolean());
        Assert.EndsWith(path + "/APIKey42", payload.RootElement.GetProperty("link").GetString());

        // The wire path has one native binding name, not an extra query parameter or fabricated RouteData alias.
        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        var operation = paths.GetProperty(path + "/{package-id}").GetProperty("get");
        var parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.Equal("package-id", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
        Assert.True(parameter.GetProperty("required").GetBoolean());

        var otherPath = enabled ? "/api/PackageReview/ReviewPackage" : "/api/package-review/review-package";
        Assert.False(paths.TryGetProperty(otherPath + "/{package-id}", out _));
        using var missing = await client.GetAsync(otherPath + "/APIKey42");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    /// <summary>
    /// Leaves explicit literal paths and their public parameter names unchanged in either host configuration.
    /// </summary>
    /// <param name="enabled">Whether token transformation is registered.</param>
    /// <returns>A task completing after runtime and document checks.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Routing_PreservesLiteralRoutesAndParameterNames(bool enabled)
    {
        await using var app = await CreateHost(enabled);
        using var client = app.GetTestClient();
        using var response = await client.GetAsync("/api/FixedPath/UnchangedID");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("UnchangedID", payload.RootElement.GetProperty("packageId").GetString());

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var operation = document.RootElement.GetProperty("paths").GetProperty("/api/FixedPath/{packageId}").GetProperty("get");
        var parameter = Assert.Single(operation.GetProperty("parameters").EnumerateArray());
        Assert.Equal("packageId", parameter.GetProperty("name").GetString());
        Assert.Equal("path", parameter.GetProperty("in").GetString());
    }

    /// <summary>
    /// Starts an isolated MVC host so unrelated controller example providers cannot affect document generation.
    /// </summary>
    /// <param name="enabled">Whether to add the shared routing convention.</param>
    /// <returns>The started host, owned and disposed by the calling test.</returns>
    private static async Task<WebApplication> CreateHost(bool enabled)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        var mvc = builder.Services.AddControllers().AddApplicationPart(typeof(PackageReviewController).Assembly)
            .ConfigureApplicationPartManager(manager =>
            {
                // Discover only this fixture; each test host still uses MVC's real routing and model binding.
                manager.FeatureProviders.Clear();
                manager.FeatureProviders.Add(new RoutingControllerFeatureProvider());
            });

        if (enabled)
        {
            mvc.AddKebabCaseRouting();
        }

        builder.Services.AddApiInfrastructure("/api", "1.0");
        builder.Services.AddOpenApiInfrastructure(_ => { });

        var app = builder.Build();
        app.MapControllers();
        app.MapOpenApi();
        await app.StartAsync();

        return app;
    }

    /// <summary>
    /// Restricts controller discovery to the routing fixture without replacing MVC processing.
    /// </summary>
    private sealed class RoutingControllerFeatureProvider : ControllerFeatureProvider
    {
        /// <summary>
        /// Selects only the synthetic controller used by these tests.
        /// </summary>
        /// <param name="typeInfo">The controller candidate found in the application part.</param>
        /// <returns>True only for the routing fixture controller.</returns>
        protected override bool IsController(TypeInfo typeInfo) => typeInfo.AsType() == typeof(PackageReviewController);
    }
}

/// <summary>
/// Supplies controller/action tokens and an explicit route without any database or authentication dependency.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class PackageReviewController : ControllerBase
{
    /// <summary>
    /// Echoes the native route binding and link without inserting a camel-case alias into RouteData.
    /// </summary>
    /// <param name="packageId">The value bound to the public hyphenated route parameter.</param>
    /// <returns>The fixture value and generated link.</returns>
    [HttpGet("[action]/{package-id}", Name = "package-review-token-route")]
    public ActionResult<RoutingSnapshot> ReviewPackage([FromRoute(Name = "package-id")] string packageId) =>
        Ok(new RoutingSnapshot(packageId, RouteData.Values.ContainsKey("packageId"),
            Url.Link("package-review-token-route", new Dictionary<string, object?> { ["package-id"] = packageId })));

    /// <summary>
    /// Echoes an explicit route whose literal text and parameter spelling are intentionally not kebab-case.
    /// </summary>
    /// <param name="packageId">The value bound by its unchanged public parameter name.</param>
    /// <returns>The unchanged fixture value.</returns>
    [HttpGet("/api/FixedPath/{packageId}")]
    public ActionResult<RoutingSnapshot> ReadLiteral([FromRoute] string packageId) =>
        Ok(new RoutingSnapshot(packageId, false, null));
}

/// <summary>
/// Exposes only synthetic route results needed to check binding and link generation.
/// </summary>
/// <param name="PackageId">The bound value, whose casing must remain unchanged.</param>
/// <param name="HasUnexpectedAlias">Whether a non-native camel-case RouteData alias was introduced.</param>
/// <param name="Link">The link generated from the named token route, if requested.</param>
public sealed record RoutingSnapshot(string PackageId, bool HasUnexpectedAlias, string? Link);
