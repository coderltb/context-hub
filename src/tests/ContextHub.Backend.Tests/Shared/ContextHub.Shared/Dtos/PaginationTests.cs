namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Dtos;

/// <summary>
/// Verifies pagination defaults, input bounds and response snapshots against the REST contract.
/// </summary>
public sealed class PaginationTests
{
    /// <summary>
    /// Allows inserts between the independent count and page reads while retaining structural bounds.
    /// </summary>
    [Fact]
    public void PageResponse_AllowsCountToLagItemsButKeepsPageBounds()
    {
        var page = new PageResponse<string>(["new row"], new PageRequest(), 0);

        Assert.Equal(0, page.TotalCount);
        Assert.Single(page.Items);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PageResponse<string>([], new PageRequest(), -1));
        Assert.Throws<ArgumentException>(() =>
            new PageResponse<string>(["first", "second"], new PageRequest(1, 1), 2));
    }

    /// <summary>
    /// Verifies that a default request selects the first page with twenty items and zero offset.
    /// </summary>
    [Fact]
    public void PageRequest_DefaultsMatchRestContract()
    {
        var request = new PageRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(20, request.PageSize);
        Assert.Equal(0L, request.Offset);
    }

    /// <summary>
    /// Verifies that page numbers and sizes outside the supported bounds are rejected.
    /// </summary>
    /// <param name="page">The requested one-based page number.</param>
    /// <param name="pageSize">The requested number of items per page.</param>
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void PageRequest_RejectsInvalidBounds(int page, int pageSize)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(page, pageSize));
    }

    /// <summary>
    /// Verifies that the offset remains correct when the page number reaches the Int32 limit.
    /// </summary>
    [Fact]
    public void PageRequest_OffsetDoesNotOverflowInt32()
    {
        Assert.Equal(214748364600L, new PageRequest(int.MaxValue, 100).Offset);
    }

    /// <summary>
    /// Verifies that later source-list changes cannot alter the response and that pagination fields match the wire
    /// contract.
    /// </summary>
    [Fact]
    public void PageResponse_CopiesItemsAndMatchesWireFields()
    {
        var source = new List<string> { "first" };
        var page = new PageResponse<string>(source, new PageRequest(), 1);
        source.Clear();

        var json = System.Text.Json.JsonSerializer.Serialize(page, System.Text.Json.JsonSerializerOptions.Web);
        Assert.Single(page.Items);
        Assert.Contains("\"totalCount\":1", json);
        Assert.Contains("\"pageSize\":20", json);
        Assert.DoesNotContain("success", json);
    }
}
