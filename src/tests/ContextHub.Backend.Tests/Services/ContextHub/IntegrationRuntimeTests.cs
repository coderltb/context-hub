namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises current connection authority and definition drift through the actual cached registry.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class IntegrationRuntimeTests
{
    /// <summary>
    /// Refuses an unreviewed operation even when the provider labels it read-only.
    /// </summary>
    /// <returns>The quarantined discovery assertion.</returns>
    [Fact]
    public async Task ReadOnlyAnnotationDoesNotReplaceIndependentReview()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient();
        var registry = CreateRegistry(services, repository, client, new());

        Assert.Empty(await registry.GetCandidatesAsync(CancellationToken.None));
        Assert.Equal(1, client.DiscoveryCalls);
        Assert.Equal(0, client.ToolCalls);
    }

    /// <summary>
    /// Rechecks current database configuration even when a valid snapshot and reference already exist.
    /// </summary>
    /// <returns>The disabled-authority and zero-upstream-call assertions.</returns>
    [Fact]
    public async Task CachedReferenceDoesNotBypassDisabledConnection()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient();
        var registry = CreateRegistry(services, repository, client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);
        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var reference = Assert.Single(search.GetProperty("candidates").EnumerateArray()).GetProperty("tool_ref").GetString()!;
        repository.Connection.Config = """{"schemaVersion":1,"url":"https://upstream.example/mcp","enabled":false,"toolPolicy":{"mode":"all"}}""";

        var response = await facade.DescribeAsync(reference, CancellationToken.None);

        Assert.Equal("managed_tool_not_found", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(1, client.DiscoveryCalls);
        Assert.Equal(0, client.ToolCalls);
    }

    /// <summary>
    /// Re-discovers an exact definition before dispatch instead of trusting the cached search result.
    /// </summary>
    /// <returns>The drift rejection and non-dispatch assertions.</returns>
    [Fact]
    public async Task DefinitionDriftInvalidatesReferenceBeforeCall()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient();
        var registry = CreateRegistry(services, repository, client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);
        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var reference = Assert.Single(search.GetProperty("candidates").EnumerateArray()).GetProperty("tool_ref").GetString()!;
        client.DefinitionHash = "changed-definition";

        var response = await facade.CallAsync(reference, JsonSerializer.SerializeToElement(new { query = "public reference" }), CancellationToken.None);

        Assert.Equal("managed_tool_definition_changed", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(2, client.DiscoveryCalls);
        Assert.Equal(0, client.ToolCalls);
    }

    /// <summary>
    /// Applies the closed published input schema before the one permitted native dispatch.
    /// </summary>
    /// <returns>The rejected extra-field and successful bounded-result assertions.</returns>
    [Fact]
    public async Task CallRejectsUnknownInputAndReturnsOnlyNeutralResult()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient();
        var registry = CreateRegistry(services, repository, client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);
        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var reference = Assert.Single(search.GetProperty("candidates").EnumerateArray()).GetProperty("tool_ref").GetString()!;
        var invalid = await facade.CallAsync(reference,
            JsonSerializer.SerializeToElement(new { query = "public reference", password = "private" }), CancellationToken.None);
        Assert.Equal("managed_tool_input_invalid", invalid.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, client.ToolCalls);

        var response = await facade.CallAsync(reference, JsonSerializer.SerializeToElement(new { query = "public reference" }), CancellationToken.None);

        Assert.Equal(reference, response.GetProperty("tool_ref").GetString());
        Assert.Equal("Public reference result.", Assert.Single(response.GetProperty("content").EnumerateArray()).GetProperty("text").GetString());
        Assert.DoesNotContain("upstream.example", response.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, client.ToolCalls);
    }

    /// <summary>
    /// Keeps mapped public metadata and field names independent from the provider's private definition.
    /// </summary>
    /// <returns>The exact search and describe wire-shape assertions.</returns>
    [Fact]
    public async Task SearchAndDescribePreserveTheirPublicContracts()
    {
        new ServiceCollection().AddMapping();
        using var services = CreateServices();
        var client = new NativeClient();
        var registry = CreateRegistry(services, new ConnectionRepository(), client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);

        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var candidate = Assert.Single(search.GetProperty("candidates").EnumerateArray());
        Assert.Equal(new[] { "tool_ref", "title", "summary", "match_reasons", "input_overview" },
            candidate.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Public reference", candidate.GetProperty("title").GetString());
        Assert.Equal("Find a public reference.", candidate.GetProperty("summary").GetString());
        Assert.Equal("keyword_match", Assert.Single(candidate.GetProperty("match_reasons").EnumerateArray()).GetString());
        Assert.Equal("query", Assert.Single(candidate.GetProperty("input_overview").EnumerateArray()).GetString());
        var reference = candidate.GetProperty("tool_ref").GetString()!;

        var description = await facade.DescribeAsync(reference, CancellationToken.None);

        Assert.Equal(new[] { "tool_ref", "title", "description", "input_schema", "output_overview", "side_effect" },
            description.EnumerateObject().Select(property => property.Name));
        Assert.Equal(reference, description.GetProperty("tool_ref").GetString());
        Assert.Equal("Public reference", description.GetProperty("title").GetString());
        Assert.Equal("Find a public reference.", description.GetProperty("description").GetString());
        Assert.Equal("Public reference text.", description.GetProperty("output_overview").GetString());
        Assert.Equal("none", description.GetProperty("side_effect").GetString());
        Assert.DoesNotContain("Private provider", description.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Preserves distinct text and JSON blocks after validating the provider's structured output.
    /// </summary>
    /// <returns>The exact block shape and completion metadata assertions.</returns>
    [Fact]
    public async Task ReviewedStructuredOutputKeepsSeparateTextAndJsonBlocks()
    {
        using var services = CreateServices();
        var client = new NativeClient { IncludeReviewedStructure = true };
        var registry = CreateRegistry(services, new ConnectionRepository(), client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);
        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var reference = Assert.Single(search.GetProperty("candidates").EnumerateArray()).GetProperty("tool_ref").GetString()!;

        var response = await facade.CallAsync(reference,
            JsonSerializer.SerializeToElement(new { query = "public reference" }), CancellationToken.None);

        Assert.Equal(new[] { "tool_ref", "content", "completed_at" }, response.EnumerateObject().Select(property => property.Name));
        var blocks = response.GetProperty("content").EnumerateArray().ToArray();
        Assert.Equal(2, blocks.Length);
        Assert.Equal("text", blocks[0].GetProperty("type").GetString());
        Assert.Equal("Public reference result.", blocks[0].GetProperty("text").GetString());
        Assert.False(blocks[0].TryGetProperty("value", out _));
        Assert.Equal("json", blocks[1].GetProperty("type").GetString());
        Assert.True(blocks[1].GetProperty("value").GetProperty("found").GetBoolean());
        Assert.False(blocks[1].TryGetProperty("text", out _));
        Assert.Equal(TimeSpan.Zero, response.GetProperty("completed_at").GetDateTimeOffset().Offset);
    }

    /// <summary>
    /// Keeps an unavailable unqualified documentation provider explicit without fabricating documents.
    /// </summary>
    /// <returns>The declared provider gate and zero-native-call assertions.</returns>
    [Fact]
    public async Task DocumentationWithoutQualifiedPairDoesNotCallUpstream()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient();
        var registry = CreateRegistry(services, repository, client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);

        var response = await facade.DocumentationSearchAsync(
            new() { LibraryName = "Example", Topic = "public reference" }, CancellationToken.None);

        Assert.Equal("documentation_provider_unavailable", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(0, client.ToolCalls);
    }

    /// <summary>
    /// Preserves provider argument names, optional version omission and cached public documentation.
    /// </summary>
    /// <param name="version">The requested advertised version, or null.</param>
    /// <returns>The provider-input, response-shape and bounded-call assertions.</returns>
    [Theory]
    [InlineData(null)]
    [InlineData("1.2.0")]
    public async Task DocumentationKeepsCamelCaseProviderInputAndSnakeCasePublicOutput(string? version)
    {
        using var services = CreateServices();
        var client = new NativeClient { DocumentationMode = true };
        var options = new ExternalMcpOptions
        {
            DocumentationCacheKey = Convert.ToBase64String(new byte[32]),
            ReviewedTools =
            [
                new()
                {
                    ConnectionName = "private-label", ToolName = "resolve-library-id", DefinitionHash = "approved-definition",
                    Title = "Public resolver", Summary = "Select public documentation.", OutputOverview = "Public library metadata.",
                    DocumentationRole = "context7-resolve"
                },
                new()
                {
                    ConnectionName = "private-label", ToolName = "query-docs", DefinitionHash = "approved-definition",
                    Title = "Public documentation", Summary = "Read public documentation.", OutputOverview = "Public source excerpts.",
                    DocumentationRole = "context7-query"
                }
            ]
        };
        var registry = CreateRegistry(services, new ConnectionRepository(), client, options);
        var facade = CreateFacade(services, registry, client, options);
        var request = new LibraryDocumentationSearchRequestDto { LibraryName = "Example", Topic = "public reference", Version = version };

        var response = await facade.DocumentationSearchAsync(request, CancellationToken.None);

        Assert.Equal(new[] { "libraryName", "query" }, client.Arguments[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal("Example", client.Arguments[0].GetProperty("libraryName").GetString());
        Assert.Equal(new[] { "libraryId", "query" }, client.Arguments[1].EnumerateObject().Select(property => property.Name));
        Assert.Equal(version is null ? "/owner/example" : "/owner/example/v1.2.0",
            client.Arguments[1].GetProperty("libraryId").GetString());
        Assert.Equal(new[] { "library", "documents", "retrieved_at" }, response.EnumerateObject().Select(property => property.Name));
        var library = response.GetProperty("library");
        Assert.Equal("Example", library.GetProperty("name").GetString());
        Assert.Equal(version is not null, library.TryGetProperty("version", out var actualVersion));
        if (version is not null)
        {
            Assert.Equal(version, actualVersion.GetString());
        }

        var document = Assert.Single(response.GetProperty("documents").EnumerateArray());
        Assert.Equal(new[] { "title", "url", "excerpt" }, document.EnumerateObject().Select(property => property.Name));
        Assert.Equal("Reference guide", document.GetProperty("title").GetString());
        Assert.Equal("https://docs.example/guide", document.GetProperty("url").GetString());
        Assert.Equal("Public guidance.", document.GetProperty("excerpt").GetString());
        Assert.Equal(TimeSpan.Zero, response.GetProperty("retrieved_at").GetDateTimeOffset().Offset);
        Assert.True(JsonElement.DeepEquals(response, await facade.DocumentationSearchAsync(request, CancellationToken.None)));
        Assert.Equal(2, client.ToolCalls);
    }

    /// <summary>
    /// Rejects an unreviewed structured result even if its companion text appears safe.
    /// </summary>
    /// <returns>The invalid-output assertion.</returns>
    [Fact]
    public async Task StructuredResultRequiresAReviewedOutputSchema()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient { IncludeUnreviewedStructure = true };
        var registry = CreateRegistry(services, repository, client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);
        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var reference = Assert.Single(search.GetProperty("candidates").EnumerateArray()).GetProperty("tool_ref").GetString()!;

        var response = await facade.CallAsync(reference, JsonSerializer.SerializeToElement(new { query = "public reference" }), CancellationToken.None);

        Assert.Equal("managed_tool_response_invalid", response.GetProperty("error").GetProperty("code").GetString());
        Assert.Equal(1, client.ToolCalls);
    }

    /// <summary>
    /// Refuses oversized blocks instead of returning only the smaller portion of a provider result.
    /// </summary>
    /// <returns>The fail-closed output assertion.</returns>
    [Fact]
    public async Task OversizedOutputIsNotSilentlyTruncated()
    {
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient { IncludeOversizedText = true };
        var registry = CreateRegistry(services, repository, client, ReviewedOptions());
        var facade = CreateFacade(services, registry, client);
        var search = await facade.SearchAsync("public reference", CancellationToken.None);
        var reference = Assert.Single(search.GetProperty("candidates").EnumerateArray()).GetProperty("tool_ref").GetString()!;

        var response = await facade.CallAsync(reference, JsonSerializer.SerializeToElement(new { query = "public reference" }), CancellationToken.None);

        Assert.Equal("managed_tool_response_invalid", response.GetProperty("error").GetProperty("code").GetString());
    }

    /// <summary>
    /// Preserves explicit preview eligibility while mapping the exact advertised metadata.
    /// </summary>
    /// <param name="reviewed">Whether the current definition has an independent review.</param>
    /// <returns>The unchanged metadata and current-policy eligibility assertions.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PreviewMappingPreservesMetadataAndEligibility(bool reviewed)
    {
        new ServiceCollection().AddMapping();
        using var services = CreateServices();
        var repository = new ConnectionRepository();
        var client = new NativeClient();
        var registry = CreateRegistry(services, repository, client, reviewed ? ReviewedOptions() : new());
        var configuration = ExternalToolRegistry.ParseConfig(repository.Connection.Config);

        var response = Assert.Single(await registry.PreviewAsync("private-label", configuration, CancellationToken.None));

        Assert.Equal(reviewed, response.IsEligible);
        Assert.Equal("lookup_public", response.Name);
        Assert.Equal("Private provider title", response.Title);
        Assert.Equal("Private provider description", response.Description);
        Assert.Equal("object", response.InputSchema.GetProperty("type").GetString());
        Assert.Equal("query", Assert.Single(response.InputSchema.GetProperty("required").EnumerateArray()).GetString());
        Assert.Equal(1, client.DiscoveryCalls);
        Assert.Equal(0, client.ToolCalls);
    }

    private static ServiceProvider CreateServices()
    {
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddCache();
        return collection.BuildServiceProvider();
    }

    private static ExternalToolRegistry CreateRegistry(
        ServiceProvider services,
        ConnectionRepository repository,
        NativeClient client,
        ExternalMcpOptions options) => new(
            repository, client, services.GetRequiredService<ICacheService>(), Options.Create(options), new NoEmbedding(),
            NullLogger<ExternalToolRegistry>.Instance);

    private static ManagedToolService CreateFacade(
        ServiceProvider services,
        ExternalToolRegistry registry,
        NativeClient client,
        ExternalMcpOptions? options = null) => new(
            registry, client, services.GetRequiredService<ICacheService>(), new NoEmbedding(), Options.Create(options ?? new ExternalMcpOptions()));

    private static ExternalMcpOptions ReviewedOptions() => new()
    {
        ReviewedTools =
        [
            new()
            {
                ConnectionName = "private-label", ToolName = "lookup_public", DefinitionHash = "approved-definition",
                Title = "Public reference", Summary = "Find a public reference.", OutputOverview = "Public reference text."
            }
        ]
    };

    private sealed class NoEmbedding : IEmbeddingService
    {
        public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
        public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken cancellationToken) => Task.FromResult<EmbeddingResultModel?>(null);
    }

    private sealed class NativeClient : IExternalMcpClient
    {
        public string DefinitionHash { get; set; } = "approved-definition";
        public int DiscoveryCalls { get; private set; }
        public int ToolCalls { get; private set; }
        public bool IncludeUnreviewedStructure { get; init; }
        public bool IncludeReviewedStructure { get; init; }
        public bool IncludeOversizedText { get; init; }
        public bool DocumentationMode { get; init; }
        public List<JsonElement> Arguments { get; } = [];

        public Task<IReadOnlyList<ExternalToolModel>> DiscoverAsync(string endpoint, CancellationToken cancellationToken)
        {
            DiscoveryCalls++;
            if (DocumentationMode)
            {
                return Task.FromResult<IReadOnlyList<ExternalToolModel>>(
                [
                    new("resolve-library-id", null, null, JsonSerializer.SerializeToElement(new
                    {
                        type = "object", properties = new { libraryName = new { type = "string" }, query = new { type = "string" } },
                        required = new[] { "libraryName", "query" }
                    }), DefinitionHash, true),
                    new("query-docs", null, null, JsonSerializer.SerializeToElement(new
                    {
                        type = "object", properties = new { libraryId = new { type = "string" }, query = new { type = "string" } },
                        required = new[] { "libraryId", "query" }
                    }), DefinitionHash, true)
                ]);
            }

            IReadOnlyList<ExternalToolModel> tools =
            [
                new("lookup_public", "Private provider title", "Private provider description",
                    JsonSerializer.SerializeToElement(new
                    {
                        type = "object", properties = new { query = new { type = "string" } }, required = new[] { "query" }
                    }), DefinitionHash, true)
                {
                    OutputSchema = IncludeReviewedStructure
                        ? JsonSerializer.SerializeToElement(new
                        {
                            type = "object", properties = new { found = new { type = "boolean" } }, required = new[] { "found" }
                        })
                        : null
                }
            ];
            return Task.FromResult(tools);
        }

        public Task<JsonElement> CallAsync(string endpoint, string toolName, JsonElement arguments, CancellationToken cancellationToken)
        {
            ToolCalls++;
            Arguments.Add(arguments.Clone());
            if (DocumentationMode)
            {
                var text = toolName == "resolve-library-id"
                    ? "- Title: Example\n- Context7-compatible library ID: /owner/example\n- Versions: v1.2.0"
                    : "# Reference guide\n\nSource: https://docs.example/guide\nPublic guidance.";
                return Task.FromResult(JsonSerializer.SerializeToElement(new { content = new[] { new { type = "text", text } } }));
            }

            if (IncludeOversizedText)
            {
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    content = new[] { new { type = "text", text = "Public reference result." }, new { type = "text", text = new string('x', 32_769) } }
                }));
            }
            if (IncludeUnreviewedStructure)
            {
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    content = new[] { new { type = "text", text = "Public reference result." } }, structuredContent = new { unreviewed = true }
                }));
            }
            if (IncludeReviewedStructure)
            {
                return Task.FromResult(JsonSerializer.SerializeToElement(new
                {
                    content = new[] { new { type = "text", text = "Public reference result." } },
                    structuredContent = new { found = true }
                }));
            }
            return Task.FromResult(JsonSerializer.SerializeToElement(new { content = new[] { new { type = "text", text = "Public reference result." } } }));
        }
    }

    private sealed class ConnectionRepository : IExternalConnectionRepository
    {
        public IntegrationExternalMcp Connection { get; } = new()
        {
            Id = Guid.CreateVersion7(), Name = "private-label",
            Config = """{"schemaVersion":1,"url":"https://upstream.example/mcp","enabled":true,"toolPolicy":{"mode":"all"}}"""
        };

        public Task<IReadOnlyList<IntegrationExternalMcp>> GetConnectionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IntegrationExternalMcp>>([Connection]);

        public Task<IntegrationExternalMcp?> GetByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult<IntegrationExternalMcp?>(Connection.Name == name ? Connection : null);

        public Task<IntegrationExternalMcp?> GetByIdAsync(Guid connectionId, CancellationToken cancellationToken) =>
            Task.FromResult<IntegrationExternalMcp?>(Connection.Id == connectionId ? Connection : null);

        public Task<IntegrationExternalMcp> SaveAsync(IntegrationExternalMcp connection, uint? expectedVersion, CancellationToken cancellationToken) =>
            Task.FromResult(connection);

        public Task DeleteAsync(IntegrationExternalMcp connection, uint expectedVersion, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
