namespace ContextHub.Backend.Tests.Shared.ContextHub.ApiCommon.Routing;

/// <summary>
/// Verifies that DI supplies a stateless transformer using the existing Core kebab-case rules.
/// </summary>
public sealed class SlugifyTransformerTests
{
    /// <summary>
    /// Preserves null and empty tokens while converting names without splitting acronym letters.
    /// </summary>
    /// <param name="value">The outbound token selected by MVC.</param>
    /// <param name="expected">The expected route token.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("PackageReview", "package-review")]
    [InlineData("APIKey", "api-key")]
    [InlineData("userID", "user-id")]
    [InlineData("Already-Kebab", "already-kebab")]
    public void TransformOutbound_UsesExistingKebabCaseRules(string? value, string? expected)
    {
        var services = new ServiceCollection();
        var mvc = services.AddControllers();
        Assert.Same(mvc, mvc.AddKebabCaseRouting());

        using var provider = services.BuildServiceProvider();
        var transformer = provider.GetRequiredService<SlugifyTransformer>();

        Assert.Equal(expected, transformer.TransformOutbound(value));
        Assert.Same(transformer, provider.GetRequiredService<SlugifyTransformer>());
    }

    /// <summary>
    /// Refuses a missing MVC builder without creating a service provider or changing host routes.
    /// </summary>
    [Fact]
    public void Registration_RejectsMissingBuilder()
    {
        Assert.Throws<ArgumentNullException>(() => ((IMvcBuilder)null!).AddKebabCaseRouting());
    }
}
