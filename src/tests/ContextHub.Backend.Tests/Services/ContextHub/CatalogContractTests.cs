using MackySoft.Json.Canonicalization;
using ContextHub.Application.Policies.Catalog;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Verifies stable selection, strict source parsing, immutable hashing and public projection contracts.
/// </summary>
public sealed class CatalogContractTests
{
    /// <summary>
    /// Rejects prerelease, metadata and noncanonical versions while accepting arbitrary-size stable components.
    /// </summary>
    /// <param name="value">The proposed version.</param>
    /// <param name="expected">Whether the stable contract permits it.</param>
    [Theory]
    [InlineData("1.2.3", true)]
    [InlineData("0.0.0", true)]
    [InlineData("999999999999999999999999.2.3", true)]
    [InlineData("1.2.3-preview.1", false)]
    [InlineData("1.2.3+build.7", false)]
    [InlineData("01.2.3", false)]
    [InlineData("1.2", false)]
    [InlineData(" 1.2.3", false)]
    public void StableVersion_UsesExactCatalogContract(string value, bool expected) =>
        Assert.Equal(expected, CatalogVersionHelper.IsStable(value));

    /// <summary>
    /// Compares release components numerically rather than lexically or through Int32 conversion.
    /// </summary>
    [Fact]
    public void VersionOrdering_HandlesNumericAndLargeComponents()
    {
        Assert.True(CatalogVersionHelper.Compare("1.10.0", "1.9.99") > 0);
        Assert.True(CatalogVersionHelper.Compare("99999999999999999999.0.0", "2147483647.9.9") > 0);
    }

    /// <summary>
    /// Keeps property order and publication metadata outside the immutable-content fingerprint.
    /// </summary>
    [Fact]
    public void CanonicalHash_IgnoresOrderAndServerOwnedFields()
    {
        var validator = new CatalogContentValidator();
        var first = JsonNode.Parse("""{"package_id":"contexthub.review","content_hash":"old","published_at":"2026-01-01T00:00:00Z","dependencies":[]}""")!.AsObject();
        var second = JsonNode.Parse("""{"dependencies":[],"published_at":"2026-10-01T00:00:00Z","content_hash":"new","package_id":"contexthub.review"}""")!.AsObject();
        var firstBundle = JsonNode.Parse("""{"b":2,"a":1}""")!.AsObject();
        var secondBundle = JsonNode.Parse("""{"a":1,"b":2}""")!.AsObject();

        Assert.Equal(validator.ComputeHash(first, firstBundle), validator.ComputeHash(second, secondBundle));
        secondBundle["a"] = 3;
        Assert.NotEqual(validator.ComputeHash(first, firstBundle), validator.ComputeHash(second, secondBundle));
    }

    /// <summary>
    /// Rejects duplicate source keys before JsonObject parsing can discard evidence of ambiguity.
    /// </summary>
    [Fact]
    public void SourceParsing_DuplicatePropertiesProduceBlockedPreview()
    {
        var source = new PackageSourceModel("""{"schema_version":"1.0","schema_version":"2.0"}""", "{}", Commit);
        var result = new CatalogContentValidator().Validate(source, Request(),
            new CatalogPackageEntity { Key = "contexthub.review", Type = "rule" }, DateTimeOffset.UtcNow);

        Assert.False(result.IsValid);
        Assert.Contains(result.Findings, finding => finding.Code == "invalid_json");
        Assert.Null(result.ContentHash);
    }

