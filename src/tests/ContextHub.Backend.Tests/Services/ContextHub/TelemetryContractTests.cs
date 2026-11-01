using ContextHub.Application.Policies.Telemetry;
using ContextHub.Application.Commands.Telemetry;
using ContextHub.Application.Commands.Mcp;
using ContextHub.Application.Constants.Telemetry;
using ContextHub.Application.Constants.Mcp;
using ContextHub.Application.Dtos.Telemetry;
using ContextHub.Application.Dtos.Mcp;
using ContextHub.Application.Models.Telemetry;
using ContextHub.Application.Models.Mcp;
using ContextHub.Application.Queries.Telemetry;
using ContextHub.Application.Queries.Mcp;
using ContextHub.Application.Validators.Telemetry;
using ContextHub.Infrastructure.Mappings.Telemetry;

namespace ContextHub.Backend.Tests.Services.ContextHubV1;

/// <summary>
/// Exercises fixed workflow and coded input contracts without relying on implementation-shaped expectations.
/// </summary>
public sealed class TelemetryContractTests
{
    /// <summary>
    /// Preserves missing context, stored JSON null and detached object values through the response mapping.
    /// </summary>
    /// <param name="context">The optional stored coded context.</param>
    /// <param name="hasValue">Whether a JSON value exists independently of its JSON kind.</param>
    [Theory]
    [InlineData(null, false)]
    [InlineData("null", true)]
    [InlineData("{\"issue\":\"missing_step\",\"details\":[1,true]}", true)]
    public void RecommendationMappingPreservesStoredJsonValues(string? context, bool hasValue)
    {
        var configuration = new TypeAdapterConfig();
        new TelemetryMapping().Register(configuration);
        var source = new TelemetryRecommendationEntity { Context = context };

        var response = source.Adapt<RecommendationResponseDto>(configuration);

        Assert.Equal(hasValue, response.Context.HasValue);
        if (hasValue)
        {
            Assert.Equal(context, response.Context!.Value.GetRawText());
        }
    }

    /// <summary>
    /// Keeps the exact ten-capability discovery order and the single write operation.
    /// </summary>
    [Fact]
    public void ToolVocabularyIsFixedAndOnlyQualityWrites()
    {
        Assert.Equal(new[] { "catalog_search", "package_get", "bundle_resolve", "compatibility_check", "revocation_check",
            "library_documentation_search", "managed_tool_search", "managed_tool_describe", "managed_tool_call", "quality_signal_submit" },
            ToolCatalogConstants.TOOLS.Select(value => value.Name));
        Assert.Equal("quality_signal_submit", Assert.Single(ToolCatalogConstants.TOOLS, value => !value.ReadOnly).Name);
    }

    /// <summary>
    /// Permits only accepted review paths and never reopens terminal feedback.
    /// </summary>
    [Fact]
    public void WorkflowMatchesTheFourPermittedTransitions()
    {
        var expected = new HashSet<(string, string)> { ("new", "accepted"), ("new", "dismissed"),
            ("accepted", "completed"), ("accepted", "dismissed") };
        foreach (var current in new[] { "new", "accepted", "completed", "dismissed" })
        {
            foreach (var target in new[] { "new", "accepted", "completed", "dismissed" })
            {
                Assert.Equal(expected.Contains((current, target)), RecommendationWorkflowPolicy.CanTransition(current, target));
            }
        }
    }

    /// <summary>
    /// Rejects cross-category and free-text issue codes without accepting arbitrary context.
    /// </summary>
    /// <param name="signal">The submitted signal category.</param>
    /// <param name="issue">The submitted issue code.</param>
    /// <param name="packageId">The optional public package reference.</param>
    /// <param name="version">The optional exact version.</param>
    /// <param name="valid">The independently specified contract result.</param>
    [Theory]
    [InlineData("coverage_gap", "no_matching_package", null, null, true)]
    [InlineData("coverage_gap", "instruction_incorrect", null, null, false)]
    [InlineData("coverage_gap", "no_matching_package", "demo.package", "1.0.0", false)]
    [InlineData("package_issue", "missing_step", "demo.package", "1.0.0", true)]
    [InlineData("package_issue", "missing_step", null, null, false)]
    [InlineData("package_issue", "user supplied description", "demo.package", "1.0.0", false)]
    [InlineData("client_incompatible", "capability_unavailable", "demo.package", "1.0.0", true)]
    [InlineData("client_incompatible", "missing_step", "demo.package", "1.0.0", false)]
    public void QualityCodesKeepTheirPackageBinding(string signal, string issue, string? packageId, string? version, bool valid)
    {
        var request = new QualitySignalRequestDto { SignalType = signal, IssueCode = issue, PackageId = packageId,
            Version = version, ArtifactType = "code", Operation = "review" };
        Assert.Equal(valid, new QualitySignalRequestDtoValidator().Validate(request).IsValid);
    }

    /// <summary>
    /// Prevents an integer-overflow page from reaching native Skip and becoming an internal error.
    /// </summary>
    [Fact]
    public void RecommendationOffsetIsValidatedBeforeIo()
    {
        var result = new GetRecommendationsQueryValidator().Validate(new GetRecommendationsQuery(
            new GetRecommendationsRequestDto { Page = int.MaxValue, PageSize = 100 }));
        Assert.False(result.IsValid);
    }

    /// <summary>
    /// Keeps dashboard ranges inclusive and bounded to ninety days.
    /// </summary>
    [Fact]
    public void DashboardRejectsNinetyOneInclusiveDays()
    {
        var from = new DateOnly(2026, 1, 1);
        var validator = new GetDashboardSummaryQueryValidator();
        Assert.True(validator.Validate(new GetDashboardSummaryQuery(new() { From = from, To = from.AddDays(89) })).IsValid);
        Assert.False(validator.Validate(new GetDashboardSummaryQuery(new() { From = from, To = from.AddDays(90) })).IsValid);
    }
}
