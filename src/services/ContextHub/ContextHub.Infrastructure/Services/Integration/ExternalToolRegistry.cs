namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Caches rebuildable definitions while rechecking database connection authority for every request.
/// </summary>
/// <param name="connectionRepository">The current connection authority.</param>
/// <param name="mcpClient">The bounded native protocol adapter.</param>
/// <param name="cacheService">The rebuildable metadata cache.</param>
/// <param name="options">The independently reviewed definition contracts.</param>
/// <param name="embeddingService">The existing bounded local embedding capability.</param>
/// <param name="logger">The metadata-only dependency logger.</param>
public sealed class ExternalToolRegistry(
    IExternalConnectionRepository connectionRepository,
    IExternalMcpClient mcpClient,
    ICacheService cacheService,
    IOptions<ExternalMcpOptions> options,
    IEmbeddingService embeddingService,
    ILogger<ExternalToolRegistry> logger) : IExternalToolRegistry
{
    /// <summary>
    /// Lists upstream definitions only inside the Owner preview boundary.
    /// </summary>
    /// <param name="name">The Owner-only label.</param>
    /// <param name="config">The checked canonical endpoint and policy.</param>
    /// <param name="cancellationToken">Cancels discovery.</param>
    /// <returns>The bounded definitions with an explicit eligibility flag.</returns>
    public async Task<IReadOnlyList<ExternalToolResponseDto>> PreviewAsync(
        string name,
        ExternalMcpConfigDto config,
        CancellationToken cancellationToken)
    {
        var tools = await mcpClient.DiscoverAsync(config.Url, cancellationToken);
        return tools.Select(tool =>
        {
            var response = new ExternalToolResponseDto { IsEligible = Qualify(name, config, tool) is not null };
            return tool.Adapt(response);
        }).ToArray();
    }

    /// <summary>
    /// Combines live database authority with bounded rebuildable discovery snapshots.
    /// </summary>
    /// <param name="cancellationToken">Cancels every read and native refresh.</param>
    /// <returns>The current reviewed candidates.</returns>
    public async Task<IReadOnlyList<ExternalToolCandidateModel>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        var connections = await connectionRepository.GetConnectionsAsync(cancellationToken);
        var candidates = new List<ExternalToolCandidateModel>();
        var attempted = 0;
        var healthy = 0;
        foreach (var connection in connections)
        {
            var config = ParseConfig(connection.Config);
            if (!config.Enabled)
            {
                continue;
            }
            attempted++;
            try
            {
                var snapshot = await GetSnapshotAsync(connection, config, false, cancellationToken);
                healthy++;
                foreach (var tool in snapshot.Tools)
                {
                    var qualified = Qualify(connection.Name, config, tool);
                    if (qualified is { } match)
                    {
                        candidates.Add(new(connection, config, tool, match.Review, match.Schema));
                    }
                }
            }
            catch (DependencyUnavailableException)
            {
                // A provider failure is isolated from other reviewed connections and the package catalog.
                logger.LogWarning(IntegrationLogConstants.CONNECTION_REFRESH_FAILED, connection.Id);
            }
        }
        if (attempted > 0 && healthy == 0)
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.MANAGED_TOOL_INDEX_UNAVAILABLE);
        }
        return candidates;
    }

    /// <summary>
    /// Resolves a reference only while current configuration, policy and definition still match.
    /// </summary>
    /// <param name="route">The cached routing binding.</param>
    /// <param name="refresh">Whether to verify the latest upstream definition.</param>
    /// <param name="cancellationToken">Cancels current authority and discovery.</param>
    /// <returns>The current reviewed candidate or null.</returns>
    public async Task<ExternalToolCandidateModel?> ResolveAsync(
        ExternalToolRouteModel route,
        bool refresh,
        CancellationToken cancellationToken)
    {
        if (route.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return null;
        }
        var connection = await connectionRepository.GetByIdAsync(route.ConnectionId, cancellationToken);
        if (connection is null || connection.Status != ExternalMcpStatus.Active || Hash(connection.Config) != route.ConfigHash)
        {
            return null;
        }
        var config = ParseConfig(connection.Config);
        if (!config.Enabled)
        {
            return null;
        }
        var snapshot = await GetSnapshotAsync(connection, config, refresh, cancellationToken);
        var tool = snapshot.Tools.FirstOrDefault(value => value.Name == route.ToolName && value.DefinitionHash == route.DefinitionHash);
        var qualified = tool is null ? null : Qualify(connection.Name, config, tool);
        return qualified is { } match ? new(connection, config, tool!, match.Review, match.Schema) : null;
    }

    /// <summary>
    /// Reads derived metadata without causing an upstream call from a list endpoint.
    /// </summary>
    /// <param name="connection">The current database connection.</param>
    /// <param name="cancellationToken">Cancels the cache read.</param>
    /// <returns>The current snapshot summary or unknown.</returns>
    public async Task<ExternalMcpDiscoveryDto> GetSummaryAsync(
        ExternalMcp connection,
        CancellationToken cancellationToken)
    {
        var snapshot = await cacheService.GetAsync<ExternalDiscoveryModel>(
            SnapshotKey(connection.Id),
            cancellationToken);
        if (snapshot is null || snapshot.ConfigHash != Hash(connection.Config) || snapshot.RefreshedAt.AddMinutes(IntegrationInfrastructureConstants.SNAPSHOT_LIFETIME_MINUTES) <= DateTimeOffset.UtcNow)
        {
            return new();
        }
        var config = ParseConfig(connection.Config);
        return new()
        {
            Status = IntegrationInfrastructureConstants.DISCOVERY_READY_STATUS,
            RefreshedAt = snapshot.RefreshedAt,
            ToolCount = snapshot.Tools.Count(tool => Qualify(connection.Name, config, tool) is not null)
        };
    }

    /// <summary>
    /// Refreshes each enabled connection while isolating known provider failures.
    /// </summary>
    /// <param name="cancellationToken">Cancels the refresh iteration.</param>
    /// <returns>The completed bounded refresh.</returns>
    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var connections = await connectionRepository.GetConnectionsAsync(cancellationToken);
        foreach (var connection in connections)
        {
            var config = ParseConfig(connection.Config);
            if (!config.Enabled)
            {
                continue;
            }
            try
            {
                await GetSnapshotAsync(connection, config, true, cancellationToken);
            }
            catch (DependencyUnavailableException)
            {
                logger.LogWarning(IntegrationLogConstants.CONNECTION_REFRESH_FAILED, connection.Id);
            }
        }
    }

    /// <summary>
    /// Gets or rebuilds metadata without caching a permission decision.
    /// </summary>
    /// <param name="connection">The current authority entity.</param>
    /// <param name="config">The validated current canonical configuration.</param>
    /// <param name="refresh">Whether stale metadata must be replaced now.</param>
    /// <param name="cancellationToken">Cancels cache and network work.</param>
    /// <returns>The exact discovery snapshot.</returns>
    private async Task<ExternalDiscoveryModel> GetSnapshotAsync(
        ExternalMcp connection,
        ExternalMcpConfigDto config,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var hash = Hash(connection.Config);
        var key = SnapshotKey(connection.Id);
        var cached = refresh ? null : await cacheService.GetAsync<ExternalDiscoveryModel>(
            key,
            cancellationToken);
        if (cached is not null && cached.ConfigHash == hash && cached.RefreshedAt.AddMinutes(IntegrationInfrastructureConstants.SNAPSHOT_LIFETIME_MINUTES) > DateTimeOffset.UtcNow)
        {
            return cached;
        }
        var discovered = await mcpClient.DiscoverAsync(config.Url, cancellationToken);
        var tools = new List<ExternalToolModel>();
        foreach (var tool in discovered)
        {
            var review = Qualify(connection.Name, config, tool);
            var embedding = review is { } approved
                ? await embeddingService.EncodePassageAsync(
                    approved.Review.Title + " " + approved.Review.Summary,
                    cancellationToken)
                : null;
            tools.Add(tool with {
                Embedding = embedding?.Values,
                EmbeddingModel = embedding?.Model
            });
        }
        var snapshot = new ExternalDiscoveryModel(tools, DateTimeOffset.UtcNow, hash);
        await cacheService.SetAsync(
            key,
            snapshot,
            new CacheEntryPolicy(
                TimeSpan.FromMinutes(IntegrationInfrastructureConstants.SNAPSHOT_LIFETIME_MINUTES),
                TimeSpan.FromSeconds(IntegrationInfrastructureConstants.LOCAL_CACHE_SECONDS)),
            cancellationToken);
        return snapshot;
    }

    /// <summary>
    /// Requires an independent exact-definition review in addition to native annotations and Owner policy.
    /// </summary>
    /// <param name="name">The private connection label.</param>
    /// <param name="config">The current canonical tool policy.</param>
    /// <param name="tool">The exact discovered definition.</param>
    /// <returns>The approved neutral contract, or null for quarantine.</returns>
    private (ReviewedExternalToolOptions Review, JsonElement Schema)? Qualify(
        string name,
        ExternalMcpConfigDto config,
        ExternalToolModel tool)
    {
        if (!tool.ReadOnlyHint || config.ToolPolicy.Mode == "allowlist" && !config.ToolPolicy.Tools!.Contains(
            tool.Name,
            StringComparer.Ordinal))
        {
            return null;
        }
        var review = options.Value.ReviewedTools.FirstOrDefault(value => value.ConnectionName == name && value.ToolName == tool.Name &&
            value.DefinitionHash == tool.DefinitionHash && value.Title.Length is > 0 and <= 128 &&
            value.Summary.Length is > 0 and <= 512 && value.OutputOverview.Length is > 0 and <= 1024);
        var schema = review is null ? null : ExternalToolSchemaHelper.Sanitize(tool.InputSchema);
        return schema is { } accepted ? (review!, accepted) : null;
    }

    /// <summary>
    /// Parses only canonical stored configuration.
    /// </summary>
    /// <param name="json">The database-owned validated canonical JSON.</param>
    /// <returns>The typed current settings.</returns>
    public static ExternalMcpConfigDto ParseConfig(string json)
    {
        using var document = JsonDocument.Parse(json);
        return ExternalMcpImportHelper.ReadCanonical(document.RootElement);
    }

    /// <summary>
    /// Computes a configuration fingerprint without exposing its contents.
    /// </summary>
    /// <param name="value">The canonical internal content.</param>
    /// <returns>The SHA-256 lowercase fingerprint.</returns>
    public static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>
    /// Names one rebuildable discovery entry using a trusted database identifier.
    /// </summary>
    /// <param name="id">The saved connection identifier.</param>
    /// <returns>The native cache key.</returns>
    public static CacheKey SnapshotKey(Guid id) => CacheKey.Create("external.discovery", 1, id.ToString("N"));
}
