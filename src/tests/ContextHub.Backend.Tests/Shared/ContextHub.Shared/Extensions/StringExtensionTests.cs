namespace ContextHub.Backend.Tests.Shared.ContextHub.Shared.Extensions;

/// <summary>
/// Verifies string normalization, complete prefix and suffix removal, and Unicode-safe diacritic handling.
/// </summary>
public sealed class StringExtensionTests
{
    /// <summary>
    /// Converts the Vietnamese character groups without trimming, recasing or transliterating other scripts.
    /// </summary>
    /// <param name="source">The optional text containing composed Vietnamese characters.</param>
    /// <param name="expected">The literal result with only the selected character groups replaced.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("áàảãạâấầẩẫậăắằẳẵặ", "aaaaaaaaaaaaaaaaa")]
    [InlineData("ÁÀẢÃẠÂẤẦẨẪẬĂẮẰẲẴẶ", "AAAAAAAAAAAAAAAAA")]
    [InlineData("éèẻẽẹêếềểễệ", "eeeeeeeeeee")]
    [InlineData("ÉÈẺẼẸÊẾỀỂỄỆ", "EEEEEEEEEEE")]
    [InlineData("íìỉĩịÍÌỈĨỊ", "iiiiiIIIII")]
    [InlineData("óòỏõọôốồổỗộơớờởỡợ", "ooooooooooooooooo")]
    [InlineData("ÓÒỎÕỌÔỐỒỔỖỘƠỚỜỞỠỢ", "OOOOOOOOOOOOOOOOO")]
    [InlineData("úùủũụưứừửữự", "uuuuuuuuuuu")]
    [InlineData("ÚÙỦŨỤƯỨỪỬỮỰ", "UUUUUUUUUUU")]
    [InlineData("ýỳỷỹỵÝỲỶỸỴđĐ", "yyyyyYYYYYdD")]
    [InlineData("  ĐIỆN thoại, 123!\t", "  DIEN thoai, 123!\t")]
    [InlineData("Åäöñç Σά بِ 中文 👨‍👩‍👧‍👦", "Åäöñç Σά بِ 中文 👨‍👩‍👧‍👦")]
    public void RemoveVietnameseDiacritics_OnlyReplacesVietnameseCharacterGroups(string? source, string? expected)
    {
        Assert.Equal(expected, source.RemoveVietnameseDiacritics());
    }

    /// <summary>
    /// Treats decomposed Vietnamese input consistently and preserves unrelated combining marks.
    /// </summary>
    /// <param name="cultureName">The culture whose casing rules must not affect conversion.</param>
    [Theory]
    [InlineData("tr-TR")]
    [InlineData("vi-VN")]
    [InlineData("en-US")]
    public void RemoveVietnameseDiacritics_HandlesCanonicalFormsIndependentlyOfCulture(string cultureName)
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(cultureName);
            var decomposed = "Điện THOẠI بِ 👩‍💻".Normalize(NormalizationForm.FormD);

