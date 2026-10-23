namespace ContextHub.Backend.Tests.Shared.ContextHub.Core.Helpers.Conversion;

/// <summary>
/// Checks invariant conversion and Unicode-safe text transformations.
/// </summary>
public sealed class SharedConversionTests
{
    /// <summary>
    /// Does not hide an invalid caller-supplied culture provider as malformed user input.
    /// </summary>
    [Fact]
    public void TryConvert_ProviderFailurePropagatesUnchanged()
    {
        var failure = new ArgumentException("Invalid fixture culture provider.");

        Assert.Same(failure, Assert.Throws<ArgumentException>(() =>
            StringConvertHelper.TryConvert<DateTime>("2026-10-14", out _, new FailingFormatProvider(failure))));
    }

    /// <summary>
    /// Supplies a caller configuration failure rather than a data-format failure.
    /// </summary>
    /// <param name="failure">The original provider failure that must remain observable.</param>
    private sealed class FailingFormatProvider(Exception failure) : IFormatProvider
    {
        /// <summary>
        /// Fails when the native parser requests its culture configuration.
        /// </summary>
        /// <param name="formatType">The native parser's requested format-information type.</param>
        /// <returns>No format information because the fixture raises its original failure.</returns>
        public object? GetFormat(Type? formatType) => throw failure;
    }

    /// <summary>
    /// Handles scalar boundaries without JSON fallback or culture-dependent decimal interpretation.
    /// </summary>
    [Fact]
    public void ScalarBoundaries_RejectOverflowAndSupportExplicitCulture()
    {
        Assert.True(StringConvertHelper.TryConvert<int?>(" 42 ", out var nullable));
        Assert.Equal(42, nullable);
        Assert.True(StringConvertHelper.TryConvert<DayOfWeek?>("MONDAY", out var day));
        Assert.Equal(DayOfWeek.Monday, day);
        Assert.False(StringConvertHelper.TryConvert<byte>("256", out _));
        Assert.False(StringConvertHelper.TryConvert<bool>("1", out _));
        Assert.True(StringConvertHelper.TryConvert<bool>("true", out var flag));
        Assert.True(flag);
        Assert.True(StringConvertHelper.TryConvert<char>("x", out var character));
        Assert.Equal('x', character);
        Assert.False(StringConvertHelper.TryConvert<char>("xy", out _));
        Assert.True(StringConvertHelper.TryConvert<DateOnly>("2026-10-06", out _));
        Assert.True(StringConvertHelper.TryConvert<TimeOnly>("12:30", out _));
        Assert.True(StringConvertHelper.TryConvert<TimeSpan>("01:30:00", out _));
        Assert.True(StringConvertHelper.TryConvert<decimal>("12,5", out var number,
            global::System.Globalization.CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal(12.5m, number);
        Assert.True(StringConvertHelper.TryConvert<string>("  unchanged  ", out var text));
        Assert.Equal("  unchanged  ", text);
        Assert.False(StringConvertHelper.TryConvert<Dictionary<string, string>>("{}", out _));
    }

    /// <summary>
    /// Accepts declared enum names and nullables without accepting numeric enum shortcuts.
    /// </summary>
    [Fact]
    public void Conversion_UsesDeclaredNamesAndNullableValues()
    {
        Assert.True(StringConvertHelper.TryConvert<decimal>("12.5", out var number));
        Assert.Equal(12.5m, number);
        Assert.True(StringConvertHelper.TryConvert<int?>(null, out var absent));
        Assert.Null(absent);
        Assert.False(StringConvertHelper.TryConvert<int>(null, out _));
        Assert.True(StringConvertHelper.TryConvert<DayOfWeek>("monday", out var day));
        Assert.Equal(DayOfWeek.Monday, day);
        Assert.False(StringConvertHelper.TryConvert<DayOfWeek>("1", out _));
        Assert.False(StringConvertHelper.TryConvert<DayOfWeek>("Monday,Tuesday", out _));
        Assert.True(StringConvertHelper.TryConvert<Guid>("778449c4-69a9-42e8-82fa-42c69908dd12", out _));
        Assert.False(StringConvertHelper.TryConvert<Guid>("invalid", out _));
        Assert.True(StringConvertHelper.TryConvert<DateTimeOffset>("2026-10-06T12:00:00Z", out _));
        Assert.False(StringConvertHelper.TryConvert<Uri>("https://example.com", out _));
    }

    /// <summary>
    /// Preserves complete emoji and combining marks rather than counting UTF-16 code units.
    /// </summary>
    [Fact]
    public void Truncate_PreservesTextElements()
    {
        Assert.Equal("👨‍👩‍👧‍👦á", "👨‍👩‍👧‍👦áX".Truncate(2));
        Assert.Equal("", "hello".Truncate(0));
        Assert.Equal("hello", "hello".Truncate(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => "hello".Truncate(-1));
    }

    /// <summary>
    /// Handles acronym boundaries without creating a separator for every capital letter.
    /// </summary>
    [Fact]
    public void CaseConversion_PreservesWordBoundaries()
    {
        Assert.Equal("http-client-id", "HTTPClientId".ToKebabCase());
        Assert.Equal("some-name", "Some_Name".ToKebabCase());
        Assert.Equal("httpClientId", "http-client-id".KebabToCamelCase());
        Assert.Equal("", "".ToKebabCase());
    }
}
