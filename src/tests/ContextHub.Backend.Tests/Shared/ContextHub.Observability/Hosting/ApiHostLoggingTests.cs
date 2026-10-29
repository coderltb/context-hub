namespace ContextHub.Backend.Tests.Shared.ContextHub.Observability.Hosting;

/// <summary>
/// Verifies the real API composition roots and their console output without database or business API setup.
/// </summary>
[Collection("Process-wide telemetry")]
public sealed class ApiHostLoggingTests
{
    /// <summary>
    /// Runs both API hosts together and checks independent identities and concurrent request correlation.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task Hosts_BothMode_PreserveRoutesAndIsolateCorrelation()
    {
        // This logging smoke uses local ephemeral keys; Production requires externally supplied token keys.
        await using var authentication = ApiHostProcess.Start("Authentication", "ContextHub.Authentication.Api", "Development", "Both");
        await using var hub = ApiHostProcess.Start("ContextHub", "ContextHub.Api", "Production", "Both");
        await Task.WhenAll(authentication.WaitUntilReadyAsync(), hub.WaitUntilReadyAsync());

        var authenticationIds = new[] { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D") };
        var hubIds = new[] { Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D") };
        await Task.WhenAll(authenticationIds.Select(id => CheckSsoAsync(authentication.Client, id))
            .Concat(hubIds.Select(id => CheckLivenessAsync(hub.Client, id))));
        using var missing = await hub.Client.GetAsync("/missing-scaffold-route");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var authenticationLogs = await authentication.ReadLogsAsync();
        var hubLogs = await hub.ReadLogsAsync();
        CheckCompletionRecords(authenticationLogs.Output, "ContextHub.Authentication.Api", authenticationIds, hubIds);
        CheckCompletionRecords(hubLogs.Output, "ContextHub.Api", hubIds, authenticationIds);
        Assert.Contains("HTTP GET completed", authenticationLogs.Error, StringComparison.Ordinal);
        Assert.Contains("HTTP GET completed", hubLogs.Error, StringComparison.Ordinal);
    }

    /// <summary>
    /// Loads the checked-in appsettings instead of requiring a format override for normal environments.
    /// </summary>
    /// <param name="environment">The selected environment.</param>
    /// <param name="json">Whether stdout must use compact JSON.</param>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task Host_Appsettings_SelectEnvironmentFormat(string environment, bool json)
    {
        await using var host = ApiHostProcess.Start("ContextHub", "ContextHub.Api", environment);
        await host.WaitUntilReadyAsync();
        var correlation = Guid.NewGuid().ToString("D");
        await CheckLivenessAsync(host.Client, correlation);

        var logs = await host.ReadLogsAsync();
        Assert.Empty(logs.Error);
        if (json)
        {
            CheckCompletionRecords(logs.Output, "ContextHub.Api", [correlation], []);
        }
        else
        {
            Assert.Contains("HTTP GET completed", logs.Output, StringComparison.Ordinal);
            Assert.Contains(correlation, logs.Output, StringComparison.Ordinal);
            Assert.False(logs.Output.TrimStart().StartsWith('{'));
        }
    }

    /// <summary>
    /// Checks native liveness and correlation on a real HTTP request without any database dependency.
    /// </summary>
    /// <param name="client">The HTTP client configured for the test boundary.</param>
    /// <param name="correlation">The correlation identifier associated with this test request.</param>
    /// <returns>A task that completes after the real endpoint response has been verified.</returns>
    private static async Task CheckLivenessAsync(HttpClient client, string correlation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live?token=query-sentinel");
        request.Headers.Add("X-Correlation-ID", correlation);
        using var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlation, Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
        Assert.False(body.RootElement.TryGetProperty("success", out _));
    }

    /// <summary>
    /// Checks Authentication's real SSO landing page without restoring the removed weather sample or opening a database.
    /// </summary>
    /// <param name="client">The real Authentication Kestrel client.</param>
    /// <param name="correlation">The request correlation isolated to this host.</param>
    /// <returns>The landing page and safe logging request assertions.</returns>
    private static async Task CheckSsoAsync(HttpClient client, string correlation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/?token=query-sentinel");
        request.Headers.Add("X-Correlation-ID", correlation);
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(correlation, Assert.Single(response.Headers.GetValues("X-Correlation-ID")));
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
    }

    /// <summary>
    /// Confirms one completion per supplied correlation and no cross-host identity or private query leakage.
    /// </summary>
    /// <param name="output">The captured stdout records inspected by the assertions.</param>
    /// <param name="service">The service identity required in these completion records.</param>
    /// <param name="expected">The expected result asserted by the scenario.</param>
    /// <param name="foreign">The other host's correlation identifier that must not leak into these records.</param>
    private static void CheckCompletionRecords(string output, string service, string[] expected, string[] foreign)
    {
        Assert.DoesNotContain("query-sentinel", output, StringComparison.Ordinal);
        var completions = new List<string>();
        foreach (var line in output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
        {
            using var document = JsonDocument.Parse(line);
            var record = document.RootElement;
            Assert.Equal(service, record.GetProperty("ServiceName").GetString());
            if (record.GetProperty("@mt").GetString() == "HTTP {RequestMethod} completed with {StatusCode} in {Elapsed:0.0000} ms.")
            {
                Assert.True(record.TryGetProperty("TraceId", out _));
                Assert.True(record.TryGetProperty("SpanId", out _));
                completions.Add(record.GetProperty("CorrelationId").GetString()!);
            }
        }

        foreach (var id in expected)
        {
            Assert.Single(completions, value => value == id);
        }

        foreach (var id in foreign)
        {
            Assert.DoesNotContain(id, completions);
        }
    }
}
