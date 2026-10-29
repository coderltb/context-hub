namespace ContextHub.Shared.Extensions;

/// <summary>
/// Provides stateless text helpers adapted from the reference projects, without HTTP or business rules.
/// </summary>
public static class StringExtension
{
    /// <summary>
    /// Lowercases only the first character using invariant casing, preserving the remainder unchanged.
    /// </summary>
    /// <param name="source">The optional original text.</param>
    /// <returns>The lowercase-first text, or the original null or empty value.</returns>
    public static string? FirstCharToLowerCase(this string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        // Invariant casing keeps the result independent of the host's locale; the suffix is never normalized.
        return char.ToLowerInvariant(source[0]) + source[1..];
    }

    /// <summary>
    /// Uppercases only the first character using invariant casing, preserving the remainder unchanged.
    /// </summary>
    /// <param name="source">The optional original text.</param>
    /// <returns>The uppercase-first text, or the original null or empty value.</returns>
    public static string? FirstCharToUpperCase(this string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        // Only the first UTF-16 character is cased; unrelated characters, including emoji, remain untouched.
        return char.ToUpperInvariant(source[0]) + source[1..];
    }

    /// <summary>
    /// Removes every occurrence of the explicitly supplied characters without trimming or recasing other text.
    /// </summary>
    /// <param name="source">The optional original text.</param>
    /// <param name="charsToRemove">The characters to remove; null or empty means no change.</param>
    /// <returns>The filtered text, or the original value when no removal is requested.</returns>
    public static string? RemoveCharacters(this string? source, params char[]? charsToRemove)
    {
        if (string.IsNullOrEmpty(source) || charsToRemove is not { Length: > 0 })
        {
            return source;
        }

        // Retain the original order and remove all matches, even when the removal list repeats a character.
        return new string(source.Where(character => !charsToRemove.Contains(character)).ToArray());
    }

    /// <summary>
    /// Trims and uppercases optional code text, retaining only Unicode characters accepted by char.IsLetterOrDigit.
    /// </summary>
    /// <param name="source">The optional code text selected explicitly by the caller.</param>
    /// <returns>Null for blank input, otherwise the alphanumeric text, including empty for punctuation-only input.</returns>
    public static string? NormalizeAlphanumericCode(this string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        // Do not conflate a supplied punctuation-only code with absent input; this helper does not define wire vocabulary.
        return new string(source.Trim().ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>
    /// Limits complete Unicode text elements, including composed emoji and combining marks.
    /// </summary>
    /// <param name="source">The original text.</param>
    /// <param name="maximum">The nonnegative number of text elements to retain.</param>
    /// <returns>The complete prefix, or the original string when within the limit.</returns>
    public static string? Truncate(this string? source, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximum);

        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        // StringInfo uses grapheme boundaries rather than cutting surrogate pairs or combining sequences.
        var boundaries = StringInfo.ParseCombiningCharacters(source);

        return boundaries.Length <= maximum ? source : source[..boundaries[maximum]];
    }

    /// <summary>
    /// Converts word boundaries and underscore separators to lowercase kebab-case.
    /// </summary>
    /// <param name="source">The original identifier.</param>
    /// <returns>The normalized identifier, with acronym boundaries preserved.</returns>
    public static string ToKebabCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new StringBuilder(source.Length);
        for (var index = 0; index < source.Length; index++)
        {
            var current = source[index];
            if (current is '-' or '_' || char.IsWhiteSpace(current))
            {
                if (result.Length > 0 && result[^1] != '-')
                {
                    result.Append('-');
                }

                continue;
            }

            // Split HTTPClient into HTTP + Client, not H + T + T + P + Client.
            var boundary = index > 0 && char.IsUpper(current) &&
                (char.IsLower(source[index - 1]) || char.IsDigit(source[index - 1]) ||
                 (char.IsUpper(source[index - 1]) && index + 1 < source.Length && char.IsLower(source[index + 1])));
            if (boundary && result.Length > 0 && result[^1] != '-')
            {
                result.Append('-');
            }

            result.Append(char.ToLowerInvariant(current));
        }

        return result.ToString().TrimEnd('-');
    }

    /// <summary>
    /// Converts kebab-separated words to a lowercase-first camel-case identifier.
    /// </summary>
    /// <param name="source">The kebab-case identifier.</param>
    /// <returns>The identifier without separators.</returns>
    public static string KebabToCamelCase(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new StringBuilder(source.Length);
        var uppercaseNext = false;
        foreach (var character in source)
        {
            if (character == '-')
            {
                uppercaseNext = result.Length > 0;
                continue;
            }

            var nextCharacter = character;
            if (result.Length == 0)
            {
                nextCharacter = char.ToLowerInvariant(character);
            }
            else if (uppercaseNext)
            {
                nextCharacter = char.ToUpperInvariant(character);
            }

            result.Append(nextCharacter);
            uppercaseNext = false;
        }

        return result.ToString();
    }

    /// <summary>
    /// Trims optional text and treats blank values as absent; identifiers and credentials must not use this helper.
    /// </summary>
    /// <param name="source">Optional descriptive text.</param>
    /// <returns>The trimmed value, or null when absent.</returns>
    public static string? NormalizeOptional(this string? source) =>
        string.IsNullOrWhiteSpace(source) ? null : source.Trim();

