namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Extensions;

/// <summary>
/// Checks optional text and per-field search before their expressions reach a database provider.
/// </summary>
public sealed class ExpressionSearchTests
{
    /// <summary>
    /// Leaves absent text optional even when no searchable fields were configured.
    /// </summary>
    /// <param name="term">The absent or whitespace-only search input.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public void BlankTextDoesNotRequireSelectors(string? term)
    {
        Assert.Null(ExpressionExtension.SearchText<SearchRow>(term));
        Assert.Null(ExpressionExtension.SearchText<SearchRow>(term, null));
    }

    /// <summary>
    /// Refuses an active keyword without valid fields rather than silently reading an unfiltered set.
    /// </summary>
    [Fact]
    public void ActiveTextRequiresValidSelectors()
    {
        Assert.Throws<ArgumentException>(() => ExpressionExtension.SearchText<SearchRow>("Alpha"));
        Assert.Throws<ArgumentException>(() => ExpressionExtension.SearchText<SearchRow>("Alpha", null));
        Assert.Throws<ArgumentException>(() => ExpressionExtension.SearchText<SearchRow>("Alpha", [null!]));
    }

    /// <summary>
    /// Searches only selected fields with OR, retaining literal whitespace, casing and nullable-field behavior.
    /// </summary>
    [Fact]
    public void TextSearchUsesLiteralContainsAcrossSelectedFields()
    {
        var predicate = ExpressionExtension.SearchText<SearchRow>("Alpha", row => row.Name, row => row.Description)!.Compile();

        Assert.True(predicate(new SearchRow { Name = "Alpha value" }));
        Assert.True(predicate(new SearchRow { Description = "Contains Alpha" }));
        Assert.False(predicate(new SearchRow { Name = "alpha", Description = null }));
        Assert.False(predicate(new SearchRow()));
        Assert.False(ExpressionExtension.SearchText<SearchRow>(" Alpha ", row => row.Name)!
            .Compile()(new SearchRow { Name = "Alpha" }));
    }

    /// <summary>
    /// Skips missing predicates without losing meaningful false, zero or explicit null comparisons.
    /// </summary>
    [Fact]
    public void FieldSearchCombinesAvailableConditionsWithAnd()
    {
        Assert.Null(ExpressionExtension.SearchFields<SearchRow>());
        Assert.Null(ExpressionExtension.SearchFields<SearchRow>(null));
        Assert.Null(ExpressionExtension.SearchFields<SearchRow>([null, null]));
        var predicate = ExpressionExtension.SearchFields<SearchRow>(null,
            row => !row.Active, row => row.Count == 0, row => row.Description == null)!.Compile();

        Assert.True(predicate(new SearchRow()));
        Assert.False(predicate(new SearchRow { Active = true }));
        Assert.False(predicate(new SearchRow { Count = 1 }));
        Assert.False(predicate(new SearchRow { Description = "Present" }));
        Assert.False(ExpressionExtension.SearchFields<SearchRow>(row => false)!.Compile()(new SearchRow()));
    }

    /// <summary>
    /// Keeps mandatory scope outside the text OR group and requires every active field condition.
    /// </summary>
    [Fact]
    public void SearchNeverBroadensTheMandatoryScope()
    {
        Expression<Func<SearchRow, bool>> scope = row => row.Count == 7;
        var text = ExpressionExtension.SearchText<SearchRow>("Alpha", row => row.Name, row => row.Description);
        var fields = ExpressionExtension.SearchFields<SearchRow>(row => row.Active);
        var predicate = scope.AndAlso(text).AndAlso(fields)!.Compile();

        Assert.True(predicate(new SearchRow { Name = "Alpha", Count = 7, Active = true }));
        Assert.True(predicate(new SearchRow { Description = "Alpha", Count = 7, Active = true }));
        Assert.False(predicate(new SearchRow { Description = "Alpha", Count = 8, Active = true }));
        Assert.False(predicate(new SearchRow { Name = "Alpha", Count = 7, Active = false }));
    }

    /// <summary>
    /// Supplies independent optional values without database or serialization behavior.
    /// </summary>
    private sealed class SearchRow
    {
        /// <summary>
        /// Holds the first optional searchable field.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Holds the second optional searchable field.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// Distinguishes zero filters from missing predicates.
        /// </summary>
        public int Count { get; init; }

        /// <summary>
        /// Distinguishes a false filter from an absent filter.
        /// </summary>
        public bool Active { get; init; }
    }
}
