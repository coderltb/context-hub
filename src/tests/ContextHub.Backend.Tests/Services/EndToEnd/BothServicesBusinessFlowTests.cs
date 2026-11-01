namespace ContextHub.Backend.Tests.Services.EndToEnd;

/// <summary>
/// Exercises authenticated business workflows across both native hosts with SQL-created data and controlled external IO.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class BothServicesBusinessFlowTests : BothServicesFlowFixture
{
    private const string Workflow = "contexthub.dotnet-api-review-workflow";
    private const string Rule = "contexthub.read-only-review-rule";
    private const string Version = "1.0.0";

    /// <summary>
    /// Publishes every package type, consumes it through MCP, reviews feedback and blocks a warmed dependency after revocation.
    /// </summary>
    /// <returns>The completed two-service catalog workflow.</returns>
    [Fact]
    public async Task OwnerPublicationClientConsumptionAndFeedbackUseRealPersistence()
    {
        var owner = await CreateOwnerAsync("catalog-owner");
        var access = owner.Credentials.GetProperty("access_token").GetString()!;
        var client = await CreateClientAsync(access,
            "catalog_search", "package_get", "bundle_resolve", "compatibility_check", "revocation_check", "quality_signal_submit");
        await PublishDocumentedPackagesAsync(access);

        await using var mcp = await ConnectMcpAsync(client.GetProperty("token").GetString()!);
        Assert.Equal(6, (await mcp.ListToolsAsync()).Count);
        var search = await CallToolAsync(mcp, "catalog_search", new() { ["query"] = "api review" });
        Assert.Contains(search.GetProperty("items").EnumerateArray(), item => item.GetProperty("package_id").GetString() == Workflow);

        var package = await CallToolAsync(mcp, "package_get", new() { ["package_id"] = Workflow });
        Assert.Equal(Version, package.GetProperty("version").GetString());
        Assert.False(package.GetProperty("manifest").TryGetProperty("authoring_source", out _));
        var bundle = await CallToolAsync(mcp, "bundle_resolve", new() { ["package_id"] = Workflow });
        var ordered = bundle.GetProperty("packages").EnumerateArray().Select(item => item.GetProperty("package_id").GetString()).ToArray();
        Assert.Equal(4, ordered.Length);
        Assert.Equal(Workflow, ordered[^1]);
        Assert.True(Array.IndexOf(ordered, Rule) < Array.IndexOf(ordered, "contexthub.dotnet-api-review-agent"));

        var compatible = await CallToolAsync(mcp, "compatibility_check", new()
        {
            ["package_id"] = Workflow,
            ["client_capabilities"] = new[] { "workspace_read" }
        });
        Assert.True(compatible.GetProperty("compatible").GetBoolean());
        var incompatible = await CallToolAsync(mcp, "compatibility_check", new()
        {
            ["package_id"] = Workflow,
            ["client_capabilities"] = Array.Empty<string>()
        });
        Assert.False(incompatible.GetProperty("compatible").GetBoolean());

        var resource = $"contexthub://packages/{Workflow}/{Version}/content";
        Assert.Single((await mcp.ReadResourceAsync(resource)).Contents);
        var missing = await mcp.CallToolAsync("package_get", new Dictionary<string, object?> { ["package_id"] = "contexthub.missing" });
        Assert.True(missing.IsError);

        var signal = new Dictionary<string, object?>
        {
            ["signal_type"] = "package_issue",
            ["issue_code"] = "instruction_ambiguous",
            ["artifact_type"] = "code",
            ["operation"] = "review",
            ["package_id"] = Workflow,
            ["version"] = Version,
            ["workflow_step_id"] = null,
            ["desired_package_type"] = null
        };
        Assert.Equal("accepted", (await CallToolAsync(mcp, "quality_signal_submit", signal)).GetProperty("status").GetString());
        Assert.Equal("deduplicated", (await CallToolAsync(mcp, "quality_signal_submit", signal)).GetProperty("status").GetString());

        var recommendations = (await SendJsonAsync(Hub, HttpMethod.Get,
            "/api/admin/v1/recommendations?packageId=" + Workflow, access)).GetProperty("data").GetProperty("items");
        var recommendation = Assert.Single(recommendations.EnumerateArray());
        Assert.Equal(2, recommendation.GetProperty("signalCount").GetInt64());
        var id = recommendation.GetProperty("id").GetGuid();
        foreach (var status in new[] { "accepted", "completed" })
        {
            recommendation = (await SendJsonAsync(Hub, HttpMethod.Put, $"/api/admin/v1/recommendations/{id}/status", access,
                new UpdateRecommendationStatusRequestDto
                {
                    Status = status,
                    ConcurrencyToken = recommendation.GetProperty("concurrencyToken").GetString()!
                })).GetProperty("data");
            Assert.Equal(status, recommendation.GetProperty("status").GetString());
        }

        var dashboard = (await SendJsonAsync(Hub, HttpMethod.Get, "/api/admin/v1/dashboard/summary", access)).GetProperty("data");
        Assert.Equal(4, dashboard.GetProperty("catalog").GetProperty("publishedVersions").GetInt64());
        Assert.True(dashboard.GetProperty("usage").GetProperty("errorCount").GetInt64() >= 1);

        // Revoking a dependency must invalidate authority even when the root content was already cached.
        var rule = (await SendJsonAsync(Hub, HttpMethod.Get, $"/api/admin/v1/packages/{Rule}/versions/{Version}", access)).GetProperty("data");
        await SendJsonAsync(Hub, HttpMethod.Post, $"/api/admin/v1/packages/{Rule}/versions/{Version}/revoke", access,
            new LifecycleVersionRequestDto { ConcurrencyToken = rule.GetProperty("concurrencyToken").GetString()! });
        var blocked = await mcp.CallToolAsync("bundle_resolve", new Dictionary<string, object?> { ["package_id"] = Workflow });
        Assert.True(blocked.IsError);
        await Assert.ThrowsAsync<McpProtocolException>(() => mcp.ReadResourceAsync(resource).AsTask());
        var revocation = await CallToolAsync(mcp, "revocation_check", new() { ["package_id"] = Rule, ["version"] = Version });
        Assert.Equal("revoked", revocation.GetProperty("status").GetString());

        using var scope = Hub.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ContextHubDbContext>();
        Assert.Equal(4, await database.Versions.CountAsync());
        Assert.Equal(owner.Id, (await database.Repositories.AsNoTracking().SingleAsync()).CreatedBy);
        Assert.Equal(1, await database.Recommendations.CountAsync());
        Assert.True(await database.DailyUsage.AnyAsync(value => value.ErrorCount > 0));
    }

    /// <summary>
    /// Updates prompt configuration by ID and rechecks tool state, credential revocation and client disablement on later requests.
    /// </summary>
    /// <returns>The completed live-configuration and access workflow.</returns>
    [Fact]
    public async Task PromptEditsToolStateAndTokenLifecycleTakeEffectAcrossHosts()
    {
        var owner = await CreateOwnerAsync("configuration-owner");
        var access = owner.Credentials.GetProperty("access_token").GetString()!;
        var issued = await CreateClientAsync(access, "catalog_search", "package_get");
        var clientId = issued.GetProperty("clientId").GetGuid();
        var firstToken = issued.GetProperty("token").GetString()!;
        await using var mcp = await ConnectMcpAsync(firstToken);

        var prompt = (await SendJsonAsync(Hub, HttpMethod.Post, "/api/admin/v1/mcp-prompts", access,
            new CreateMcpPromptRequestDto
            {
                Name = "synthetic_review_flow",
                Title = "Synthetic review",
                Content = "Review the requested area without editing files.",
                Arguments = [new McpPromptArgumentDto { Name = "area", Required = true }]
            }, HttpStatusCode.Created)).GetProperty("data");
        var promptId = prompt.GetProperty("id").GetGuid();
        var promptPath = $"/api/admin/v1/mcp-prompts/{promptId}";
        Assert.Equal("synthetic_review_flow", (await SendJsonAsync(Hub, HttpMethod.Get, promptPath, access))
            .GetProperty("data").GetProperty("name").GetString());
        Assert.Contains(await mcp.ListPromptsAsync(), item => item.Name == "synthetic_review_flow");
        var rendered = await mcp.GetPromptAsync(
            "synthetic_review_flow",
            new Dictionary<string, object?> { ["area"] = "transient-user-input-marker" });
        Assert.Contains("transient-user-input-marker", Assert.IsType<TextContentBlock>(Assert.Single(rendered.Messages).Content).Text);

        var update = new UpdateMcpPromptRequestDto
        {
            Content = "The Owner changed this configuration.",
            Arguments = [new McpPromptArgumentDto { Name = "area", Required = true }],
            IsEnabled = true,
            ConcurrencyToken = prompt.GetProperty("concurrencyToken").GetString()!
        };
        prompt = (await SendJsonAsync(Hub, HttpMethod.Put, promptPath, access, update)).GetProperty("data");
        await SendJsonAsync(Hub, HttpMethod.Put, promptPath, access, update, HttpStatusCode.Conflict);
        var changed = await mcp.GetPromptAsync("synthetic_review_flow", new Dictionary<string, object?> { ["area"] = "metadata" });
        Assert.Contains(update.Content, Assert.IsType<TextContentBlock>(Assert.Single(changed.Messages).Content).Text);
        await SendJsonAsync(Hub, HttpMethod.Put, promptPath, access, update with
        {
            IsEnabled = false,
            ConcurrencyToken = prompt.GetProperty("concurrencyToken").GetString()!
        });
        Assert.DoesNotContain(await mcp.ListPromptsAsync(), item => item.Name == "synthetic_review_flow");
        await Assert.ThrowsAsync<McpProtocolException>(() => mcp.GetPromptAsync("synthetic_review_flow").AsTask());

        var tools = (await SendJsonAsync(Hub, HttpMethod.Get, "/api/admin/v1/mcp-tools", access)).GetProperty("data").GetProperty("items");
        var search = tools.EnumerateArray().Single(item => item.GetProperty("name").GetString() == "catalog_search");
        await SendJsonAsync(Hub, HttpMethod.Put, "/api/admin/v1/mcp-tools/catalog_search", access, new UpdateMcpToolRequestDto
        {
            IsEnabled = false,
            ConcurrencyToken = search.GetProperty("concurrencyToken").GetString()!
        });
        Assert.DoesNotContain(await mcp.ListToolsAsync(), tool => tool.Name == "catalog_search");
        Assert.True((await mcp.CallToolAsync("catalog_search", new Dictionary<string, object?> { ["query"] = "review" })).IsError);

        // A new token belongs to the application, independently of the Owner's browser session.
        var second = (await SendJsonAsync(Authentication, HttpMethod.Post, $"/api/admin/v1/clients/{clientId}/tokens", access,
            new IssueClientTokenRequestDto { AllowedTools = ["package_get"], ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) },
            HttpStatusCode.Created)).GetProperty("data");
        var secondToken = second.GetProperty("token").GetString()!;
        await using var other = await ConnectMcpAsync(secondToken);
        await SendJsonAsync(Authentication, HttpMethod.Post,
            $"/api/admin/v1/clients/{clientId}/tokens/{issued.GetProperty("tokenId").GetGuid()}/revoke", access);
        var rejected = await Assert.ThrowsAsync<HttpRequestException>(() => mcp.ListToolsAsync().AsTask());
        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal("package_get", Assert.Single(await other.ListToolsAsync()).Name);
        await SendJsonAsync(Hub, HttpMethod.Get, "/api/admin/v1/packages", secondToken, expected: HttpStatusCode.Forbidden);

        var client = (await SendJsonAsync(Authentication, HttpMethod.Get, $"/api/admin/v1/clients/{clientId}", access)).GetProperty("data");
        Assert.DoesNotContain(secondToken, client.GetRawText());
        Assert.DoesNotContain(firstToken, client.GetRawText());
        await SendJsonAsync(Authentication, HttpMethod.Put, $"/api/admin/v1/clients/{clientId}", access,
            new UpdateClientRequestDto(
                "Disabled synthetic client",
                false,
                client.GetProperty("client").GetProperty("concurrencyToken").GetString()!));
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Assert.ThrowsAsync<HttpRequestException>(() => other.ListToolsAsync().AsTask())).StatusCode);

        using var scope = Hub.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<ContextHubDbContext>();
        Assert.False(await database.Prompts.AnyAsync(value => value.Content.Contains("transient-user-input-marker")));
        await using var connection = await Hub.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
        Assert.False(await connection.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM access.\"Prompt\" WHERE \"Arguments\"::text LIKE @marker)",
            new { marker = "%transient-user-input-marker%" }));
        Assert.False((await database.Prompts.AsNoTracking().SingleAsync(value => value.Id == promptId)).IsEnabled);
        await AssertAnonymousHealthAsync();
    }

    /// <summary>
    /// Resets an actual Owner password using the emailed native OTP and rejects old grants without revoking client credentials.
    /// </summary>
    /// <returns>The completed HTTP recovery and session workflow.</returns>
    [Fact]
    public async Task EmailOtpResetRevokesHumanGrantsWhileClientRemainsUsable()
    {
        var owner = await CreateOwnerAsync("recovery-owner");
        var access = owner.Credentials.GetProperty("access_token").GetString()!;
        var issued = await CreateClientAsync(access, "catalog_search");
        var clientToken = issued.GetProperty("token").GetString()!;
        await using var mcp = await ConnectMcpAsync(clientToken);
        await SendJsonAsync(Hub, HttpMethod.Get, "/api/admin/v1/packages", access);

        using var browser = Authentication.GetTestClient();
        var cookies = new CookieContainer();
        using var forgot = await SubmitFormAsync(browser, cookies, AuthenticationRecoveryConstants.FORGOT_PAGE,
            new() { ["Input.Email"] = "recovery-owner@example.test" });
        Assert.Equal(HttpStatusCode.Redirect, forgot.StatusCode);
        var email = Assert.Single(EmailService.Messages);
        Assert.Equal("recovery-owner@example.test", email.Recipient);
        var otp = Regex.Match(email.PlainText, @"\b[0-9]{6}\b").Value;
        Assert.Equal(6, otp.Length);

        using var verified = await SubmitFormAsync(browser, cookies, AuthenticationRecoveryConstants.VERIFY_PAGE,
            new() { ["Input.Email"] = email.Recipient, ["Input.OtpCode"] = otp });
        Assert.Equal(HttpStatusCode.Redirect, verified.StatusCode);
        Assert.NotNull(cookies.GetCookies(new Uri("https://localhost"))[AuthenticationRecoveryConstants.RECOVERY_COOKIE]);
        const string replacement = "Updated-native-Password!456";
        using var reset = await SubmitFormAsync(browser, cookies, AuthenticationRecoveryConstants.RESET_PAGE,
            new() { ["Input.Password"] = replacement, ["Input.ConfirmPassword"] = replacement });
        Assert.Equal(HttpStatusCode.Redirect, reset.StatusCode);
        Assert.StartsWith("/login", reset.Headers.Location!.OriginalString);
        Assert.Null(cookies.GetCookies(new Uri("https://localhost"))[AuthenticationRecoveryConstants.RECOVERY_COOKIE]);
        Assert.Null(cookies.GetCookies(new Uri("https://localhost"))["__Host-context-hub-sso"]);

        await SendJsonAsync(Hub, HttpMethod.Get, "/api/admin/v1/packages", access, expected: HttpStatusCode.Unauthorized);
        using var refresh = await browser.PostAsync("/connect/token",
            AuthenticationProtocolFixture.RefreshForm(owner.Credentials.GetProperty("refresh_token").GetString()!));
        Assert.Equal(HttpStatusCode.BadRequest, refresh.StatusCode);
        Assert.Equal("catalog_search", Assert.Single(await mcp.ListToolsAsync()).Name);

        var wrongCookies = new CookieContainer();
        using var oldPassword = await SubmitFormAsync(browser, wrongCookies, "/login", new()
        {
            ["Input.UserName"] = "recovery-owner",
            ["Input.Password"] = "Native-test-Password!123"
        });
        Assert.Equal(HttpStatusCode.OK, oldPassword.StatusCode);
        Assert.Null(wrongCookies.GetCookies(new Uri("https://localhost"))["__Host-context-hub-sso"]);
        var newCookies = new CookieContainer();
        using var newPassword = await SubmitFormAsync(browser, newCookies, "/login", new()
        {
            ["Input.UserName"] = "recovery-owner",
            ["Input.Password"] = replacement
        });
        Assert.Equal(HttpStatusCode.Redirect, newPassword.StatusCode);
        var newCookie = newCookies.GetCookieHeader(new Uri("https://localhost"));
        var grant = await AuthenticationProtocolFixture.AuthorizeAsync(browser, newCookie, swagger: false);
        var credentials = await AuthenticationProtocolFixture.ExchangeAsync(browser, grant.Code, grant.Verifier, swagger: false);
        await SendJsonAsync(Hub, HttpMethod.Get, "/api/admin/v1/packages", credentials.GetProperty("access_token").GetString()!);

        await AuthenticationProtocolFixture.LogoutAsync(browser, newCookie);
        var verifier = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        using var staleCookie = new HttpRequestMessage(HttpMethod.Get, AuthenticationProtocolFixture.AuthorizationUri(verifier, swagger: false));
        staleCookie.Headers.Add("Cookie", newCookie);
        using var stale = await browser.SendAsync(staleCookie);
        Assert.Equal(HttpStatusCode.Redirect, stale.StatusCode);
        Assert.Contains("/login", stale.Headers.Location!.OriginalString);
        Assert.Equal("catalog_search", Assert.Single(await mcp.ListToolsAsync()).Name);
        using var scope = Authentication.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var user = await userManager.FindByIdAsync(owner.Id.ToString());
        Assert.NotNull(user);
        Assert.False(await userManager.CheckPasswordAsync(user, "Native-test-Password!123"));
        Assert.True(await userManager.CheckPasswordAsync(user, replacement));
    }

    private async Task PublishDocumentedPackagesAsync(string access)
    {
        var source = (await SendJsonAsync(Hub, HttpMethod.Post, "/api/admin/v1/repositories", access,
            new CreateRepositoryRequestDto
            {
                RepositoryUrl = "https://github.com/context-hub-tests/flow-source",
                DefaultRef = "main"
            }, HttpStatusCode.Created)).GetProperty("data");
        var repositoryId = source.GetProperty("id").GetGuid();
        var root = WorkspacePath("Documents", "reference", "packages", "examples");
        var manifests = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "dotnet-api-review.manifests.json")))!.AsArray();
        Hub.Services.GetRequiredService<IOptions<GitHubOptions>>().Value.AuthoringRepositories.Add("context-hub-fixture-authoring", repositoryId);

        foreach (var manifest in manifests.Select(item => item!.AsObject()))
        {
            var id = manifest["package_id"]!.GetValue<string>();
            var kind = manifest["package_type"]!.GetValue<string>();
            var file = kind == "rule" ? "read-only-review-rule.bundle.json" : "dotnet-api-review-" + kind + ".bundle.json";
            var commit = manifest["authoring_source"]!["commit"]!.GetValue<string>();
            var path = "packages/" + id;
            GitHubClient.Sources.Add(path, new PackageSourceModel(
                manifest.ToJsonString(),
                await File.ReadAllTextAsync(Path.Combine(root, file)),
                commit));
            await SendJsonAsync(Hub, HttpMethod.Post, "/api/admin/v1/packages", access,
                new CreatePackageRequestDto
                {
                    PackageId = id,
                    PackageType = kind,
                    DisplayName = "Documented " + kind + " review",
                    Summary = "Evidence-based API review guidance."
                }, HttpStatusCode.Created);
            var publish = new PublishVersionRequestDto
            {
                Version = Version,
                AuthoringRepository = "context-hub-fixture-authoring",
                SourceCommitSha = commit,
                SourcePath = path
            };
            var versionsPath = $"/api/admin/v1/packages/{id}/versions";
            var preview = (await SendJsonAsync(Hub, HttpMethod.Post, versionsPath + "/preview", access, publish)).GetProperty("data");
            Assert.Equal("ready", preview.GetProperty("readiness").GetProperty("status").GetString());
            var empty = (await SendJsonAsync(Hub, HttpMethod.Get, versionsPath, access)).GetProperty("data").GetProperty("items");
            Assert.Empty(empty.EnumerateArray());
            var first = (await SendJsonAsync(Hub, HttpMethod.Post, versionsPath, access, publish, HttpStatusCode.Created)).GetProperty("data");
            var replay = (await SendJsonAsync(Hub, HttpMethod.Post, versionsPath, access, publish, HttpStatusCode.Created)).GetProperty("data");
            Assert.Equal(first.GetProperty("contentHash").GetString(), replay.GetProperty("contentHash").GetString());
            Assert.Equal(first.GetProperty("publishedAt").GetString(), replay.GetProperty("publishedAt").GetString());
        }

        var synced = (await SendJsonAsync(Hub, HttpMethod.Post, $"/api/admin/v1/repositories/{repositoryId}/sync", access,
            new SyncRepositoryRequestDto { ConcurrencyToken = source.GetProperty("concurrencyToken").GetString()! })).GetProperty("data");
        Assert.Equal(new string('1', 40), synced.GetProperty("resolvedCommitSha").GetString());
    }

    private static async Task<JsonElement> CallToolAsync(McpClient client, string name, Dictionary<string, object?> arguments)
    {
        var result = await client.CallToolAsync(name, arguments);
        var errorCode = result.StructuredContent is { } payload && payload.TryGetProperty("error", out var error)
            ? error.GetProperty("code").GetString()
            : null;
        Assert.True(result.IsError != true, name + ": " + errorCode);
        Assert.NotNull(result.StructuredContent);

        return result.StructuredContent.Value;
    }

    private async Task AssertAnonymousHealthAsync()
    {
        foreach (var host in new[] { Authentication, Hub })
        {
            using var client = host.GetTestClient();
            foreach (var path in new[] { "/health/live", "/health/ready" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid-synthetic-token");
                using var response = await client.SendAsync(request);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }
    }

    private static async Task<HttpResponseMessage> SubmitFormAsync(
        HttpClient client,
        CookieContainer cookies,
        string path,
        Dictionary<string, string> fields)
    {
        var origin = new Uri("https://localhost");
        using var get = new HttpRequestMessage(HttpMethod.Get, path);
        var cookieHeader = cookies.GetCookieHeader(origin);
        if (cookieHeader.Length > 0)
        {
            get.Headers.Add("Cookie", cookieHeader);
        }
        using var page = await client.SendAsync(get);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        UpdateCookies(page, cookies);
        fields["__RequestVerificationToken"] = AuthenticationProtocolFixture.Antiforgery(await page.Content.ReadAsStringAsync());
        fields["ReturnUrl"] = "/";
        using var post = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        post.Headers.Add("Cookie", cookies.GetCookieHeader(origin));
        var response = await client.SendAsync(post);
        UpdateCookies(response, cookies);

        return response;
    }

    private static void UpdateCookies(HttpResponseMessage response, CookieContainer cookies)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var headers))
        {
            foreach (var header in headers)
            {
                cookies.SetCookies(new Uri("https://localhost"), header);
            }
        }
    }
}
