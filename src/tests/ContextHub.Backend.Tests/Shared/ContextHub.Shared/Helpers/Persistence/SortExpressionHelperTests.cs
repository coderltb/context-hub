namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Helpers.Persistence;

/// <summary>
/// Locks the shared external sort grammar independently of any entity query implementation.
/// </summary>
public sealed class SortExpressionHelperTests
{
    /// <summary>
    /// Keeps omitted ordering distinct from default ordering chosen by individual request DTOs.
    /// </summary>
    /// <param name="sort">The omitted or whitespace-only input.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void EmptyInputSkipsOrdering(string? sort)
    {
        Assert.True(SortExpressionHelper.TryParse(sort, ["userName"], out var fields));
        Assert.Empty(fields);
    }

    /// <summary>
    /// Preserves canonical names, clause order and independent directions, including repeated fields.
    /// </summary>
    [Fact]
    public void MixedDirectionsRetainCanonicalFieldNamesAndOrder()
    {
        Assert.True(SortExpressionHelper.TryParse(
            " USERNAME DeSc , email, userName ASC ",
            ["userName", "email"],
            out var fields));

        Assert.Equal(
            new[] { new SortFieldModel("userName", true), new SortFieldModel("email", false), new SortFieldModel("userName", false) },
            fields);
    }

    /// <summary>
    /// Rejects malformed clauses without returning a partially accepted ordering.
    /// </summary>
    /// <param name="sort">An invalid clause list.</param>
    [Theory]
    [InlineData(",")]
    [InlineData("userName,")]
    [InlineData(",userName")]
    [InlineData("userName,,email")]
    [InlineData("userName sideways")]
    [InlineData("userName asc extra")]
    [InlineData("unknown desc")]
    [InlineData("userName asc,email desc;drop")]
    public void InvalidClauseRejectsEntireExpression(string sort)
    {
        Assert.False(SortExpressionHelper.TryParse(sort, ["userName", "email"], out var fields));
        Assert.Empty(fields);
    }

    /// <summary>
    /// Leaves field vocabulary with the consuming feature rather than accepting arbitrary property paths.
    /// </summary>
    [Fact]
    public void FeaturesKeepIndependentAllowlists()
    {
        Assert.True(SortExpressionHelper.TryParse("repositoryURL desc,lastSyncedAt", ["repositoryUrl", "lastSyncedAt"], out var fields));
        Assert.Equal(new[] { new SortFieldModel("repositoryUrl", true), new SortFieldModel("lastSyncedAt", false) }, fields);
        Assert.False(SortExpressionHelper.TryParse("repositoryUrl", ["userName", "email"], out _));
        Assert.False(SortExpressionHelper.TryParse("userName", [], out _));
    }
}