            Assert.Equal("Dien THOAI بِ 👩‍💻", decomposed.RemoveVietnameseDiacritics());
        }
        finally
        {
            // Culture is test-local state; restore it even when an assertion fails.
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// Treats absent text as a no-op while preserving whole Unicode text elements.
    /// </summary>
    /// <param name="source">The optional text to shorten.</param>
    /// <param name="maximum">The maximum number of text elements.</param>
    /// <param name="expected">The expected unchanged or shortened text.</param>
    [Theory]
    [InlineData(null, 2, null)]
    [InlineData("", 0, "")]
    [InlineData("👨‍👩‍👧‍👦éZ", 1, "👨‍👩‍👧‍👦")]
    [InlineData("éZ", 1, "é")]
    [InlineData("abc", 0, "")]
    [InlineData("abc", 5, "abc")]
    public void Truncate_PreservesOptionalTextAndGraphemes(string? source, int maximum, string? expected)
    {
        Assert.Equal(expected, source!.Truncate(maximum));
    }

    /// <summary>
    /// Rejects an invalid limit even when there is no source text to shorten.
    /// </summary>
    [Fact]
    public void Truncate_NegativeLimitIsInvalidForAbsentText()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((string)null!).Truncate(-1));
    }

    /// <summary>
    /// Verifies that complete repeated suffixes are removed without changing unmatched input.
    /// </summary>
    /// <param name="source">The optional original string, including any repeated suffixes.</param>
    /// <param name="suffix">The complete suffix to remove using ordinal comparison.</param>
    /// <param name="expected">The expected result asserted by the scenario.</param>
    [Theory]
    [InlineData("--value----", "--", "--value")]
    [InlineData("value", "--", "value")]
    [InlineData("", "--", "")]
    [InlineData(null, "--", null)]
    [InlineData("abab", "ab", "")]
    [InlineData("valueAB", "ab", "valueAB")]
    public void RemoveEndWith_RemovesWholeRepeatedSuffix(string? source, string suffix, string? expected)
    {
        Assert.Equal(expected, source.RemoveEndWith(suffix));
    }

    /// <summary>
    /// Removes complete repeated prefixes while preserving absent, empty and unmatched input.
    /// </summary>
    /// <param name="source">The optional original text.</param>
    /// <param name="prefix">The complete prefix to remove using ordinal comparison.</param>
    /// <param name="expected">The unchanged or fully stripped result.</param>
    [Theory]
    [InlineData(null, "ab", null)]
    [InlineData("", "ab", "")]
    [InlineData("ababvalue", "ab", "value")]
    [InlineData("abab", "ab", "")]
    [InlineData("value", "ab", "value")]
    [InlineData("ABvalue", "ab", "ABvalue")]
    public void RemoveStartWith_RemovesWholeRepeatedPrefix(string? source, string prefix, string? expected)
    {
        Assert.Equal(expected, source.RemoveStartWith(prefix));
    }

    /// <summary>
    /// Leaves text unchanged when no removal pattern is supplied, including absent and empty sources.
    /// </summary>
    /// <param name="source">The source, including null and empty no-op cases.</param>
    /// <param name="value">The absent or empty removal pattern.</param>
    /// <param name="expected">The original source returned unchanged.</param>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "", null)]
    [InlineData("", null, "")]
    [InlineData("", "", "")]
    [InlineData("value", null, "value")]
    [InlineData("value", "", "value")]
    public void Removal_AbsentPatternPreservesSource(string? source, string? value, string? expected)
    {
        // An absent removal request is a no-op, not an invalid argument or a zero-length loop iteration.
        var prefixResult = source.RemoveStartWith(value);
        var suffixResult = source.RemoveEndWith(value);

        Assert.Equal(expected, prefixResult);
        Assert.Equal(expected, suffixResult);
    }

    /// <summary>
    /// Verifies that optional strings are trimmed and absent or whitespace-only values become null.
    /// </summary>
    /// <param name="source">The optional text to trim or treat as absent.</param>
    /// <param name="expected">The expected result asserted by the scenario.</param>
    [Theory]
    [InlineData("  value  ", "value")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    public void NormalizeOptional_HandlesNullableAndBlankValues(string? source, string? expected)
    {
        Assert.Equal(expected, source.NormalizeOptional());
    }

    /// <summary>
    /// Verifies equivalent normalization of Vietnamese Unicode forms without damaging unrelated characters.
    /// </summary>
    [Fact]
    public void RemoveDiacritics_HandlesComposedAndDecomposedVietnamese()
    {
        Assert.Equal("Dien thoai", "Điện thoại".RemoveDiacritics());
        Assert.Equal(
            "Dien thoai",
            "Điện thoại".Normalize(NormalizationForm.FormD).RemoveDiacritics());
        Assert.Equal("中文", "中文".RemoveDiacritics());
    }

    /// <summary>
    /// Changes only the first character and preserves absent, empty and single-character input.
    /// </summary>
    /// <param name="source">The nullable original text.</param>
    /// <param name="lower">The expected lowercase-first text.</param>
    /// <param name="upper">The expected uppercase-first text.</param>
    [Theory]
    [InlineData(null, null, null)]
    [InlineData("", "", "")]
    [InlineData("A", "a", "A")]
    [InlineData("z", "z", "Z")]
    [InlineData("HelloWORLD", "helloWORLD", "HelloWORLD")]
    [InlineData("éTest", "éTest", "ÉTest")]
    [InlineData("😀Test", "😀Test", "😀Test")]
    public void FirstCharacterCasing_PreservesRemainingText(string? source, string? lower, string? upper)
    {
        Assert.Equal(lower, source.FirstCharToLowerCase());
        Assert.Equal(upper, source.FirstCharToUpperCase());
    }

    /// <summary>
    /// Keeps casing independent of the current culture and restores test-local culture afterwards.
    /// </summary>
    [Fact]
    public void Casing_UsesInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            // Turkish casing differs for I/i; the helper contract must not depend on the executing host.
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

            Assert.Equal("iTail", "ITail".FirstCharToLowerCase());
            Assert.Equal("ITail", "iTail".FirstCharToUpperCase());
            Assert.Equal("I12", " i-12 ".NormalizeAlphanumericCode());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    /// <summary>
    /// Removes all occurrences of the requested characters without changing unrelated Unicode text.
    /// </summary>
    [Fact]
    public void RemoveCharacters_HandlesOptionalAndRepeatedCharacters()
    {
        Assert.Null(((string?)null).RemoveCharacters('-'));
        Assert.Equal("", "".RemoveCharacters('-'));
        Assert.Equal("a-b", "a-b".RemoveCharacters(null));
        Assert.Equal("a-b", "a-b".RemoveCharacters());
        Assert.Equal("é😀ab", "é😀--a_b--".RemoveCharacters('-', '_', '-'));
    }

    /// <summary>
    /// Distinguishes absent input from punctuation-only input and retains Unicode letters and digits.
    /// </summary>
    /// <param name="source">The optional code text to normalize deliberately.</param>
    /// <param name="expected">The uppercase alphanumeric result or null for blank input.</param>
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \t ", null)]
    [InlineData("---!!!", "")]
    [InlineData(" ab-c_12 ", "ABC12")]
    [InlineData("é-中文-١", "É中文١")]
    public void NormalizeAlphanumericCode_UsesDeclaredCharacterRules(string? source, string? expected)
    {
        Assert.Equal(expected, source.NormalizeAlphanumericCode());
    }
}
