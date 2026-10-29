namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Extensions;

/// <summary>
/// Verifies strict enum-name parsing and presentation metadata without numeric conversion or wire-name rewriting.
/// </summary>
public sealed class EnumExtensionTests
{
    /// <summary>
    /// Accepts one declared name using ordinal case-insensitive comparison and optional surrounding whitespace.
    /// </summary>
    /// <param name="input">The input value evaluated by this scenario.</param>
    [Theory]
    [InlineData("Published")]
    [InlineData("published")]
    [InlineData("  PUBLISHED  ")]
    public void TryParseDefinedName_AcceptsDeclaredName(string input)
    {
        var parsed = EnumExtension.TryParseDefinedName<PackageState>(input, out var value);

        Assert.True(parsed);
        Assert.Equal(PackageState.Published, value);
    }

    /// <summary>
    /// Rejects absent, numeric, unknown and combined values even when native enum parsing would accept them.
    /// </summary>
    /// <param name="input">The input value evaluated by this scenario.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("0x01")]
    [InlineData("Unknown")]
    [InlineData("Published, Draft")]
    public void TryParseDefinedName_RejectsNonNames(string? input)
    {
        var parsed = EnumExtension.TryParseDefinedName<PackageState>(input, out var value);

        Assert.False(parsed);
        Assert.Equal(default, value);
    }

    /// <summary>
    /// Accepts a named flags member but rejects an unnamed combination and comma-separated input.
    /// </summary>
    /// <param name="input">The input value evaluated by this scenario.</param>
    /// <param name="expected">The expected result asserted by the scenario.</param>
    [Theory]
    [InlineData("Read", true)]
    [InlineData("All", true)]
    [InlineData("Read, Write", false)]
    [InlineData("3", false)]
    public void TryParseDefinedName_RequiresOneDeclaredFlagsName(string input, bool expected)
    {
        var parsed = EnumExtension.TryParseDefinedName<AccessMode>(input, out _);

        Assert.Equal(expected, parsed);
    }

    /// <summary>
    /// Reads metadata independently rather than deriving display text from another attribute.
    /// </summary>
    [Fact]
    public void Metadata_UsesDescriptionAndDisplayName()
    {
        Assert.Equal("Ready for use.", PackageState.Published.GetDescription());
        Assert.Equal("Published package", PackageState.Published.GetDisplayName());
    }

    /// <summary>
    /// Uses the declared name when an enum member has no presentation metadata.
    /// </summary>
    [Fact]
    public void Metadata_FallsBackToDeclaredName()
    {
        Assert.Equal("Draft", PackageState.Draft.GetDescription());
        Assert.Equal("Draft", PackageState.Draft.GetDisplayName());
    }

    /// <summary>
    /// Preserves the native representation of undefined values without claiming that they are valid members.
    /// </summary>
    [Fact]
    public void Metadata_FallsBackToUndefinedValueRepresentation()
    {
        var value = (PackageState)99;

        Assert.Equal("99", value.GetDescription());
        Assert.Equal("99", value.GetDisplayName());
    }

    /// <summary>
    /// Handles long and unsigned underlying values without an intermediate Int32 conversion.
    /// </summary>
    [Fact]
    public void ParsingAndMetadata_DoNotNarrowUnderlyingValues()
    {
        Assert.True(EnumExtension.TryParseDefinedName<LongState>("Large", out var signed));
        Assert.True(EnumExtension.TryParseDefinedName<UnsignedState>("Maximum", out var unsigned));
        Assert.Equal(LongState.Large, signed);
        Assert.Equal(UnsignedState.Maximum, unsigned);
        Assert.Equal("Large", signed.GetDescription());
        Assert.Equal("Maximum", unsigned.GetDisplayName());
    }

    /// <summary>
    /// Provides separate description and display fixtures with an undecorated fallback member.
    /// </summary>
    private enum PackageState
    {
        /// <summary>
        /// Represents a member without presentation metadata.
        /// </summary>
        Draft,

        /// <summary>
        /// Represents a member with independent display and description values.
        /// </summary>
        [Description("Ready for use.")]
        [Display(Name = "Published package")]
        Published
    }

    /// <summary>
    /// Distinguishes a declared combined flags name from a comma-separated expression.
    /// </summary>
    [Flags]
    private enum AccessMode
    {
        /// <summary>
        /// Grants read capability in the synthetic fixture.
        /// </summary>
        Read = 1,

        /// <summary>
        /// Grants write capability in the synthetic fixture.
        /// </summary>
        Write = 2,

        /// <summary>
        /// Declares an explicit name for both capabilities.
        /// </summary>
        All = Read | Write
    }

    /// <summary>
    /// Exercises a signed enum value outside Int32 range.
    /// </summary>
    private enum LongState : long
    {
        /// <summary>
        /// Exceeds the supported range of the legacy numeric conversion helper.
        /// </summary>
        Large = long.MaxValue
    }

    /// <summary>
    /// Exercises the full unsigned enum range.
    /// </summary>
    private enum UnsignedState : ulong
    {
        /// <summary>
        /// Requires preserving the original unsigned underlying type.
        /// </summary>
        Maximum = ulong.MaxValue
    }
}
