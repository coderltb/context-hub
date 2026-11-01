namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Locks managed-search ranking through the public facade before moving its pure policy.
/// </summary>
[Collection(nameof(GlobalMappingCollection))]
public sealed class ManagedToolRankingTests
{
    /// <summary>
    /// Combines actual branch ranks and excludes vectors from incompatible model identities or dimensions.
    /// </summary>
    /// <returns>The independently calculated RRF ordering and public reason assertions.</returns>
    [Fact]
    public async Task ReciprocalRanksPreserveActualBranchesAndEmbeddingCompatibility()
    {
        var candidates = new[]
        {
            Candidate(1, "first", "alpha beta", [0, 1]),
            Candidate(2, "second", "alpha", [1, 0]),
            Candidate(3, "third", "unrelated", [1, 0]),
            Candidate(4, "old-model", "unrelated", [1, 0], "old"),
            Candidate(5, "wrong-dimensions", "unrelated", [1, 0, 0])
        };
        using var services = CreateServices();
        var service = CreateFacade(services, candidates, new EmbeddingResultModel([1, 0], "current"));

        var response = await service.SearchAsync("alpha beta", CancellationToken.None);
        var ranked = response.GetProperty("candidates").EnumerateArray().ToArray();

        // Second has ranks 2+1, first has 1+3, and third appears only in the semantic branch.
        Assert.Equal(new[] { "alpha", "alpha beta", "unrelated" }, ranked.Select(value => value.GetProperty("title").GetString()));
        Assert.Equal(new[] { "keyword_match", "semantic_match" },
            ranked[0].GetProperty("match_reasons").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("semantic_match", Assert.Single(ranked[2].GetProperty("match_reasons").EnumerateArray()).GetString());
        Assert.DoesNotContain("upstream.example", response.GetRawText(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Falls back to lexical matches with stable identity ordering and a five-result cap.
    /// </summary>
    /// <returns>The bounded ordering and reason assertions.</returns>
    [Fact]
    public async Task LexicalFallbackUsesStableTieBreakAndKeepsOnlyFiveResults()
    {
        var candidates = Enumerable.Range(1, 7).Reverse()
            .Select(index => Candidate(index, "lookup", "Alpha " + index, null)).ToArray();
        using var services = CreateServices();
        var service = CreateFacade(services, candidates, null);

        var response = await service.SearchAsync("ALPHA alpha", CancellationToken.None);
        var ranked = response.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal(new[] { "Alpha 1", "Alpha 2", "Alpha 3", "Alpha 4", "Alpha 5" },
            ranked.Select(value => value.GetProperty("title").GetString()));
        Assert.All(ranked, value => Assert.Equal("keyword_match", Assert.Single(value.GetProperty("match_reasons").EnumerateArray()).GetString()));
    }

    /// <summary>
    /// Leaves unmatched metadata out when local inference is unavailable.
    /// </summary>
    /// <returns>The empty public candidate array.</returns>
    [Fact]
    public async Task MissingEmbeddingDoesNotInventMatches()
    {
        using var services = CreateServices();
        var service = CreateFacade(services, [Candidate(1, "lookup", "unrelated", null)], null);

        var response = await service.SearchAsync("alpha", CancellationToken.None);

        Assert.Empty(response.GetProperty("candidates").EnumerateArray());
    }

    /// <summary>
    /// Orders equal ranks by ordinal tool name when the connection identity is the same.
    /// </summary>
    /// <returns>The deterministic same-connection order.</returns>
    [Fact]
    public async Task EqualConnectionRanksUseOrdinalToolName()
    {
        using var services = CreateServices();
        var service = CreateFacade(services,
            [Candidate(1, "b", "Alpha b", null), Candidate(1, "a", "Alpha a", null)], null);

        var response = await service.SearchAsync("alpha", CancellationToken.None);

        Assert.Equal(new[] { "Alpha a", "Alpha b" }, response.GetProperty("candidates").EnumerateArray()
            .Select(candidate => candidate.GetProperty("title").GetString()));
    }

    /// <summary>
    /// Retains the original single ranked identity when a registry snapshot repeats the same reviewed definition.
    /// </summary>
    /// <returns>The existing deduplicated public search result.</returns>
    [Fact]
    public async Task RepeatedReviewedDefinitionDoesNotIntroduceADictionaryFailure()
    {
        var candidate = Candidate(1, "lookup", "Alpha", null);
        using var services = CreateServices();
        var service = CreateFacade(services, [candidate, candidate], null);

        var response = await service.SearchAsync("alpha", CancellationToken.None);

        Assert.Equal("Alpha", Assert.Single(response.GetProperty("candidates").EnumerateArray()).GetProperty("title").GetString());
    }

    /// <summary>
    /// Records a match reason only for a branch that retained the candidate within its own limit.
    /// </summary>
    /// <returns>The independently ranked tail candidate and branch-reason assertions.</returns>
    [Fact]
    public async Task CandidateOutsideLexicalBranchLimitCanStillMatchOnlySemanticBranch()
    {
        var candidates = Enumerable.Range(1, 1000).Select(index => Candidate(index, "lookup", "Alpha " + index, null))
            .Append(Candidate(1001, "semantic-tail", "Alpha tail", [1, 0])).ToArray();
        using var services = CreateServices();
        var service = CreateFacade(services, candidates, new EmbeddingResultModel([1, 0], "current"));

        var response = await service.SearchAsync("alpha", CancellationToken.None);
        var ranked = response.GetProperty("candidates").EnumerateArray().ToArray();

        Assert.Equal(5, ranked.Length);
        Assert.Equal("Alpha 1", ranked[0].GetProperty("title").GetString());
        Assert.Equal("Alpha tail", ranked[1].GetProperty("title").GetString());
        Assert.Equal("semantic_match", Assert.Single(ranked[1].GetProperty("match_reasons").EnumerateArray()).GetString());
    }

    /// <summary>
    /// Uses the real shared cache and global mappings without starting a provider connection.
    /// </summary>
    /// <returns>The fixture-owned container.</returns>
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCache();
        services.AddMapping();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Substitutes only the IO ports used by managed search.
    /// </summary>
    /// <param name="services">The fixture-owned shared services.</param>
    /// <param name="candidates">The eligible current registry snapshot.</param>
    /// <param name="query">The local encoder result, or null for lexical fallback.</param>
    /// <returns>The real managed-search facade.</returns>
    private static ManagedToolService CreateFacade(
        ServiceProvider services,
        IReadOnlyList<ExternalToolCandidateModel> candidates,
        EmbeddingResultModel? query)
    {
        var registry = DispatchProxy.Create<IExternalToolRegistry, CandidateRegistry>();
        ((CandidateRegistry)(object)registry).Candidates = candidates;
        return new(registry, DispatchProxy.Create<IExternalMcpClient, UnexpectedClient>(),
            services.GetRequiredService<ICacheService>(), new FixedEmbedding(query), Options.Create(new ExternalMcpOptions()));
    }

    /// <summary>
    /// Builds independently identified candidates without leaking private routing into public metadata.
    /// </summary>
    /// <param name="identity">The stable connection identifier suffix.</param>
    /// <param name="name">The private tool name used for tie-breaking.</param>
    /// <param name="title">The approved public title.</param>
    /// <param name="vector">The optional indexed vector.</param>
    /// <param name="model">The vector's exact model identity.</param>
    /// <returns>The current independently reviewed candidate.</returns>
    private static ExternalToolCandidateModel Candidate(int identity, string name, string title, float[]? vector, string model = "current")
    {
        var schema = JsonSerializer.SerializeToElement(new { type = "object" });
        return new(
            new IntegrationExternalMcp { Id = Guid.Parse($"00000000-0000-0000-0000-{identity:000000000000}") },
            new ExternalMcpConfigDto { Url = "https://upstream.example/mcp" },
            new ExternalToolModel(name, null, null, schema, "reviewed", true) { Embedding = vector, EmbeddingModel = model },
            new ReviewedExternalToolOptions { Title = title, Summary = "Public documentation." }, schema);
    }

    /// <summary>
    /// Returns one deterministic local-query vector without running a model artifact.
    /// </summary>
    /// <param name="result">The independently supplied encoder result.</param>
    private sealed class FixedEmbedding(EmbeddingResultModel? result) : IEmbeddingService
    {
        public Task<EmbeddingResultModel?> EncodeQueryAsync(string text, CancellationToken token) => Task.FromResult(result);
        public Task<EmbeddingResultModel?> EncodePassageAsync(string text, CancellationToken token) => throw new NotSupportedException();
    }

    /// <summary>
    /// Supplies the current candidate snapshot without implementing provider discovery.
    /// </summary>
    public class CandidateRegistry : DispatchProxy
    {
        /// <summary>
        /// Gets or sets the fixture's independently reviewed candidates.
        /// </summary>
        public IReadOnlyList<ExternalToolCandidateModel> Candidates { get; set; } = [];

        /// <summary>
        /// Supports the search snapshot read and rejects unrelated registry operations.
        /// </summary>
        /// <param name="targetMethod">The requested registry operation.</param>
        /// <param name="args">The unused cancellation arguments.</param>
        /// <returns>The current candidate snapshot.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod!.Name == nameof(IExternalToolRegistry.GetCandidatesAsync)
                ? Task.FromResult(Candidates) : throw new NotSupportedException();
    }

    /// <summary>
    /// Detects an accidental provider call during a local ranking operation.
    /// </summary>
    public class UnexpectedClient : DispatchProxy
    {
        /// <summary>
        /// Rejects every native client call because search must only rank registry metadata.
        /// </summary>
        /// <param name="targetMethod">The unexpected provider method.</param>
        /// <param name="args">The unused provider inputs.</param>
        /// <returns>No value because provider calls are forbidden in this fixture.</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException("Ranking must not invoke an upstream tool.");
    }
}
