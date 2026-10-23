namespace ContextHub.Core.Helpers.Security;

/// <summary>
/// Generates numeric identifier suffixes without owning uniqueness, persistence or business prefixes.
/// </summary>
public static class CodeGenerationHelper
{
    /// <summary>
    /// Generates a fixed-width string of cryptographically random decimal digits, including leading zeroes.
    /// </summary>
    /// <param name="length">The required digit count, from one through thirty-two.</param>
    /// <returns>The numeric string; uniqueness must be enforced by the owning service.</returns>
    public static string GenerateNumericCode(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, CoreConstants.CodeVariables.MAX_LENGTH);

        // Generate digits directly: converting a random integer to text would lose leading zeroes and bias lengths.
        return string.Create(length, 0, static (digits, _) =>
        {
            for (var index = 0; index < digits.Length; index++)
            {
                digits[index] = (char)('0' + RandomNumberGenerator.GetInt32(10));
            }
        });
    }

    /// <summary>
    /// Combines a caller-owned prefix with a fixed-width random suffix, without a uniqueness guarantee.
    /// </summary>
    /// <param name="prefix">The required prefix, trimmed before formatting.</param>
    /// <param name="randomLength">The numeric suffix length from one through thirty-two.</param>
    /// <param name="separator">The separator; null omits it.</param>
    /// <returns>The formatted identifier ready for service-owned uniqueness enforcement.</returns>
    public static string GenerateCode(string prefix, int randomLength, string? separator = CoreConstants.CodeVariables.SEPARATOR)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        return string.Concat(prefix.Trim(), separator, GenerateNumericCode(randomLength));
    }
}
