namespace ContextHub.Infrastructure.Services.Integration;

/// <summary>
/// Serves neutral capabilities through current authority and reviewed contracts.
/// </summary>
/// <param name="externalToolRegistry">The current reviewed registry.</param>
/// <param name="externalMcpClient">The bounded native MCP caller.</param>
/// <param name="cacheService">The rebuildable route and documentation cache.</param>
/// <param name="embeddingService">The existing local semantic encoder.</param>
/// <param name="externalMcpOptions">The reviewed contracts and cache-fingerprint key.</param>
public sealed class ManagedToolService(
    IExternalToolRegistry externalToolRegistry,
    IExternalMcpClient externalMcpClient,
    ICacheService cacheService,
    IEmbeddingService embeddingService,
    IOptions<ExternalMcpOptions> externalMcpOptions) : IManagedToolService
{
    /// <summary>
    /// Ranks actual lexical and same-model semantic matches with reciprocal ranks.
    /// </summary>
    /// <param name="need">The generalized capability need.</param>
    /// <param name="cancellationToken">Cancels registry and local inference work.</param>
    /// <returns>At most five neutral candidates with opaque references.</returns>
    public async Task<JsonElement> SearchAsync(
        string need,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidates = await externalToolRegistry.GetCandidatesAsync(cancellationToken);
            var query = await embeddingService.EncodeQueryAsync(need, cancellationToken);
            var rankingCandidates = candidates.Adapt<ManagedToolRankingCandidateModel[]>();
            var ranking = ManagedToolRankingPolicy.Rank(need, rankingCandidates, query);

            // Bind the selected input instance so repeated upstream identities retain their exact reviewed definition.
            IEqualityComparer<ManagedToolRankingCandidateModel> identityComparer = ReferenceEqualityComparer.Instance;
            var byRankInput = rankingCandidates.Zip(candidates)
                .ToDictionary(pair => pair.First, pair => pair.Second, identityComparer);

            // Application chooses the order; private routing stays bound to the same current registry snapshot.
            var result = new List<ManagedToolCandidateResponseDto>();
            foreach (var match in ranking)
            {
                var candidate = byRankInput[match.Candidate];
                var toolRef = IntegrationInfrastructureConstants.TOOL_REFERENCE_PREFIX +
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(24)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
                await cacheService.SetAsync(
                    RouteKey(toolRef),
                    CreateRoute(candidate),
                    new CacheEntryPolicy(
                        TimeSpan.FromMinutes(IntegrationInfrastructureConstants.SNAPSHOT_LIFETIME_MINUTES),
                        TimeSpan.FromSeconds(IntegrationInfrastructureConstants.LOCAL_CACHE_SECONDS)),
                    cancellationToken);

                var required = candidate.Schema.TryGetProperty("required", out var fields)
                    ? fields.EnumerateArray().Select(value => value.GetString()!).ToArray() : [];
                result.Add(candidate.Review.Adapt<ManagedToolCandidateResponseDto>() with
                {
                    ToolRef = toolRef,
                    MatchReasons = match.MatchReasons,
                    InputOverview = required
                });
            }

            return JsonSerializer.SerializeToElement(new ManagedToolSearchResponseDto(result), McpApplicationConstants.JSON_OPTIONS);
        }
        catch (DependencyUnavailableException)
        {
            return Error(IntegrationErrorConstants.MANAGED_TOOL_INDEX_UNAVAILABLE, true);
        }
    }

    /// <summary>
    /// Describes only a current permitted reference and its exact closed schema.
    /// </summary>
    /// <param name="toolRef">The random bounded reference.</param>
    /// <param name="cancellationToken">Cancels current state and cache reads.</param>
    /// <returns>The provider-neutral definition.</returns>
    public async Task<JsonElement> DescribeAsync(
        string toolRef,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidate = await ResolveAsync(toolRef, false, cancellationToken);
            if (candidate is null)
            {
                return Error(IntegrationErrorConstants.MANAGED_TOOL_NOT_FOUND);
            }

            var response = candidate.Review.Adapt<ManagedToolDescriptionResponseDto>() with
            {
                ToolRef = toolRef,
                InputSchema = candidate.Schema,
                SideEffect = IntegrationInfrastructureConstants.NO_SIDE_EFFECT
            };

            return JsonSerializer.SerializeToElement(response, McpApplicationConstants.JSON_OPTIONS);
        }
        catch (DependencyUnavailableException)
        {
            return Error(IntegrationErrorConstants.MANAGED_TOOL_REGISTRY_UNAVAILABLE, true);
        }
    }

    /// <summary>
    /// Rechecks current routing and schema before one native read-only call.
    /// </summary>
    /// <param name="toolRef">The exact reviewed routing reference.</param>
    /// <param name="arguments">The bounded argument object.</param>
    /// <param name="cancellationToken">Cancels current checks and the native call.</param>
    /// <returns>The bounded neutral result or declared tool error.</returns>
    public async Task<JsonElement> CallAsync(
        string toolRef,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            var candidate = await ResolveAsync(toolRef, true, cancellationToken);
            if (candidate is null)
            {
                return Error(IntegrationErrorConstants.MANAGED_TOOL_DEFINITION_CHANGED);
            }
            if (!ExternalToolSchemaHelper.IsValid(candidate.Schema, arguments) || ContainsPrivateInput(arguments.GetRawText()))
            {
                return Error(IntegrationErrorConstants.MANAGED_TOOL_INPUT_INVALID);
            }
            var native = await externalMcpClient.CallAsync(candidate.Config.Url, candidate.Tool.Name, arguments, cancellationToken);
            if (native.TryGetProperty("isError", out var failed) && failed.GetBoolean())
            {
                return Error(IntegrationErrorConstants.MANAGED_TOOL_UNAVAILABLE, true);
            }
            var content = new List<JsonElement>();
            if (native.TryGetProperty("content", out var blocks))
            {
                if (blocks.ValueKind != JsonValueKind.Array ||
                    blocks.GetArrayLength() > IntegrationInfrastructureConstants.MAX_CONTENT_BLOCKS)
                {
                    return Error(IntegrationErrorConstants.MANAGED_TOOL_RESPONSE_INVALID);
                }
                foreach (var block in blocks.EnumerateArray())
                {
                    if (block.ValueKind != JsonValueKind.Object || !block.TryGetProperty("type", out var type) ||
                        type.ValueKind != JsonValueKind.String ||
                        type.GetString() != IntegrationInfrastructureConstants.TEXT_CONTENT_TYPE ||
                        !block.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String ||
                        text.GetString()!.Length > IntegrationInfrastructureConstants.MAX_TEXT_CHARACTERS)
                    {
                        return Error(IntegrationErrorConstants.MANAGED_TOOL_RESPONSE_INVALID);
                    }
                    content.Add(JsonSerializer.SerializeToElement(
                        new ManagedToolTextContentResponseDto(IntegrationInfrastructureConstants.TEXT_CONTENT_TYPE, text.GetString()!),
                        McpApplicationConstants.JSON_OPTIONS));
                }
            }
            if (native.TryGetProperty("structuredContent", out var structured))
            {
                if (candidate.Tool.OutputSchema is not { } outputSchema ||
                    !ExternalToolSchemaHelper.IsValidOutput(outputSchema, structured))
                {
                    return Error(IntegrationErrorConstants.MANAGED_TOOL_RESPONSE_INVALID);
                }
                content.Add(JsonSerializer.SerializeToElement(
                    new ManagedToolJsonContentResponseDto(IntegrationInfrastructureConstants.JSON_CONTENT_TYPE, structured),
                    McpApplicationConstants.JSON_OPTIONS));
            }
            var projected = JsonSerializer.SerializeToElement(content);
            var projectedJson = projected.GetRawText();
            if (content.Count is < 1 or > IntegrationInfrastructureConstants.MAX_CONTENT_BLOCKS ||
                Encoding.UTF8.GetByteCount(projectedJson) > IntegrationInfrastructureConstants.MAX_RESPONSE_BYTES ||
                RevealsRouting(projectedJson, candidate))
            {
                return Error(IntegrationErrorConstants.MANAGED_TOOL_RESPONSE_INVALID);
            }
            return JsonSerializer.SerializeToElement(
                new ManagedToolCallResponseDto(toolRef, content, DateTimeOffset.UtcNow),
                McpApplicationConstants.JSON_OPTIONS);
        }
        catch (DependencyUnavailableException)
        {
            return Error(IntegrationErrorConstants.MANAGED_TOOL_UNAVAILABLE, true);
        }
    }

    /// <summary>
    /// Runs the reviewed documentation pair with at most two upstream tool calls.
    /// </summary>
    /// <param name="request">The validated public library request.</param>
    /// <param name="cancellationToken">Cancels current checks, native calls and cache work.</param>
    /// <returns>The bounded neutral documentation payload.</returns>
    public async Task<JsonElement> DocumentationSearchAsync(
        LibraryDocumentationSearchRequestDto request,
        CancellationToken cancellationToken)
    {
        var library = request.LibraryName;
        var topic = request.Topic;
        var version = request.Version;
        if (ContainsPrivateInput(library) || ContainsPrivateInput(topic) || version is not null && ContainsPrivateInput(version))
        {
            return Error(IntegrationErrorConstants.VALIDATION_ERROR);
        }
        try
        {
            var candidates = await externalToolRegistry.GetCandidatesAsync(cancellationToken);
            var resolver = candidates.FirstOrDefault(value =>
                value.Review.DocumentationRole == IntegrationInfrastructureConstants.CONTEXT7_RESOLVER_ROLE &&
                value.Tool.Name == IntegrationInfrastructureConstants.CONTEXT7_RESOLVER_TOOL);
            var query = resolver is null ? null : candidates.FirstOrDefault(value =>
                value.Connection.Id == resolver.Connection.Id &&
                value.Review.DocumentationRole == IntegrationInfrastructureConstants.CONTEXT7_QUERY_ROLE &&
                value.Tool.Name == IntegrationInfrastructureConstants.CONTEXT7_QUERY_TOOL);
            if (resolver is null || query is null || !TryDocumentationCacheKey(request, resolver, query, out var cacheKey))
            {
                return Error(IntegrationErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE, true);
            }

            var cached = await cacheService.GetAsync<JsonElement?>(cacheKey!, cancellationToken);
            if (cached is { } hit)
            {
                return hit;
            }

            var resolveArgs = JsonSerializer.SerializeToElement(
                new Context7LibraryResolveRequestModel(library, topic),
                IntegrationJsonOptions.CANONICAL);
            if (!ExternalToolSchemaHelper.IsValid(resolver.Schema, resolveArgs))
            {
                return Error(IntegrationErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE, true);
            }
            var resolved = await externalMcpClient.CallAsync(resolver.Config.Url, resolver.Tool.Name, resolveArgs, cancellationToken);
            var selected = Context7DocumentationHelper.SelectLibrary(ReadText(resolved), library, version);
            if (selected is null)
            {
                return Error(IntegrationErrorConstants.DOCUMENTATION_NOT_FOUND);
            }

            var queryArgs = JsonSerializer.SerializeToElement(
                new Context7DocumentationRequestModel(selected.Value.Id, topic),
                IntegrationJsonOptions.CANONICAL);
            if (!ExternalToolSchemaHelper.IsValid(query.Schema, queryArgs))
            {
                return Error(IntegrationErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE, true);
            }
            var native = await externalMcpClient.CallAsync(query.Config.Url, query.Tool.Name, queryArgs, cancellationToken);
            var documents = Context7DocumentationHelper.ReadDocuments(ReadText(native));
            if (documents.Count == 0)
            {
                return Error(IntegrationErrorConstants.DOCUMENTATION_NOT_FOUND);
            }

            // Omit only the unspecified version; other MCP null handling remains unchanged.
            var output = JsonSerializer.SerializeToElement(
                new LibraryDocumentationResponseDto(
                    new LibraryDocumentationLibraryResponseDto(selected.Value.Name, version),
                    documents,
                    DateTimeOffset.UtcNow),
                McpApplicationConstants.JSON_OPTIONS);
            var outputJson = output.GetRawText();
            if (Encoding.UTF8.GetByteCount(outputJson) > IntegrationInfrastructureConstants.MAX_DOCUMENTATION_RESPONSE_BYTES ||
                RevealsRouting(outputJson, resolver) || RevealsRouting(outputJson, query))
            {
                return Error(IntegrationErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE, true);
            }

            await cacheService.SetAsync(
                cacheKey!,
                (JsonElement?)output,
                new CacheEntryPolicy(
                    TimeSpan.FromMinutes(IntegrationInfrastructureConstants.DOCUMENTATION_CACHE_MINUTES),
                    TimeSpan.FromSeconds(IntegrationInfrastructureConstants.LOCAL_CACHE_SECONDS)),
                cancellationToken);
            return output;
        }
        catch (DependencyUnavailableException)
        {
            return Error(IntegrationErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE, true);
        }
    }

    /// <summary>
    /// Resolves only a bounded opaque key before current-state checks.
    /// </summary>
    /// <param name="toolRef">The supplied non-authoritative handle.</param>
    /// <param name="refresh">Whether to verify the latest provider definition.</param>
    /// <param name="cancellationToken">Cancels cache and authority reads.</param>
    /// <returns>The current reviewed candidate or null.</returns>
    private async Task<ExternalToolCandidateModel?> ResolveAsync(
        string toolRef,
        bool refresh,
        CancellationToken cancellationToken)
    {
        if (toolRef.Length is < 8 or > 128 ||
            !toolRef.StartsWith(IntegrationInfrastructureConstants.TOOL_REFERENCE_PREFIX, StringComparison.Ordinal))
        {
            return null;
        }
        var route = await cacheService.GetAsync<ExternalToolRouteModel>(RouteKey(toolRef), cancellationToken);
        return route is null ? null : await externalToolRegistry.ResolveAsync(route, refresh, cancellationToken);
    }

    /// <summary>
    /// Binds references to exact configuration, definition and expiration.
    /// </summary>
    /// <param name="candidate">The currently reviewed candidate.</param>
    /// <returns>The private routing binding.</returns>
    private static ExternalToolRouteModel CreateRoute(ExternalToolCandidateModel candidate) => new(
        candidate.Connection.Id,
        candidate.Tool.Name,
        candidate.Tool.DefinitionHash,
        ExternalToolRegistry.Hash(candidate.Connection.Config),
        DateTimeOffset.UtcNow.AddMinutes(IntegrationInfrastructureConstants.SNAPSHOT_LIFETIME_MINUTES));

    /// <summary>
    /// Names a routing handle without including provider metadata.
    /// </summary>
    /// <param name="reference">The non-authoritative opaque handle.</param>
    /// <returns>The native cache key.</returns>
    private static CacheKey RouteKey(string reference) => CacheKey.Create("external.routes", 1, reference);

    /// <summary>
    /// Rejects known private markers without claiming to infer every sensitive natural-language input.
    /// </summary>
    /// <param name="value">The bounded request text.</param>
    /// <returns>Whether a deterministic forbidden marker is present.</returns>
    private static bool ContainsPrivateInput(string value) =>
        value.Contains("-----BEGIN", StringComparison.Ordinal) || value.Contains("Bearer ", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("password=", StringComparison.OrdinalIgnoreCase) || value.Contains("api_key=", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("token=", StringComparison.OrdinalIgnoreCase) || value.Contains("\\\\", StringComparison.Ordinal) ||
        Regex.IsMatch(
            value,
            @"[A-Za-z]:\\|(?:^|\s)/(?:home|Users|etc|var|tmp)/",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

    /// <summary>
    /// Prevents known private routing identifiers from crossing the public projection.
    /// </summary>
    /// <param name="text">The projected public content.</param>
    /// <param name="candidate">The current private routing metadata.</param>
    /// <returns>Whether a private routing marker is present.</returns>
    private static bool RevealsRouting(string text, ExternalToolCandidateModel candidate) =>
        text.Contains(candidate.Config.Url, StringComparison.OrdinalIgnoreCase) ||
        text.Contains(new Uri(candidate.Config.Url).Host, StringComparison.OrdinalIgnoreCase) ||
        text.Contains(candidate.Tool.Name, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads only successful native text results for the reviewed documentation adapter.
    /// </summary>
    /// <param name="result">The bounded native result.</param>
    /// <returns>The joined reference text.</returns>
    private static string ReadText(JsonElement result)
    {
        if (result.TryGetProperty("isError", out var failed) && failed.GetBoolean())
        {
            throw new DependencyUnavailableException(IntegrationErrorConstants.DOCUMENTATION_PROVIDER_UNAVAILABLE);
        }
        return string.Join(
            '\n',
            result.GetProperty("content").EnumerateArray()
            .Where(value => value.GetProperty("type").GetString() == IntegrationInfrastructureConstants.TEXT_CONTENT_TYPE)
            .Select(value => value.GetProperty("text").GetString()));
    }

    /// <summary>
    /// Uses a keyed fingerprint so cache keys cannot enumerate documentation queries.
    /// </summary>
    /// <param name="request">The validated query object.</param>
    /// <param name="resolver">The current reviewed resolver.</param>
    /// <param name="query">The current reviewed query operation.</param>
    /// <param name="key">The keyed cache identifier.</param>
    /// <returns>Whether a valid deployment-owned fingerprint key exists.</returns>
    private bool TryDocumentationCacheKey(
        LibraryDocumentationSearchRequestDto request,
        ExternalToolCandidateModel resolver,
        ExternalToolCandidateModel query,
        out CacheKey? key)
    {
        key = null;
        Span<byte> secret = stackalloc byte[256];
        if (!Convert.TryFromBase64String(externalMcpOptions.Value.DocumentationCacheKey, secret, out var count) || count < 32)
        {
            return false;
        }
        var normalized = JsonSerializer.Serialize(new
        {
            library = request.LibraryName.Trim(),
            topic = request.Topic.Trim(),
            version = request.Version,
            resolver = resolver.Tool.DefinitionHash,
            query = query.Tool.DefinitionHash,
            configuration = ExternalToolRegistry.Hash(resolver.Connection.Config)
        });
        var fingerprint = Convert.ToHexStringLower(HMACSHA256.HashData(secret[..count], Encoding.UTF8.GetBytes(normalized)));
        CryptographicOperations.ZeroMemory(secret);
        key = CacheKey.Create("external.documentation", 1, fingerprint);
        return true;
    }

    /// <summary>
    /// Reuses the common closed MCP error factory with a fixed safe message.
    /// </summary>
    /// <param name="code">The declared tool error code.</param>
    /// <param name="retryable">Whether the dependency may recover later.</param>
    /// <returns>The closed safe error payload.</returns>
    private static JsonElement Error(string code, bool retryable = false) =>
        JsonSerializer.SerializeToElement(McpErrorFactory.Create(code, McpErrorConstants.REQUEST_FAILED, retryable));
}