    /// <summary>
    /// Resolves embedded bundle references locally and indexes only positive public guidance.
    /// </summary>
    [Fact]
    public void ValidRule_UsesLocalSchemaAndExcludesNegativeGuidanceFromSearch()
    {
        var manifest = new JsonObject
        {
            ["schema_version"] = "1.0", ["package_id"] = "contexthub.review",
            ["package_type"] = "rule", ["version"] = "1.0.0", ["status"] = "published",
            ["content_hash"] = "sha256:" + new string('a', 64), ["published_at"] = "2026-01-01T00:00:00Z",
            ["tags"] = new JsonArray("review"), ["capabilities"] = new JsonArray("api_review"),
            ["required_capabilities"] = new JsonArray("workspace_read"),
            ["dependencies"] = new JsonArray(), ["conflicts_with"] = new JsonArray(),
            ["authoring_source"] = new JsonObject { ["repository"] = "reviewed-source", ["commit"] = Commit },
            ["upstream_references"] = new JsonArray()
        };
        var bundle = JsonNode.Parse("""
            {"schema_version":"1.0","when_to_use":"Review API changes.","when_not_to_use":"ExcludedSentinel",
             "inputs":[],"outputs":[{"name":"findings","description":"Verified findings."}],
             "instructions":"Inspect the affected source.","completion_criteria":["Report evidence."],
             "applies_to":"API changes.","requirements":["Read scope."],"violation_handling":"Report unsupported work."}
            """)!.AsObject();
        var result = new CatalogContentValidator().Validate(
            new(manifest.ToJsonString(), bundle.ToJsonString(), Commit),
            Request(), new CatalogPackageEntity { Key = "contexthub.review", Type = "rule", Name = "Review", Summary = "Reviews API changes." },
            DateTimeOffset.UtcNow);

        Assert.True(result.IsValid, string.Join(", ", result.Findings.Select(value => value.Code)));
        Assert.Contains("Review API changes.", result.SearchText);
        Assert.DoesNotContain("ExcludedSentinel", result.SearchText);

        var projection = CatalogContentValidator.PublicManifest(result.Manifest!);
        Assert.False(projection.ContainsKey("authoring_source"));
        Assert.False(projection.ContainsKey("upstream_references"));
        Assert.Equal(result.ContentHash, projection["content_hash"]!.GetValue<string>());
    }

