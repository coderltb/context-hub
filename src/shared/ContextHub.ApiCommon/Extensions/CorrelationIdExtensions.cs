namespace ContextHub.ApiCommon.Extensions;

/// <summary>
/// Defines the canonical UUID boundary shared by inbound and outbound HTTP correlation.
/// </summary>
public static class CorrelationIdExtensions
{
    /// <summary>
    /// Checks a single identifier without accepting whitespace, lists or arbitrary header content.
    /// </summary>
    /// <param name="value">The candidate correlation identifier.</param>
    /// <returns>Whether the value has the canonical hyphenated UUID shape.</returns>
    public static bool IsCanonicalCorrelationId(this string? value)
    {
        return value is { Length: 36 } && Guid.TryParseExact(value, "D", out _);
    }
}
