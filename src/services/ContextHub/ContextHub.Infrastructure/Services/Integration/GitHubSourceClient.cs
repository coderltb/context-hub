namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Uses GitHub's bounded REST API with short-lived native JWT installation authentication.
/// </summary>
/// <param name="httpClient">The typed factory client with safe-method resilience.</param>
/// <param name="gitHubOptions">The server-owned App and installation configuration.</param>
/// <param name="memoryCache">The host-local short-lived installation-token cache.</param>
public sealed class GitHubSourceClient(
    HttpClient httpClient,
    IOptions<GitHubOptions> gitHubOptions,
    IMemoryCache memoryCache)
    : IGitHubSourceClient
{
    /// <summary>
    /// Resolves the stored reference then validates supported source files without mirroring them.
    /// </summary>
    /// <param name="repositoryUrl">The persisted canonical repository URL.</param>
    /// <param name="reference">The persisted configured reference.</param>
    /// <param name="cancellationToken">Cancels every outbound and content-read operation.</param>
    /// <returns>The inspected exact commit and file count.</returns>
    public async Task<GitHubInspectionModel> InspectAsync(
        string repositoryUrl,
        string reference,
        CancellationToken cancellationToken)
    {
        var route = RepositoryRoute(repositoryUrl);
        var commit = await GetJsonAsync($"{route}/commits/{Uri.EscapeDataString(reference)}", cancellationToken);
        var sha = commit.GetProperty("sha").GetString()!;
        var tree = await ReadTreeAsync(route, sha, cancellationToken);

        // Inspect only supported authoring documents; a symlink is not immutable source content.
        var inspected = 0;
        foreach (var item in tree.EnumerateArray())
        {
            var path = item.GetProperty("path").GetString()!;
            var fileName = path[(path.LastIndexOf('/') + 1)..];
            if (fileName is not ("manifest.json" or "bundle.json"))
            {
                continue;
            }
            if (++inspected > IntegrationInfrastructureConstants.MAX_INSPECTED_SOURCE_FILES ||
                item.GetProperty("type").GetString() != "blob" ||
                item.GetProperty("mode").GetString() == IntegrationInfrastructureConstants.GIT_SYMLINK_MODE)
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }
            var directory = path.Contains('/') ? path[..(path.LastIndexOf('/') + 1)] : "";
            var encoded = GitHubSourceHelper.EncodeSourcePath(directory) + Uri.EscapeDataString(path[(path.LastIndexOf('/') + 1)..]);
            var content = await ReadFileAsync(route, sha, encoded, cancellationToken);
            using var document = JsonDocument.Parse(content);
        }
        return new(sha, inspected);
    }

    /// <summary>
    /// Reads the canonical authoring files at an exact verified commit.
    /// </summary>
    /// <param name="repositoryUrl">The persisted allowlisted repository URL.</param>
    /// <param name="commitSha">The immutable forty-character commit identifier.</param>
    /// <param name="sourcePath">The relative directory containing manifest.json and bundle.json.</param>
    /// <param name="cancellationToken">Cancels every outbound and content-read operation.</param>
    /// <returns>The immutable source documents.</returns>
    public async Task<PackageSourceModel> ReadAsync(
        string repositoryUrl,
        string commitSha,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        // An exact commit prevents a moving branch reference from changing a published package.
        if (commitSha.Length != IntegrationInfrastructureConstants.COMMIT_SHA_LENGTH ||
            commitSha.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        var route = RepositoryRoute(repositoryUrl);
        var commit = await GetJsonAsync($"{route}/commits/{commitSha}", cancellationToken);
        if (!string.Equals(commit.GetProperty("sha").GetString(), commitSha, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }

        var path = GitHubSourceHelper.EncodeSourcePath(sourcePath);
        var rawDirectory = sourcePath.TrimEnd('/');
        var rawPrefix = rawDirectory is "" or "." ? "" : rawDirectory + "/";
        var tree = await ReadTreeAsync(route, commitSha, cancellationToken);
        foreach (var name in new[] { "manifest.json", "bundle.json" })
        {
            // Contents API may dereference symlinks, so verify the exact Git tree before reading either document.
            var entry = tree.EnumerateArray().FirstOrDefault(value => value.GetProperty("path").GetString() == rawPrefix + name);
            if (entry.ValueKind == JsonValueKind.Undefined || entry.GetProperty("type").GetString() != "blob" ||
                entry.GetProperty("mode").GetString() is not ("100644" or "100755"))
            {
                throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
            }
        }
        var manifest = await ReadFileAsync(route, commitSha, path + "manifest.json", cancellationToken);
        var bundle = await ReadFileAsync(route, commitSha, path + "bundle.json", cancellationToken);
        return new(manifest, bundle, commitSha.ToLowerInvariant());
    }

    /// <summary>
    /// Constructs only the fixed GitHub API route after canonical URL validation.
    /// </summary>
    /// <param name="url">The persisted repository URL.</param>
    /// <returns>The service-owned API path.</returns>
    private static string RepositoryRoute(string url) =>
        "repos/" + new Uri(GitHubSourceHelper.CanonicalRepositoryUrl(url)).AbsolutePath.Trim('/');

    /// <summary>
    /// Reads one complete bounded Git tree so truncated listings cannot hide a forbidden source entry.
    /// </summary>
    /// <param name="route">The checked repository API path.</param>
    /// <param name="sha">The resolved immutable commit.</param>
    /// <param name="cancellationToken">Cancels HTTP and content reads.</param>
    /// <returns>The complete tree entries.</returns>
    private async Task<JsonElement> ReadTreeAsync(
        string route,
        string sha,
        CancellationToken cancellationToken)
    {
        var tree = await GetJsonAsync($"{route}/git/trees/{sha}?recursive=1", cancellationToken);
        if (tree.TryGetProperty("truncated", out var truncated) && truncated.GetBoolean())
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        if (!tree.TryGetProperty("tree", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.GITHUB_RESPONSE_INVALID);
        }
        return entries;
    }

    /// <summary>
    /// Reads a regular file from the contents API with a bounded decoded payload.
    /// </summary>
    /// <param name="route">The validated GitHub repository API route.</param>
    /// <param name="sha">The exact commit.</param>
    /// <param name="path">The encoded checked file path.</param>
    /// <param name="cancellationToken">Cancels HTTP and body reads.</param>
    /// <returns>The UTF-8 source text.</returns>
    private async Task<string> ReadFileAsync(
        string route,
        string sha,
        string path,
        CancellationToken cancellationToken)
    {
        var file = await GetJsonAsync($"{route}/contents/{path}?ref={sha}", cancellationToken);
        if (file.GetProperty("type").GetString() != "file" || file.GetProperty("encoding").GetString() != "base64" ||
            file.GetProperty("size").GetInt64() > IntegrationConstants.MAX_SOURCE_BYTES)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        // Validate decoded size too; remote metadata alone cannot bound the accepted payload.
        var decoded = Convert.FromBase64String(file.GetProperty("content").GetString()!);
        if (decoded.Length > IntegrationConstants.MAX_SOURCE_BYTES)
        {
            throw new BusinessException(IntegrationConstants.INVALID_INPUT, IntegrationConstants.INVALID_MESSAGE);
        }
        return new UTF8Encoding(false, true).GetString(decoded);
    }

    /// <summary>
    /// Performs one safe source read without logging request paths, headers or response content.
    /// </summary>
    /// <param name="path">The internally constructed GitHub API path.</param>
    /// <param name="cancellationToken">Cancels token acquisition, HTTP and content reads.</param>
    /// <returns>The cloned bounded JSON document.</returns>
    private async Task<JsonElement> GetJsonAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var token = await GetInstallationTokenAsync(cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await SendJsonAsync(request, cancellationToken);
    }

    /// <summary>
    /// Exchanges a server-owned App assertion for a short-lived installation token.
    /// </summary>
    /// <param name="cancellationToken">Cancels the non-retried exchange.</param>
    /// <returns>The host-local installation token.</returns>
    private async Task<string> GetInstallationTokenAsync(CancellationToken cancellationToken)
    {
        var settings = gitHubOptions.Value;
        var cacheKey = $"github.installation:{settings.AppId}:{settings.InstallationId}";
        if (memoryCache.TryGetValue<string>(cacheKey, out var cached))
        {
            return cached!;
        }

        // Provider assertions stay short-lived; installation secrets remain only in the host-local cache.
        using var rsa = RSA.Create();
        rsa.ImportFromPem(settings.PrivateKey);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.AppId,
            IssuedAt = DateTime.UtcNow.AddSeconds(-IntegrationInfrastructureConstants.APP_ASSERTION_CLOCK_SKEW_SECONDS),
            NotBefore = DateTime.UtcNow.AddSeconds(-IntegrationInfrastructureConstants.APP_ASSERTION_CLOCK_SKEW_SECONDS),
            Expires = DateTime.UtcNow.AddMinutes(IntegrationInfrastructureConstants.APP_ASSERTION_LIFETIME_MINUTES),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa.ExportParameters(true)), SecurityAlgorithms.RsaSha256)
        };
        var assertion = new JsonWebTokenHandler().CreateToken(descriptor);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"app/installations/{settings.InstallationId}/access_tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", assertion);
        var result = await SendJsonAsync(request, cancellationToken);
        var token = result.GetProperty("token").GetString()!;
        var expiresAt = result.GetProperty("expires_at").GetDateTimeOffset()
            .AddMinutes(-IntegrationInfrastructureConstants.INSTALLATION_TOKEN_REFRESH_MARGIN_MINUTES);
        memoryCache.Set(cacheKey, token, expiresAt);
        return token;
    }

    /// <summary>
    /// Bounds buffered content and translates only known transport/dependency failures.
    /// </summary>
    /// <param name="request">The owned internal request, without automatic unsafe-method retries.</param>
    /// <param name="cancellationToken">Cancels HTTP and body reads.</param>
    /// <returns>The cloned native JSON value.</returns>
    private async Task<JsonElement> SendJsonAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // The deadline covers headers and bounded body reads while caller cancellation keeps its native meaning.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(IntegrationInfrastructureConstants.REQUEST_TIMEOUT_SECONDS));
        var operationToken = deadline.Token;
        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new DependencyUnavailableException(IntegrationErrorConstants.GITHUB_UNAVAILABLE);
            }
            await using var stream = await response.Content.ReadAsStreamAsync(operationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int count;
            while ((count = await stream.ReadAsync(chunk, operationToken)) > 0)
            {
                if (buffer.Length + count > IntegrationConstants.MAX_SOURCE_BYTES * 2)
                {
                    throw new DependencyUnavailableException(IntegrationErrorConstants.GITHUB_RESPONSE_INVALID);
                }
                await buffer.WriteAsync(chunk.AsMemory(0, count), operationToken);
            }
            using var document = JsonDocument.Parse(buffer.ToArray());
            return document.RootElement.Clone();
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or TimeoutRejectedException or BrokenCircuitException)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.GITHUB_UNAVAILABLE, exception);
        }
        catch (JsonException exception)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.GITHUB_RESPONSE_INVALID, exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.GITHUB_UNAVAILABLE, exception);
        }
    }
}