    /// <summary>
    /// Rejects an overflowed native Skip offset before repository I/O.
    /// </summary>
    [Fact]
    public void ListValidation_RejectsOffsetOverflow()
    {
        var result = new PackageListRequestDtoValidator().Validate(new PackageListRequestDto { Page = int.MaxValue, PageSize = 100 });
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Rejects traversal and nonexact source locators without contacting the source reader.
    /// </summary>
    /// <param name="path">The proposed source directory.</param>
    [Theory]
    [InlineData("../packages/review")]
    [InlineData("packages/../review")]
    [InlineData("/packages/review")]
    [InlineData("C:/packages/review")]
    [InlineData("packages//review")]
    public void SourceValidation_RejectsUnsafePaths(string path)
    {
        var result = new PublishVersionRequestDtoValidator().Validate(Request() with { SourcePath = path });
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Keeps RFC 8785 floating-point spelling stable rather than relying on source JSON formatting.
    /// </summary>
    [Fact]
    public void CanonicalizationUsesRfc8785NumberVector()
    {
        var source = """{"numbers":[333333333.33333329,1E30,4.50,2e-3,0.000000000000000000000000001]}"""u8.ToArray();
        var expected = """{"numbers":[333333333.3333333,1e+30,4.5,0.002,1e-27]}"""u8.ToArray();
        Assert.Equal(expected, Rfc8785JsonCanonicalizer.Canonicalize(source));
    }

    /// <summary>
    /// Sorts Unicode property names by UTF-16 while preserving Unicode content without normalization.
    /// </summary>
    [Fact]
    public void CanonicalizationUsesRfc8785UnicodeOrderingVector()
    {
        var source = """{"\u20ac":1,"\r":2,"\ufb33":3,"1":4,"\ud83d\ude00":5,"\u0080":6,"\u00f6":7}"""u8.ToArray();
        var expected = """{"\r":2,"1":4,"":6,"ö":7,"€":1,"😀":5,"דּ":3}"""u8.ToArray();
        Assert.Equal(expected, Rfc8785JsonCanonicalizer.Canonicalize(source));
    }

    /// <summary>
    /// Identifies dependency and verification changes without reporting server metadata or source provenance as consumer changes.
    /// </summary>
    [Fact]
    public void PreviewDiffReportsFactualContractAreasOnly()
    {
        var baseline = JsonNode.Parse("""{"version":"1.0.0","content_hash":"old","dependencies":[],"tags":["review"]}""")!.AsObject();
        var candidate = JsonNode.Parse("""{"version":"1.1.0","content_hash":"new","dependencies":[{"package_id":"contexthub.rule","version":"1.0.0"}],"tags":["review"]}""")!.AsObject();
        var before = JsonNode.Parse("""{"instructions":"Inspect.","completion_criteria":["Evidence."]}""")!.AsObject();
        var after = JsonNode.Parse("""{"instructions":"Inspect.","completion_criteria":["Verified evidence."]}""")!.AsObject();
        var changes = CatalogPreviewPolicy.DescribeChanges(baseline, before, candidate, after);
        Assert.Equal(new[] { "/manifest/dependencies", "/bundle/completion_criteria" },
            changes.Items.Select(value => value!["path"]!.GetValue<string>()));
        Assert.Equal(new[] { "dependencies", "verification" }, changes.Areas.Select(value => value!.GetValue<string>()));
    }

    /// <summary>
    /// Treats malformed null paths and unknown or duplicate filters as validation failures before I/O.
    /// </summary>
    [Fact]
    public void InputContractsRejectNullPathAndInvalidSearchFilters()
    {
        Assert.False(new PublishVersionRequestDtoValidator().Validate(Request() with { SourcePath = null! }).IsValid);
        var validator = new CatalogSearchRequestDtoValidator();
        Assert.False(validator.Validate(new CatalogSearchRequestDto { Query = "review", Capabilities = ["unknown"] }).IsValid);
        Assert.False(validator.Validate(new CatalogSearchRequestDto { Query = "review", Tags = ["review", "review"] }).IsValid);
        Assert.True(validator.Validate(new CatalogSearchRequestDto { Query = "review", Capabilities = ["api_review"] }).IsValid);
    }

    /// <summary>
    /// Keeps all checked-in package examples valid under the bounded credential and provenance publication checks.
    /// </summary>
    /// <param name="kind">The example's closed package kind.</param>
    /// <param name="bundleFile">The checked-in bundle authority example.</param>
    [Theory]
    [InlineData("rule", "read-only-review-rule.bundle.json")]
    [InlineData("skill", "dotnet-api-review-skill.bundle.json")]
    [InlineData("agent", "dotnet-api-review-agent.bundle.json")]
    [InlineData("workflow", "dotnet-api-review-workflow.bundle.json")]
    public void DocumentedPackageExamplesPassContentChecks(string kind, string bundleFile)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Documents", "reference", "packages", "examples")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var root = Path.Combine(directory.FullName, "Documents", "reference", "packages", "examples");
        var manifests = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "dotnet-api-review.manifests.json")))!.AsArray();
        var manifest = manifests.Single(value => value!["package_type"]!.GetValue<string>() == kind)!.AsObject();
        var commit = manifest["authoring_source"]!["commit"]!.GetValue<string>();
        var request = new PublishVersionRequestDto
        {
            Version = manifest["version"]!.GetValue<string>(),
            AuthoringRepository = manifest["authoring_source"]!["repository"]!.GetValue<string>(),
            SourceCommitSha = commit,
            SourcePath = "packages/documented-review"
        };
        var result = new CatalogContentValidator().Validate(
            new(manifest.ToJsonString(), File.ReadAllText(Path.Combine(root, bundleFile)), commit), request,
            new CatalogPackageEntity
            {
                Key = manifest["package_id"]!.GetValue<string>(), Type = kind,
                Name = "Documented review package", Summary = "Evidence-based source review guidance."
            }, DateTimeOffset.UtcNow);
        Assert.True(result.IsValid, string.Join(", ", result.Findings.Select(value => value.Code)));
    }

    private const string Commit = "1111111111111111111111111111111111111111";

    private static PublishVersionRequestDto Request() =>
        new() { Version = "1.0.0", AuthoringRepository = "reviewed-source", SourceCommitSha = Commit, SourcePath = "packages/review" };
}
