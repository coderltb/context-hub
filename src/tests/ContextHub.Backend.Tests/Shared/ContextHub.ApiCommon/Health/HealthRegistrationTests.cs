namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Health;

/// <summary>
/// Verifies liveness isolation, readiness evidence and privacy through an actual HTTP test pipeline.
/// </summary>
public sealed class HealthRegistrationTests
{
    /// <summary>
    /// Does not accept self-only evidence or a degraded dependency as successful readiness.
    /// </summary>
    /// <param name="selfOnly">Selects the process-only report instead of a degraded dependency report.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Readiness_SelfOnlyOrDegradedIsUnavailable(bool selfOnly)
    {
        var entry = new HealthReportEntry(selfOnly ? HealthStatus.Healthy : HealthStatus.Degraded,
            "private-description", TimeSpan.Zero, null, new Dictionary<string, object> { ["secret"] = "private" });
        var report = new HealthReport(new Dictionary<string, HealthReportEntry> { [selfOnly ? "self" : "dependency"] = entry }, TimeSpan.Zero);
        var context = new DefaultHttpContext();
        await using var body = new MemoryStream();
        context.Response.Body = body;

        await HealthResponseWriter.WriteReadinessAsync(context, report);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        using var json = JsonDocument.Parse(body.ToArray());
        Assert.Equal("Unhealthy", json.RootElement.GetProperty("status").GetString());
        Assert.DoesNotContain("private", Encoding.UTF8.GetString(body.ToArray()), StringComparison.Ordinal);
    }

    /// <summary>
    /// Keeps the health wire contract independent of host-specific application JSON naming.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Health_UsesItsOwnStableJsonProjection()
    {
        using var host = await CreateHostAsync(withReadinessProbe: true, healthy: true, customJson: true);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/health/ready");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        Assert.True(Assert.Single(document.RootElement.GetProperty("checks").EnumerateArray()).TryGetProperty("durationMs", out _));
    }

    /// <summary>
    /// Keeps failed dependencies out of liveness and excludes diagnostic secrets from readiness JSON.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Health_SeparatesLivenessFromFailedReadiness()
    {
        using var host = await CreateHostAsync(withReadinessProbe: true);
        using var client = host.GetTestClient();

        using var live = await client.GetAsync("/health/live");
        using var ready = await client.GetAsync("/health/ready");
        var liveJson = await live.Content.ReadAsStringAsync();
        var readyJson = await ready.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("{\"status\":\"Healthy\"}", liveJson);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        using var report = JsonDocument.Parse(readyJson);
        Assert.Equal("Unhealthy", report.RootElement.GetProperty("status").GetString());
        var check = Assert.Single(report.RootElement.GetProperty("checks").EnumerateArray());
        Assert.Equal("required-dependency", check.GetProperty("name").GetString());
        Assert.Equal("Unhealthy", check.GetProperty("status").GetString());
        Assert.True(check.GetProperty("durationMs").GetDouble() >= 0);
        Assert.DoesNotContain("private", readyJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("exception", readyJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("meta", readyJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("application/json", ready.Content.Headers.ContentType!.MediaType);
    }

    /// <summary>
    /// Does not mistake an empty probe set or self-check for proven dependency readiness.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Readiness_WithoutRequiredProbeIsUnavailable()
    {
        using var host = await CreateHostAsync(withReadinessProbe: false);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Unhealthy", document.RootElement.GetProperty("status").GetString());
        Assert.Empty(document.RootElement.GetProperty("checks").EnumerateArray());
    }

    /// <summary>
    /// Reports successful probes without an application envelope or diagnostic data.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Readiness_WithHealthyProbeReturnsHealthy()
    {
        using var host = await CreateHostAsync(withReadinessProbe: true, healthy: true);
        using var client = host.GetTestClient();

        using var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        Assert.Single(document.RootElement.GetProperty("checks").EnumerateArray());
    }

    /// <summary>
    /// Maps only test endpoints; real API hosts remain unchanged until service-owned probes are implemented.
    /// </summary>
    /// <param name="withReadinessProbe">Whether the fixture registers a dependency readiness probe.</param>
    /// <param name="healthy">Whether the synthetic dependency reports a healthy result.</param>
    /// <param name="customJson">Whether the fixture installs non-default HTTP JSON options.</param>
    /// <returns>The started test host; the caller owns its shutdown and disposal.</returns>
    private static Task<IHost> CreateHostAsync(bool withReadinessProbe, bool healthy = false, bool customJson = false) => new HostBuilder().ConfigureWebHost(web => web.UseTestServer()
        .ConfigureServices(services =>
        {
            services.AddRouting();
            if (customJson)
            {
                services.ConfigureHttpJsonOptions(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.KebabCaseUpper);
            }

            var health = services.AddHealthInfrastructure();
            if (withReadinessProbe)
            {
                health.AddCheck("required-dependency", () => healthy ? HealthCheckResult.Healthy() :
                    HealthCheckResult.Unhealthy("private-connection-string", new InvalidOperationException("private-token"),
                        new Dictionary<string, object> { ["credential"] = "private-key" }), tags: ["ready"]);
            }
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains("live"),
                    ResponseWriter = HealthResponseWriter.WriteLivenessAsync
                });
                endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains("ready"),
                    ResponseWriter = HealthResponseWriter.WriteReadinessAsync
                });
            });
        })).StartAsync();
}
