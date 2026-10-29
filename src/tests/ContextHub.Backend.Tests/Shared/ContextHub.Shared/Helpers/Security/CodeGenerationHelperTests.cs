namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Helpers.Security;

/// <summary>
/// Verifies fixed-width cryptographic numeric codes independently of service uniqueness policy.
/// </summary>
public sealed class CodeGenerationHelperTests
{
    /// <summary>
    /// Keeps every digit in a fixed-width string, rather than parsing away any leading zeroes.
    /// </summary>
    /// <param name="length">The requested digit count.</param>
    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(32)]
    public void NumericCode_HasExactWidthAndOnlyAsciiDigits(int length)
    {
        var code = CodeGenerationHelper.GenerateNumericCode(length);

        Assert.Equal(length, code.Length);
        Assert.All(code, digit => Assert.InRange(digit, '0', '9'));
    }

    /// <summary>
    /// Rejects lengths outside the documented resource bound.
    /// </summary>
    /// <param name="length">The invalid requested digit count.</param>
    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(33)]
    public void NumericCode_RejectsInvalidLength(int length)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CodeGenerationHelper.GenerateNumericCode(length));
    }

    /// <summary>
    /// Uses the same numeric generator with the chosen prefix and optional separator.
    /// </summary>
    /// <param name="separator">The optional separator, with null meaning no separator.</param>
    /// <param name="start">The independently expected formatted prefix.</param>
    [Theory]
    [InlineData("_", "INV_")]
    [InlineData("-", "INV-")]
    [InlineData(null, "INV")]
    public void GenerateCode_FormatsPrefixAndNumericSuffix(string? separator, string start)
    {
        var code = CodeGenerationHelper.GenerateCode(" INV ", 8, separator);

        Assert.StartsWith(start, code);
        Assert.Equal(start.Length + 8, code.Length);
        Assert.All(code[start.Length..], digit => Assert.InRange(digit, '0', '9'));
        Assert.StartsWith("INV_", CodeGenerationHelper.GenerateCode("INV", 1));
    }

    /// <summary>
    /// Requires a usable prefix instead of silently generating an unrelated business identifier.
    /// </summary>
    /// <param name="prefix">The missing or blank prefix.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void GenerateCode_RequiresPrefix(string? prefix)
    {
        Assert.ThrowsAny<ArgumentException>(() => CodeGenerationHelper.GenerateCode(prefix!, 4));
    }
}
