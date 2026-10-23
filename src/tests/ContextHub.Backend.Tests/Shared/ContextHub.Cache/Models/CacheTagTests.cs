namespace ContextHub.Backend.Tests.Shared.ContextHub.Cache.Models;

/// <summary>
/// Requires native tag support while keeping authority and invalidation decisions in the calling use case.
/// </summary>
public sealed class CacheTagTests
{
    /// <summary>
    /// Requires a snapshot policy and explicit invalidation entry point before exercising native behavior.
    /// </summary>
    [Fact]
    public void TagContract_IsAvailable()
    {
        Assert.NotNull(typeof(CacheEntryPolicy).GetProperty("Tags"));
        Assert.NotNull(typeof(ICacheService).GetMethod("RemoveByTagAsync"));
    }

    /// <summary>
    /// Freezes ordinal tags so later caller mutations cannot change stored invalidation groups.
    /// </summary>
    [Fact]
    public void Policy_SnapshotsAndDeduplicatesTags()
    {
        var tags = new List<string> { "catalog", "catalog", "Catalog" };
        var policy = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), tags);
        tags.Clear();

        Assert.Equal(["catalog", "Catalog"], policy.Tags);
        Assert.Throws<NotSupportedException>(() => ((IList<string>)policy.Tags).Add("changed"));
    }

    /// <summary>
    /// Rejects empty and wildcard identifiers before any native invalidation.
    /// </summary>
    /// <param name="tag">The deliberately invalid tag.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("catalog entries")]
    [InlineData("catalog:*")]
    public void Policy_RejectsInvalidTags(string? tag)
    {
        Assert.Throws<ArgumentException>(() => new CacheEntryPolicy(
            TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), [tag!]));
    }

    /// <summary>
    /// Proves both cache write paths use native tags without evicting unrelated entries.
    /// </summary>
    /// <returns>A task that completes after the scenario's operations and assertions have finished.</returns>
    [Fact]
    public async Task NativeInvalidation_RecomputesTaggedEntriesOnly()
    {
        await using var provider = new ServiceCollection().AddLogging().AddCache().BuildServiceProvider();
        var cache = provider.GetRequiredService<ICacheService>();
        var tagged = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1), ["catalog"]);
        var other = new CacheEntryPolicy(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(1));
        var first = CacheKey.Create("catalog", 1, "one");
        var second = CacheKey.Create("catalog", 1, "two");
        var untouched = CacheKey.Create("other", 1, "one");
        await cache.SetAsync(first, "old", tagged);
        await cache.GetOrCreateAsync(second, _ => ValueTask.FromResult("old"), tagged);
        await cache.SetAsync(untouched, "keep", other);

        await cache.RemoveByTagAsync("catalog");

        Assert.Equal("new", await cache.GetOrCreateAsync(first, _ => ValueTask.FromResult("new"), tagged));
        Assert.Equal("new", await cache.GetOrCreateAsync(second, _ => ValueTask.FromResult("new"), tagged));
        Assert.Equal("keep", await cache.GetOrCreateAsync(untouched, _ => ValueTask.FromResult("new"), other));
        await Assert.ThrowsAsync<ArgumentException>(() => cache.RemoveByTagAsync("*").AsTask());
    }
}