    /// <summary>
    /// Removes complete repeated prefixes using ordinal comparison rather than recursive character removal.
    /// </summary>
    /// <param name="source">The optional original text; null and empty values are preserved.</param>
    /// <param name="value">The optional prefix; null or empty means no removal.</param>
    /// <returns>The text after removing matching prefixes, or the original value when no removal applies.</returns>
    public static string? RemoveStartWith(this string? source, string? value)
    {
        // Missing text or pattern means no work; skip empty patterns so the loop always advances.
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
        {
            return source;
        }

        // Advance by the entire prefix so repeated multi-character values are removed without recursion.
        var offset = 0;
        while (source.AsSpan(offset).StartsWith(value, StringComparison.Ordinal))
        {
            offset += value.Length;
        }

        return source[offset..];
    }

    /// <summary>
    /// Removes complete repeated suffixes using ordinal comparison.
    /// </summary>
    /// <param name="source">The optional original text; null and empty values are preserved.</param>
    /// <param name="value">The optional suffix; null or empty means no removal.</param>
    /// <returns>The text after removing matching suffixes, or the original value when no removal applies.</returns>
    public static string? RemoveEndWith(this string? source, string? value)
    {
        // An absent removal request is a no-op; an empty suffix must never enter the removal loop.
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(value))
        {
            return source;
        }

        // Move the end boundary without allocating intermediate strings for every repeated suffix.
        var length = source.Length;
        while (source.AsSpan(0, length).EndsWith(value, StringComparison.Ordinal))
        {
            length -= value.Length;
        }

        return source[..length];
    }

    /// <summary>
    /// Replaces Vietnamese accented letters while retaining casing, whitespace and unrelated Unicode characters.
    /// </summary>
    /// <param name="source">The optional text explicitly selected for Vietnamese diacritic removal.</param>
    /// <returns>The canonically composed result, or the original null or empty value.</returns>
    public static string? RemoveVietnameseDiacritics(this string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return source;
        }

        // Compose first so precomposed and decomposed Vietnamese letters use the same fixed character mapping.
        var normalized = source.Normalize(NormalizationForm.FormC);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            // Unlike broad diacritic removal, this does not strip combining marks from other writing systems.
            result.Append(character switch
            {
                'á' or 'à' or 'ả' or 'ã' or 'ạ' or 'â' or 'ấ' or 'ầ' or 'ẩ' or 'ẫ' or 'ậ' or
                    'ă' or 'ắ' or 'ằ' or 'ẳ' or 'ẵ' or 'ặ' => 'a',
                'Á' or 'À' or 'Ả' or 'Ã' or 'Ạ' or 'Â' or 'Ấ' or 'Ầ' or 'Ẩ' or 'Ẫ' or 'Ậ' or
                    'Ă' or 'Ắ' or 'Ằ' or 'Ẳ' or 'Ẵ' or 'Ặ' => 'A',
                'é' or 'è' or 'ẻ' or 'ẽ' or 'ẹ' or 'ê' or 'ế' or 'ề' or 'ể' or 'ễ' or 'ệ' => 'e',
                'É' or 'È' or 'Ẻ' or 'Ẽ' or 'Ẹ' or 'Ê' or 'Ế' or 'Ề' or 'Ể' or 'Ễ' or 'Ệ' => 'E',
                'í' or 'ì' or 'ỉ' or 'ĩ' or 'ị' => 'i',
                'Í' or 'Ì' or 'Ỉ' or 'Ĩ' or 'Ị' => 'I',
                'ó' or 'ò' or 'ỏ' or 'õ' or 'ọ' or 'ô' or 'ố' or 'ồ' or 'ổ' or 'ỗ' or 'ộ' or
                    'ơ' or 'ớ' or 'ờ' or 'ở' or 'ỡ' or 'ợ' => 'o',
                'Ó' or 'Ò' or 'Ỏ' or 'Õ' or 'Ọ' or 'Ô' or 'Ố' or 'Ồ' or 'Ổ' or 'Ỗ' or 'Ộ' or
                    'Ơ' or 'Ớ' or 'Ờ' or 'Ở' or 'Ỡ' or 'Ợ' => 'O',
                'ú' or 'ù' or 'ủ' or 'ũ' or 'ụ' or 'ư' or 'ứ' or 'ừ' or 'ử' or 'ữ' or 'ự' => 'u',
                'Ú' or 'Ù' or 'Ủ' or 'Ũ' or 'Ụ' or 'Ư' or 'Ứ' or 'Ừ' or 'Ử' or 'Ữ' or 'Ự' => 'U',
                'ý' or 'ỳ' or 'ỷ' or 'ỹ' or 'ỵ' => 'y',
                'Ý' or 'Ỳ' or 'Ỷ' or 'Ỹ' or 'Ỵ' => 'Y',
                'đ' => 'd',
                'Đ' => 'D',
                _ => character
            });
        }

        return result.ToString();
    }

    /// <summary>
    /// Removes combining diacritics and folds Vietnamese D-with-stroke; does not promise ASCII transliteration.
    /// </summary>
    /// <param name="source">The text to normalize for a deliberate text-search projection.</param>
    /// <returns>The normalized text with non-diacritic Unicode characters retained.</returns>
    public static string RemoveDiacritics(this string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // FormD separates accents; rune iteration preserves supplementary Unicode characters.
        var result = new StringBuilder(source.Length);
        foreach (var rune in source.Normalize(NormalizationForm.FormD).EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            // Vietnamese D-with-stroke does not decompose into a base letter and combining mark.
            result.Append(rune.Value switch
            {
                0x0110 => "D",
                0x0111 => "d",
                _ => rune.ToString()
            });
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
