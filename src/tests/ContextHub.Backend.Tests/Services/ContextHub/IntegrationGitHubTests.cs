namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies exact source routing and native credential caching without contacting GitHub.
/// </summary>
public sealed class IntegrationGitHubTests
{
    private const string Commit = "1234567890abcdef1234567890abcdef12345678";

    /// <summary>
    /// Uses one installation-token exchange and exact commit-bound source paths across reads.
    /// </summary>
    /// <returns>The source, assertion and cached-token assertions.</returns>
    [Fact]
    public async Task ExactReadUsesNativeAppAssertionAndCachesOnlyInstallationCredential()
    {
        using var rsa = RSA.Create(2048);
        using var handler = new GitHubHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(httpClient, memory, rsa);

        var first = await client.ReadAsync("https://github.com/Owner/Repository", Commit, "packages/example", CancellationToken.None);
        var second = await client.ReadAsync("https://github.com/Owner/Repository", Commit, "packages/example", CancellationToken.None);

        Assert.Equal("{\"schemaVersion\":1}", first.ManifestJson);
        Assert.Equal("{\"items\":[]}", second.BundleJson);
        Assert.Equal(Commit, first.CommitSha);
        Assert.Equal(1, handler.TokenCalls);
        Assert.Equal(8, handler.ReadCalls);
        Assert.All(handler.SourcePaths, path => Assert.Contains("ref=" + Commit, path, StringComparison.Ordinal));
        Assert.Contains(handler.SourcePaths, path => path.Contains("/packages/example/manifest.json", StringComparison.Ordinal));
        Assert.Contains(handler.SourcePaths, path => path.Contains("/packages/example/bundle.json", StringComparison.Ordinal));
        var validation = await new JsonWebTokenHandler().ValidateTokenAsync(handler.AppAssertion!, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = "test-app", ValidateAudience = false,
            IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)), RequireSignedTokens = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        });
        Assert.True(validation.IsValid);
    }

    /// <summary>
    /// Rejects non-exact commits before credential acquisition or any provider IO.
    /// </summary>
    /// <returns>The zero-request assertion.</returns>
    [Fact]
    public async Task InvalidCommitNeverContactsGitHub()
    {
        using var rsa = RSA.Create(2048);
        using var handler = new GitHubHandler();
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(httpClient, memory, rsa);

        await Assert.ThrowsAsync<BusinessException>(() => client.ReadAsync(
            "https://github.com/owner/repository", "main", "packages/example", CancellationToken.None));

        Assert.Equal(0, handler.TokenCalls);
        Assert.Equal(0, handler.ReadCalls);
    }

    /// <summary>
    /// Checks Git object mode before the contents endpoint could dereference a symlink.
    /// </summary>
    /// <returns>The rejected source and zero-content-read assertions.</returns>
    [Fact]
    public async Task SymbolicLinkIsRejectedBeforeContentsRead()
    {
        using var rsa = RSA.Create(2048);
        using var handler = new GitHubHandler { SymlinkManifest = true };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(httpClient, memory, rsa);

        await Assert.ThrowsAsync<BusinessException>(() => client.ReadAsync(
            "https://github.com/owner/repository", Commit, "packages/example", CancellationToken.None));

        Assert.Empty(handler.SourcePaths);
        Assert.Equal(2, handler.ReadCalls);
    }

    /// <summary>
    /// Leaves caller cancellation unchanged instead of translating it into a provider outage.
    /// </summary>
    /// <returns>The cancellation and non-replayed exchange assertions.</returns>
    [Fact]
    public async Task CallerCancellationStopsCredentialExchange()
    {
        using var rsa = RSA.Create(2048);
        using var handler = new GitHubHandler { StallExchange = true };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cancellation = new CancellationTokenSource();
        var client = CreateClient(httpClient, memory, rsa);
        var pending = client.ReadAsync("https://github.com/owner/repository", Commit, "packages/example", cancellation.Token);
        await handler.ExchangeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, handler.TokenCalls);
        Assert.Equal(0, handler.ReadCalls);
    }

    /// <summary>
    /// Closes malformed provider content and reports a safe dependency failure.
    /// </summary>
    /// <returns>The safe failure and response-disposal assertions.</returns>
    [Fact]
    public async Task InvalidProviderJsonIsSafeAndOwnedResponseIsDisposed()
    {
        using var rsa = RSA.Create(2048);
        using var handler = new GitHubHandler { InvalidJson = true };
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var client = CreateClient(httpClient, memory, rsa);

        var failure = await Assert.ThrowsAsync<DependencyUnavailableException>(() => client.ReadAsync(
            "https://github.com/owner/repository", Commit, "packages/example", CancellationToken.None));

        Assert.IsAssignableFrom<JsonException>(failure.InnerException);
        Assert.DoesNotContain("provider-private-value", failure.Message, StringComparison.Ordinal);
        Assert.True(handler.InvalidContent!.Disposed);
    }

    private static GitHubSourceClient CreateClient(HttpClient httpClient, MemoryCache memory, RSA rsa) => new(
        httpClient, Options.Create(new GitHubOptions
        {
            AppId = "test-app", InstallationId = 123, PrivateKey = rsa.ExportPkcs8PrivateKeyPem()
        }), memory);

    private sealed class GitHubHandler : HttpMessageHandler
    {
        public int TokenCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public string? AppAssertion { get; private set; }
        public List<string> SourcePaths { get; } = [];
        public bool StallExchange { get; init; }
        public bool InvalidJson { get; init; }
        public bool SymlinkManifest { get; init; }
        public TrackingContent? InvalidContent { get; private set; }
        public TaskCompletionSource ExchangeStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Post)
            {
                TokenCalls++;
                AppAssertion = request.Headers.Authorization?.Parameter;
                ExchangeStarted.TrySetResult();
                if (StallExchange)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                if (InvalidJson)
                {
                    InvalidContent = new TrackingContent("provider-private-value");
                    return new(HttpStatusCode.OK) { Content = InvalidContent };
                }
                return Json(new { token = "installation-secret", expires_at = DateTimeOffset.UtcNow.AddHours(1) });
            }
            ReadCalls++;
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("installation-secret", request.Headers.Authorization?.Parameter);
            var path = request.RequestUri!.PathAndQuery;
            if (path.Contains("/commits/", StringComparison.Ordinal))
            {
                Assert.EndsWith(Commit, path, StringComparison.Ordinal);
                return Json(new { sha = Commit });
            }
            if (path.Contains("/git/trees/", StringComparison.Ordinal))
            {
                return Json(new
                {
                    truncated = false,
                    tree = new[]
                    {
                        new { path = "packages/example/manifest.json", type = "blob", mode = SymlinkManifest ? "120000" : "100644" },
                        new { path = "packages/example/bundle.json", type = "blob", mode = "100644" }
                    }
                });
            }
            SourcePaths.Add(path);
            var content = path.Contains("manifest.json", StringComparison.Ordinal) ? "{\"schemaVersion\":1}" : "{\"items\":[]}";
            return Json(new { type = "file", encoding = "base64", size = Encoding.UTF8.GetByteCount(content), content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)) });
        }

        private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json")
        };
    }

    private sealed class TrackingContent(string value) : StringContent(value)
    {
        public bool Disposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
